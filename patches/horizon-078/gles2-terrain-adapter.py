#!/usr/bin/env python3
"""Install a safe CPU adapter for Horizon section meshes on GLES2."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
lib = root / "crates/horizon-client-render/src/lib.rs"
adapter = root / "crates/horizon-client-render/src/gles2_terrain.rs"

module = "pub mod gles2_terrain;\n"
source = lib.read_text(encoding="utf-8")
if source.count(module) == 0:
    anchor = "pub mod gles2_hud;\n"
    if source.count(anchor) != 1:
        raise SystemExit("Could not uniquely locate gles2_hud module declaration")
    lib.write_text(source.replace(anchor, anchor + module), encoding="utf-8")
elif source.count(module) != 1:
    raise SystemExit("Unexpected duplicate gles2_terrain module declaration")

adapter_source = r'''//! CPU conversion of Horizon section meshes to a GLES2 vertex layout.
//!
//! No GL calls or unsafe code live here. The output avoids integer attributes and
//! texture arrays; a GLES2 renderer can sample a 2D atlas using atlas_origin/scale
//! and Horizon's exact tile_uv formula in its GLSL ES 1.00 fragment shader
//! (uv - max(ceil(uv) - 1, 0)).
use crate::mesher::{SectionMesh, Sides, TerrainVertex};

/// Horizon stores the opaque-layer marker in the high bit; the remaining bits are
/// the sprite-array layer, as used by the terrain material shader.
pub const OPAQUE_LAYER_BIT: u16 = crate::mesher::OPAQUE_LAYER;
pub const SPRITE_LAYER_MASK: u16 = !OPAQUE_LAYER_BIT;

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct AtlasGrid {
    pub sprite_size: u32,
    pub texture_layers: u32,
    pub columns: u32,
    pub rows: u32,
    pub width: u32,
    pub height: u32,
}

impl AtlasGrid {
    pub fn new(sprite_size: u32, texture_layers: u32, max_texture_size: u32) -> Result<Self, &'static str> {
        if sprite_size == 0 || texture_layers == 0 || max_texture_size == 0 {
            return Err("sprite size, layer count and texture limit must be nonzero");
        }
        if texture_layers > u32::from(SPRITE_LAYER_MASK) + 1 {
            return Err("Horizon terrain layer encoding supports at most 32768 sprites");
        }
        if sprite_size > max_texture_size {
            return Err("one sprite exceeds the GLES2 texture limit");
        }
        let max_columns = max_texture_size / sprite_size;
        let ideal_columns = (texture_layers as f64).sqrt().ceil() as u32;
        let columns = ideal_columns.clamp(1, max_columns);
        let rows = texture_layers.div_ceil(columns);
        if rows > max_texture_size / sprite_size {
            return Err("terrain sprite layers do not fit in one GLES2 atlas");
        }
        Ok(Self {
            sprite_size,
            texture_layers,
            columns,
            rows,
            width: columns * sprite_size,
            height: rows * sprite_size,
        })
    }

    pub fn tile(&self, encoded_layer: u16) -> Result<([f32; 2], [f32; 2], bool), &'static str> {
        let layer = u32::from(encoded_layer & SPRITE_LAYER_MASK);
        if layer >= self.texture_layers {
            return Err("terrain vertex references a sprite layer outside the atlas");
        }
        let x = layer % self.columns;
        let y = layer / self.columns;
        Ok((
            [x as f32 / self.columns as f32, y as f32 / self.rows as f32],
            [1.0 / self.columns as f32, 1.0 / self.rows as f32],
            encoded_layer & OPAQUE_LAYER_BIT != 0,
        ))
    }

    /// Pack Horizon's layer-major RGBA sprite pixels into the 2D atlas.
    /// Extra source bytes are ignored because resource preparation may append
    /// data for other texture families; every requested base layer must exist.
    pub fn pack_rgba(&self, pixels: &[u8]) -> Result<Vec<u8>, &'static str> {
        let row_bytes = self.sprite_size.checked_mul(4).ok_or("sprite row size overflow")? as usize;
        let layer_bytes = row_bytes.checked_mul(self.sprite_size as usize).ok_or("sprite layer size overflow")?;
        let source_bytes = layer_bytes.checked_mul(self.texture_layers as usize).ok_or("sprite source size overflow")?;
        if pixels.len() < source_bytes {
            return Err("sprite pixel data is shorter than the declared layer count");
        }
        let atlas_bytes = (self.width as usize)
            .checked_mul(self.height as usize)
            .and_then(|size| size.checked_mul(4))
            .ok_or("terrain atlas allocation size overflow")?;
        let mut atlas = vec![0u8; atlas_bytes];
        for layer in 0..self.texture_layers {
            let tile_x = layer % self.columns;
            let tile_y = layer / self.columns;
            let source_layer = layer as usize * layer_bytes;
            for row in 0..self.sprite_size {
                let source_start = source_layer + row as usize * row_bytes;
                let source_end = source_start + row_bytes;
                let dest_start = (((tile_y * self.sprite_size + row) * self.width
                    + tile_x * self.sprite_size) * 4) as usize;
                let dest_end = dest_start + row_bytes;
                atlas[dest_start..dest_end].copy_from_slice(&pixels[source_start..source_end]);
            }
        }
        Ok(atlas)
    }
}

/// GLES2-compatible vertex: float attributes plus normalized byte colour/light.
/// uv remains sprite-local; the shader must use Horizon's exact tile_uv formula
/// (uv - max(ceil(uv) - 1, 0)) before adding atlas_origin.
#[repr(C)]
/// Decoded face metadata retained in TerrainVertex::color's upper alpha nibble.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Gles2MaterialFlags {
    pub fluid_face: Option<u8>,
}

/// Horizon encodes fluid faces as 8 + Direction (0..5); other values are not fluids.
pub fn decode_material_flags(flags: u8) -> Gles2MaterialFlags {
    let high = flags >> 4;
    let fluid_face = if high >= 8 && (high & 7) < 6 { Some(high & 7) } else { None };
    Gles2MaterialFlags { fluid_face }
}

/// GLES2-compatible vertex: float attributes plus normalized byte colour/light.
#[repr(C)]
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Gles2TerrainVertex {
    pub position: [f32; 3],
    pub uv: [f32; 2],
    pub atlas_origin: [f32; 2],
    pub atlas_scale: [f32; 2],
    pub color: [u8; 4],
    /// HDR emission tier decoded from the low nibble of packed vertex alpha.
    pub emission: u8,
    /// Upper alpha nibble: native material face flags, including fluid-face marker.
    pub material_flags: u8,
    /// Sky light first, block light second; each is Horizon's original 0..255 byte.
    pub light: [u8; 2],
    /// Float attribute so GLES2 does not need integer vertex attributes.
    pub opaque_layer: f32,
}

pub fn convert_vertex_with_uv_step(
    vertex: &TerrainVertex,
    atlas: &AtlasGrid,
    uv_step: f32,
) -> Result<Gles2TerrainVertex, &'static str> {
    if !uv_step.is_finite() || uv_step <= 0.0 {
        return Err("terrain UV step must be finite and positive");
    }
    let (atlas_origin, atlas_scale, opaque) = atlas.tile(vertex.layer)?;
    let packed_color = vertex.color.to_le_bytes();
    let emission = 15 - (packed_color[3] & 0x0f);
    let material_flags = packed_color[3] >> 4;
    Ok(Gles2TerrainVertex {
        position: vertex.pos,
        uv: vertex.uv.map(|value| f32::from(value) * uv_step),
        atlas_origin,
        atlas_scale,
        color: packed_color,
        emission,
        material_flags,
        light: [vertex.light as u8, (vertex.light >> 8) as u8],
        opaque_layer: if opaque { 1.0 } else { 0.0 },
    })
}

/// Convert an ordinary 16x16 section mesh, whose UV fixed-point step is 1/32768.
pub fn convert_vertex(vertex: &TerrainVertex, atlas: &AtlasGrid) -> Result<Gles2TerrainVertex, &'static str> {
    convert_vertex_with_uv_step(vertex, atlas, 1.0 / crate::mesher::UV_STEPS)
}

#[derive(Default)]
pub struct Gles2TerrainMesh {
    pub solid: Vec<Gles2TerrainVertex>,
    /// Horizon's solid face buckets, retained in quad units. Convert each
    /// (first, count) returned by Sides::visible() to triangle vertices by
    /// multiplying both values by six before drawing this expanded buffer.
    pub sides: Sides,
    pub translucent: Vec<Gles2TerrainVertex>,
}

/// Horizon's mesher emits four vertices per quad; GLES2 renderer draws GL_TRIANGLES.
/// Expand each quad as (0,1,2) + (2,3,0), preserving its winding and attributes.
fn append_triangle_list(
    source: &[TerrainVertex],
    destination: &mut Vec<Gles2TerrainVertex>,
    atlas: &AtlasGrid,
    uv_step: f32,
) -> Result<(), &'static str> {
    if source.len() % 4 != 0 {
        return Err("Horizon terrain mesh must contain complete four-vertex quads");
    }
    destination.reserve(source.len() / 4 * 6);
    for quad in source.chunks_exact(4) {
        let v0 = convert_vertex_with_uv_step(&quad[0], atlas, uv_step)?;
        let v1 = convert_vertex_with_uv_step(&quad[1], atlas, uv_step)?;
        let v2 = convert_vertex_with_uv_step(&quad[2], atlas, uv_step)?;
        let v3 = convert_vertex_with_uv_step(&quad[3], atlas, uv_step)?;
        destination.extend_from_slice(&[v0, v1, v2, v2, v3, v0]);
    }
    Ok(())
}

pub fn convert_mesh_with_uv_step(
    mesh: &SectionMesh,
    atlas: &AtlasGrid,
    uv_step: f32,
) -> Result<Gles2TerrainMesh, &'static str> {
    let mut converted = Gles2TerrainMesh {
        solid: Vec::with_capacity(mesh.solid.len() / 4 * 6),
        sides: mesh.sides,
        translucent: Vec::with_capacity(mesh.translucent.len() / 4 * 6),
    };
    append_triangle_list(&mesh.solid, &mut converted.solid, atlas, uv_step)?;
    append_triangle_list(&mesh.translucent, &mut converted.translucent, atlas, uv_step)?;
    Ok(converted)
}

/// Convert an ordinary section mesh using Horizon's standard 1/32768 UV step.
pub fn convert_mesh(mesh: &SectionMesh, atlas: &AtlasGrid) -> Result<Gles2TerrainMesh, &'static str> {
    convert_mesh_with_uv_step(mesh, atlas, 1.0 / crate::mesher::UV_STEPS)
}

/// Return visible solid draw ranges in GLES2 triangle-vertex units.
/// Horizon's Sides reports (first, count) in four-vertex quad units;
/// the converted buffer stores six vertices per quad.
pub fn visible_solid_triangle_ranges(sides: &Sides, eye: [f32; 3]) -> Vec<(u32, u32)> {
    let mut ranges = Vec::new();
    sides.visible(eye, |first_quad, quad_count| {
        ranges.push((first_quad * 6, quad_count * 6));
    });
    ranges
}

/// Sort the expanded six-vertex triangles for each Horizon translucent quad.
/// convert_mesh() expands each original quad as (0,1,2, 2,3,0); vertices 0 and 2
/// remain the opposite corners used by Horizon's own quad-centroid sorting.
/// The caller must still order visible sections back-to-front globally.
pub fn sort_translucent_back_to_front(
    vertices: &mut Vec<Gles2TerrainVertex>,
    section_origin: [f32; 3],
    camera_position: [f32; 3],
) -> Result<(), &'static str> {
    if vertices.len() % 6 != 0 {
        return Err("translucent triangle list must contain complete six-vertex quads");
    }
    if section_origin.iter().chain(camera_position.iter()).any(|value| !value.is_finite()) {
        return Err("translucent sort positions must be finite");
    }
    let mut quads = Vec::with_capacity(vertices.len() / 4);
    for (index, quad) in vertices.chunks_exact(6).enumerate() {
        let mut distance_squared = 0.0f32;
        for axis in 0..3 {
            let center = (quad[0].position[axis] + quad[2].position[axis]) * 0.5;
            let delta = center + section_origin[axis] - camera_position[axis];
            distance_squared += delta * delta;
        }
        if !distance_squared.is_finite() {
            return Err("translucent quad distance is not finite");
        }
        quads.push((index, distance_squared));
    }
    quads.sort_by(|left, right| right.1.total_cmp(&left.1));
    let original = vertices.clone();
    for (destination, (source, _)) in quads.into_iter().enumerate() {
        vertices[destination * 6..destination * 6 + 6]
            .copy_from_slice(&original[source * 6..source * 6 + 6]);
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::mesher::TerrainVertex;


    #[test]
    fn sorts_translucent_quad_triangle_groups_back_to_front_using_world_positions() {
        let quad = |z| [
            Gles2TerrainVertex {
                position: [0.0, 0.0, z], uv: [0.0; 2], atlas_origin: [0.0; 2],
                atlas_scale: [1.0; 2], color: [255; 4], emission: 0,
                material_flags: 0, light: [0; 2], opaque_layer: 0.0,
            },
            Gles2TerrainVertex {
                position: [1.0, 0.0, z], uv: [0.0; 2], atlas_origin: [0.0; 2],
                atlas_scale: [1.0; 2], color: [255; 4], emission: 0,
                material_flags: 0, light: [0; 2], opaque_layer: 0.0,
            },
            Gles2TerrainVertex {
                position: [1.0, 1.0, z], uv: [0.0; 2], atlas_origin: [0.0; 2],
                atlas_scale: [1.0; 2], color: [255; 4], emission: 0,
                material_flags: 0, light: [0; 2], opaque_layer: 0.0,
            },
            Gles2TerrainVertex {
                position: [0.0, 1.0, z], uv: [0.0; 2], atlas_origin: [0.0; 2],
                atlas_scale: [1.0; 2], color: [255; 4], emission: 0,
                material_flags: 0, light: [0; 2], opaque_layer: 0.0,
            },
        ];
        let expand = |q: [Gles2TerrainVertex; 4]| [q[0], q[1], q[2], q[2], q[3], q[0]];
        let mut vertices = expand(quad(2.0)).into_iter().chain(expand(quad(8.0))).collect();
        sort_translucent_back_to_front(&mut vertices, [100.0, 0.0, 0.0], [100.0, 0.0, 0.0]).unwrap();
        assert!(vertices[..6].iter().all(|v| v.position[2] == 8.0));
        assert!(vertices[6..].iter().all(|v| v.position[2] == 2.0));
    }

    #[test]
    fn rejects_incomplete_or_non_finite_translucent_geometry() {
        let vertex = Gles2TerrainVertex {
            position: [0.0; 3], uv: [0.0; 2], atlas_origin: [0.0; 2],
            atlas_scale: [1.0; 2], color: [255; 4], emission: 0,
            material_flags: 0, light: [0; 2], opaque_layer: 0.0,
        };
        assert!(sort_translucent_back_to_front(&mut vec![vertex; 5], [0.0; 3], [0.0; 3]).is_err());
        assert!(sort_translucent_back_to_front(&mut Vec::new(), [f32::NAN, 0.0, 0.0], [0.0; 3]).is_err());
    }

    #[test]
    fn converts_horizon_quads_to_gles2_triangle_lists_with_original_winding() {
        let atlas = AtlasGrid::new(16, 1, 4096).unwrap();
        let make = |x, y| TerrainVertex::new([x, y, 0.0], [x, y], 0, u32::MAX, 0);
        let quad = [make(0.0, 0.0), make(1.0, 0.0), make(1.0, 1.0), make(0.0, 1.0)];
        let sides = Sides::unsorted(1);
        let mesh = SectionMesh {
            solid: quad.to_vec(),
            sides,
            translucent: quad.to_vec(),
            ..Default::default()
        };
        let converted = convert_mesh(&mesh, &atlas).unwrap();
        assert_eq!(converted.sides, sides);
        assert_eq!(converted.solid.len(), 6);
        assert_eq!(converted.translucent.len(), 6);
        let positions: Vec<_> = converted.solid.iter().map(|v| v.position).collect();
        assert_eq!(positions, vec![
            [0.0, 0.0, 0.0], [1.0, 0.0, 0.0], [1.0, 1.0, 0.0],
            [1.0, 1.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 0.0],
        ]);
    }


    #[test]
    fn converts_horizon_visible_quad_ranges_to_triangle_vertex_ranges() {
        let sides = Sides::unsorted(3);
        let ranges = visible_solid_triangle_ranges(&sides, [0.0, 0.0, 0.0]);
        assert_eq!(ranges, vec![(0, 18)]);
        let sides = Sides::default();
        assert!(visible_solid_triangle_ranges(&sides, [0.0; 3]).is_empty());
    }

    #[test]
    fn rejects_meshes_with_incomplete_horizon_quads() {
        let atlas = AtlasGrid::new(16, 1, 4096).unwrap();
        let vertex = TerrainVertex::new([0.0; 3], [0.0; 2], 0, u32::MAX, 0);
        let mesh = SectionMesh { solid: vec![vertex; 3], ..Default::default() };
        assert!(convert_mesh(&mesh, &atlas).is_err());
    }

    #[test]
    fn vertex_layout_matches_gles2_attribute_contract() {
        assert_eq!(std::mem::offset_of!(Gles2TerrainVertex, position), 0);
        assert_eq!(std::mem::offset_of!(Gles2TerrainVertex, uv), 12);
        assert_eq!(std::mem::offset_of!(Gles2TerrainVertex, atlas_origin), 20);
        assert_eq!(std::mem::offset_of!(Gles2TerrainVertex, atlas_scale), 28);
        assert_eq!(std::mem::offset_of!(Gles2TerrainVertex, color), 36);
        assert_eq!(std::mem::offset_of!(Gles2TerrainVertex, emission), 40);
        assert_eq!(std::mem::offset_of!(Gles2TerrainVertex, material_flags), 41);
        assert_eq!(std::mem::offset_of!(Gles2TerrainVertex, light), 42);
        assert_eq!(std::mem::offset_of!(Gles2TerrainVertex, opaque_layer), 44);
        assert_eq!(std::mem::size_of::<Gles2TerrainVertex>(), 48);
    }

    #[test]
    fn decodes_horizon_fluid_face_nibble_without_misclassifying_other_materials() {
        for face in 0u8..6 {
            assert_eq!(decode_material_flags((8 + face) << 4).fluid_face, Some(face));
        }
        assert_eq!(decode_material_flags(0x70).fluid_face, None);
        assert_eq!(decode_material_flags(0xE0).fluid_face, None);
        assert_eq!(decode_material_flags(0xF0).fluid_face, None);
    }

    #[test]
    fn atlas_grid_fits_layer_count_inside_texture_limit() {
        let atlas = AtlasGrid::new(16, 300, 4096).unwrap();
        assert_eq!((atlas.columns, atlas.rows), (18, 17));
        assert_eq!((atlas.width, atlas.height), (288, 272));
        assert_eq!(atlas.tile(0).unwrap().0, [0.0, 0.0]);
        assert_eq!(atlas.tile(17).unwrap().0, [17.0 / 18.0, 0.0]);
        assert_eq!(atlas.tile(18).unwrap().0, [0.0, 1.0 / 17.0]);
        assert!(AtlasGrid::new(4097, 1, 4096).is_err());
        assert!(AtlasGrid::new(16, 65536, 4096).is_err());
    }

    #[test]
    fn packs_layer_major_rgba_pixels_into_compact_atlas_tiles() {
        let atlas = AtlasGrid::new(2, 3, 8).unwrap();
        let pixels = [
            1, 1, 1, 255, 1, 1, 1, 255,
            1, 1, 1, 255, 1, 1, 1, 255,
            2, 2, 2, 255, 2, 2, 2, 255,
            2, 2, 2, 255, 2, 2, 2, 255,
            3, 3, 3, 255, 3, 3, 3, 255,
            3, 3, 3, 255, 3, 3, 3, 255,
        ];
        let packed = atlas.pack_rgba(&pixels).unwrap();
        assert_eq!((atlas.width, atlas.height), (4, 4));
        assert_eq!(packed.len(), 4 * 4 * 4);
        assert_eq!(&packed[0..4], &[1, 1, 1, 255]);
        assert_eq!(&packed[2 * 4..2 * 4 + 4], &[2, 2, 2, 255]);
        assert_eq!(&packed[2 * 4 * 4..2 * 4 * 4 + 4], &[3, 3, 3, 255]);
        assert!(atlas.pack_rgba(&pixels[..pixels.len() - 1]).is_err());
    }

    #[test]
    fn converts_packed_vertex_without_integer_attributes() {
        let atlas = AtlasGrid::new(16, 300, 4096).unwrap();
        let vertex = TerrainVertex::new(
            [1.25, 2.5, 3.75],
            [0.25, 1.5],
            0x8000 | 256,
            0x4433_2211,
            0xABCD,
        );
        let converted = convert_vertex(&vertex, &atlas).unwrap();
        assert_eq!(converted.position, [1.25, 2.5, 3.75]);
        assert_eq!(converted.uv, [0.25, 1.5]);
        // Layer 256 occupies column 4, row 14 in the 18x17 compact grid.
        assert_eq!(converted.atlas_origin, [4.0 / 18.0, 14.0 / 17.0]);
        assert_eq!(converted.atlas_scale, [1.0 / 18.0, 1.0 / 17.0]);
        assert_eq!(converted.color, [0x11, 0x22, 0x33, 0x44]);
        assert_eq!(converted.emission, 11);
        assert_eq!(converted.material_flags, 4);
        assert_eq!(converted.light, [0xCD, 0xAB]);
        assert_eq!(converted.opaque_layer, 1.0);
    }

    #[test]
    fn preserves_distant_tile_uv_step_separately_from_section_uvs() {
        let atlas = AtlasGrid::new(16, 1, 4096).unwrap();
        let tiled = TerrainVertex::tiled([0.0; 3], [32.0, 64.0], 0, u32::MAX, 0);
        let section = convert_vertex(&tiled, &atlas).unwrap();
        let distant = convert_vertex_with_uv_step(&tiled, &atlas, 1.0 / crate::mesher::TILED_UV_STEPS).unwrap();
        assert_eq!(section.uv, [1.0 / 32.0, 1.0 / 16.0]);
        assert_eq!(distant.uv, [32.0, 64.0]);
    }

    #[test]
    fn rejects_layers_outside_the_uploaded_texture_set() {
        let atlas = AtlasGrid::new(16, 2, 4096).unwrap();
        let vertex = TerrainVertex::new([0.0; 3], [0.0; 2], 2, u32::MAX, 0);
        assert!(convert_vertex(&vertex, &atlas).is_err());
    }
}
'''
if adapter.exists():
    existing = adapter.read_text(encoding="utf-8")
    if existing != adapter_source:
        raise SystemExit("gles2_terrain.rs already exists with different content; refusing overwrite")
else:
    adapter.write_text(adapter_source, encoding="utf-8")
print("Installed horizon-client-render::gles2_terrain CPU adapter and tests.")
