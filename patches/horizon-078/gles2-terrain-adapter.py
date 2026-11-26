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
use std::collections::VecDeque;

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
    pub cell_size: u32,
    pub width: u32,
    pub height: u32,
}

impl AtlasGrid {
    /// One duplicated border texel on every side prevents linear filtering
    /// from sampling a neighbouring sprite at a tile edge.
    pub const GUTTER: u32 = 1;

    pub fn new(sprite_size: u32, texture_layers: u32, max_texture_size: u32) -> Result<Self, &'static str> {
        if sprite_size == 0 || texture_layers == 0 || max_texture_size == 0 {
            return Err("sprite size, layer count and texture limit must be nonzero");
        }
        if texture_layers > u32::from(SPRITE_LAYER_MASK) + 1 {
            return Err("Horizon terrain layer encoding supports at most 32768 sprites");
        }
        let cell_size = sprite_size.checked_add(Self::GUTTER * 2)
            .ok_or("sprite atlas cell size overflow")?;
        if cell_size > max_texture_size {
            return Err("sprite plus filtering gutter exceeds the GLES2 texture limit");
        }
        let max_columns = max_texture_size / cell_size;
        let ideal_columns = (texture_layers as f64).sqrt().ceil() as u32;
        let columns = ideal_columns.clamp(1, max_columns);
        let rows = texture_layers.div_ceil(columns);
        if rows > max_texture_size / cell_size {
            return Err("terrain sprite layers do not fit in one GLES2 atlas");
        }
        let width = columns.checked_mul(cell_size).ok_or("terrain atlas width overflow")?;
        let height = rows.checked_mul(cell_size).ok_or("terrain atlas height overflow")?;
        Ok(Self {
            sprite_size,
            texture_layers,
            columns,
            rows,
            cell_size,
            width,
            height,
        })
    }

    pub fn tile(&self, encoded_layer: u16) -> Result<([f32; 2], [f32; 2], bool), &'static str> {
        let layer = u32::from(encoded_layer & SPRITE_LAYER_MASK);
        if layer >= self.texture_layers {
            return Err("terrain vertex references a sprite layer outside the atlas");
        }
        let x = (layer % self.columns) * self.cell_size + Self::GUTTER;
        let y = (layer / self.columns) * self.cell_size + Self::GUTTER;
        Ok((
            [x as f32 / self.width as f32, y as f32 / self.height as f32],
            [self.sprite_size as f32 / self.width as f32, self.sprite_size as f32 / self.height as f32],
            encoded_layer & OPAQUE_LAYER_BIT != 0,
        ))
    }

    /// Pack Horizon's layer-major RGBA sprite pixels into a 2D atlas.
    /// Each cell duplicates its border texels to prevent linear-filter bleeding.
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
        let mut atlas = Vec::new();
        atlas.try_reserve_exact(atlas_bytes)
            .map_err(|_| "not enough memory to allocate the packed GLES2 terrain atlas")?;
        atlas.resize(atlas_bytes, 0);
        for layer in 0..self.texture_layers {
            let tile_x = layer % self.columns;
            let tile_y = layer / self.columns;
            let cell_x = tile_x * self.cell_size;
            let cell_y = tile_y * self.cell_size;
            let source_layer = layer as usize * layer_bytes;
            for row in 0..self.sprite_size {
                let source_start = source_layer + row as usize * row_bytes;
                let source_end = source_start + row_bytes;
                let dest_start = (((cell_y + Self::GUTTER + row) * self.width
                    + cell_x + Self::GUTTER) * 4) as usize;
                let dest_end = dest_start + row_bytes;
                atlas[dest_start - 4..dest_start].copy_from_slice(&pixels[source_start..source_start + 4]);
                atlas[dest_start..dest_end].copy_from_slice(&pixels[source_start..source_end]);
                atlas[dest_end..dest_end + 4].copy_from_slice(&pixels[source_end - 4..source_end]);
            }
            let cell_row_bytes = self.cell_size as usize * 4;
            let first_inner = (((cell_y + Self::GUTTER) * self.width + cell_x) * 4) as usize;
            let top = (cell_y * self.width + cell_x) as usize * 4;
            let last_inner = (((cell_y + Self::GUTTER + self.sprite_size - 1) * self.width + cell_x) * 4) as usize;
            let bottom = ((cell_y + self.cell_size - 1) * self.width + cell_x) as usize * 4;
            atlas.copy_within(first_inner..first_inner + cell_row_bytes, top);
            atlas.copy_within(last_inner..last_inner + cell_row_bytes, bottom);
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
    let expanded_count = (source.len() / 4)
        .checked_mul(6)
        .ok_or("expanded Horizon terrain vertex count overflow")?;
    destination.try_reserve(expanded_count)
        .map_err(|_| "not enough memory to expand the Horizon terrain mesh")?;
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
    let solid_capacity = (mesh.solid.len() / 4)
        .checked_mul(6)
        .ok_or("expanded Horizon solid vertex count overflow")?;
    let translucent_capacity = (mesh.translucent.len() / 4)
        .checked_mul(6)
        .ok_or("expanded Horizon translucent vertex count overflow")?;
    let mut converted = Gles2TerrainMesh {
        solid: Vec::new(),
        sides: if mesh.sides.quads() == (mesh.solid.len() / 4) as u32 {
            mesh.sides
        } else {
            Sides::unsorted((mesh.solid.len() / 4) as u32)
        },
        translucent: Vec::new(),
    };
    converted.solid.try_reserve_exact(solid_capacity)
        .map_err(|_| "not enough memory to allocate converted Horizon solid terrain")?;
    converted.translucent.try_reserve_exact(translucent_capacity)
        .map_err(|_| "not enough memory to allocate converted Horizon translucent terrain")?;
    append_triangle_list(&mesh.solid, &mut converted.solid, atlas, uv_step)?;
    append_triangle_list(&mesh.translucent, &mut converted.translucent, atlas, uv_step)?;
    Ok(converted)
}

/// Convert an ordinary section mesh using Horizon's standard 1/32768 UV step.
pub fn convert_mesh(mesh: &SectionMesh, atlas: &AtlasGrid) -> Result<Gles2TerrainMesh, &'static str> {
    convert_mesh_with_uv_step(mesh, atlas, 1.0 / crate::mesher::UV_STEPS)
}

/// Owned, backend-neutral section data ready to cross from Horizon's meshing
/// workers to a serialized GLES2 upload owner. It contains no GL/EGL handles.
pub struct Gles2SectionPayload {
    pub key: (i32, i32, i32),
    pub geometry: Gles2TerrainMesh,
    pub visibility: u64,
    pub block_entities: Vec<([i32; 3], u32)>,
    pub lights: Vec<([i32; 3], u32)>,
    pub sprite_layers: Vec<u16>,
    pub bounds: Option<[[f32; 3]; 2]>,
    pub keep: bool,
}

impl Gles2SectionPayload {
    /// Reserved CPU buffer bytes owned by this payload, with checked arithmetic.
    /// Capacities are used rather than lengths so queue limits include spare Vec
    /// allocations retained by the payload on memory-constrained Android devices.
    pub fn byte_len(&self) -> Result<usize, &'static str> {
        let vertices = self.geometry.solid.capacity()
            .checked_add(self.geometry.translucent.capacity())
            .and_then(|count| count.checked_mul(std::mem::size_of::<Gles2TerrainVertex>()))
            .ok_or("terrain payload vertex byte count overflow")?;
        let block_entities = self.block_entities.capacity()
            .checked_mul(std::mem::size_of::<([i32; 3], u32)>())
            .ok_or("terrain payload block-entity byte count overflow")?;
        let lights = self.lights.capacity()
            .checked_mul(std::mem::size_of::<([i32; 3], u32)>())
            .ok_or("terrain payload light byte count overflow")?;
        let sprite_layers = self.sprite_layers.capacity()
            .checked_mul(std::mem::size_of::<u16>())
            .ok_or("terrain payload sprite-layer byte count overflow")?;
        // Count the complete inline payload, including Vec headers, Sides,
        // bounds, key and flags. Counting individual fields missed the Vec
        // descriptors and understated the per-section queue footprint.
        [
            vertices,
            block_entities,
            lights,
            sprite_layers,
            std::mem::size_of::<Gles2SectionPayload>(),
        ]
        .into_iter()
        .try_fold(0usize, |total, bytes| total.checked_add(bytes))
        .ok_or("terrain payload total byte count overflow")
    }
}

/// Convert a real Horizon section into an owned CPU payload. The source
/// SectionMesh remains borrowed so Horizon's streaming owner can release its
/// meshing allocation through its existing free(spent) path after upload.
/// Metadata is copied into the payload because the render owner outlives that
/// source allocation; this temporary duplication must be included in budgeting.
pub fn convert_section(
    key: (i32, i32, i32),
    mesh: &SectionMesh,
    atlas: &AtlasGrid,
    uv_step: f32,
) -> Result<Gles2SectionPayload, &'static str> {
    let geometry = convert_mesh_with_uv_step(mesh, atlas, uv_step)?;

    let mut block_entities = Vec::new();
    block_entities.try_reserve_exact(mesh.block_entities.len())
        .map_err(|_| "not enough memory to copy Horizon block-entity metadata")?;
    block_entities.extend_from_slice(&mesh.block_entities);

    let mut lights = Vec::new();
    lights.try_reserve_exact(mesh.lights.len())
        .map_err(|_| "not enough memory to copy Horizon light metadata")?;
    lights.extend_from_slice(&mesh.lights);

    let mut sprite_layers = Vec::new();
    sprite_layers.try_reserve_exact(mesh.sprite_layers.len())
        .map_err(|_| "not enough memory to copy Horizon sprite-layer metadata")?;
    sprite_layers.extend_from_slice(&mesh.sprite_layers);

    Ok(Gles2SectionPayload {
        key,
        geometry,
        visibility: mesh.visibility,
        block_entities,
        lights,
        sprite_layers,
        bounds: mesh.bounds.map(|bounds| [bounds[0].to_array(), bounds[1].to_array()]),
        keep: mesh.keep,
    })
}

/// A byte-bounded FIFO of owned section updates waiting for the GLES2 owner.
///
/// Enqueue coalesces pending updates by section key: the newest payload replaces
/// the older one and moves to the back of the queue. The budget accounts for
/// reserved vector capacity and inline metadata, not allocator bookkeeping or GPU memory.
pub struct Gles2SectionQueue {
    pending: VecDeque<Gles2SectionPayload>,
    max_bytes: usize,
    queued_bytes: usize,
}

impl Gles2SectionQueue {
    pub fn new(max_bytes: usize) -> Self {
        Self { pending: VecDeque::new(), max_bytes, queued_bytes: 0 }
    }

    pub fn len(&self) -> usize { self.pending.len() }
    pub fn is_empty(&self) -> bool { self.pending.is_empty() }
    pub fn queued_bytes(&self) -> usize { self.queued_bytes }
    pub fn max_bytes(&self) -> usize { self.max_bytes }

    /// Queue a payload, returning the replaced payload on success.
    /// On rejection, returns the incoming payload and leaves the queue unchanged.
    pub fn enqueue(
        &mut self,
        payload: Gles2SectionPayload,
    ) -> Result<Option<Gles2SectionPayload>, Gles2SectionPayload> {
        let incoming_bytes = match payload.byte_len() {
            Ok(bytes) if bytes <= self.max_bytes => bytes,
            _ => return Err(payload),
        };
        let existing = self.pending.iter().position(|item| item.key == payload.key);
        let old_bytes = match existing {
            Some(index) => match self.pending[index].byte_len() {
                Ok(bytes) => bytes,
                Err(_) => return Err(payload),
            },
            None => 0,
        };
        let base_bytes = match self.queued_bytes.checked_sub(old_bytes) {
            Some(bytes) => bytes,
            None => return Err(payload),
        };
        let next_bytes = match base_bytes.checked_add(incoming_bytes) {
            Some(bytes) if bytes <= self.max_bytes => bytes,
            _ => return Err(payload),
        };
        if existing.is_none() && self.pending.try_reserve(1).is_err() {
            return Err(payload);
        }

        let replaced = existing.and_then(|index| self.pending.remove(index));
        self.queued_bytes = next_bytes;
        self.pending.push_back(payload);
        Ok(replaced)
    }

    pub fn pop_front(&mut self) -> Option<Gles2SectionPayload> {
        let payload = self.pending.pop_front()?;
        let bytes = payload.byte_len().expect("queued payload byte count was validated");
        self.queued_bytes = self.queued_bytes.checked_sub(bytes)
            .expect("queued byte accounting must not underflow");
        Some(payload)
    }

    pub fn remove_section(&mut self, key: (i32, i32, i32)) -> Option<Gles2SectionPayload> {
        let index = self.pending.iter().position(|item| item.key == key)?;
        let payload = self.pending.remove(index)?;
        let bytes = payload.byte_len().expect("queued payload byte count was validated");
        self.queued_bytes = self.queued_bytes.checked_sub(bytes)
            .expect("queued byte accounting must not underflow");
        Some(payload)
    }

    /// Remove all pending sections in a horizontal chunk column (x, z).
    pub fn remove_columns(&mut self, columns: &[(i32, i32)]) -> usize {
        let mut removed = 0usize;
        let mut retained_bytes = 0usize;
        self.pending.retain(|payload| {
            if columns.iter().any(|&(x, z)| payload.key.0 == x && payload.key.2 == z) {
                removed += 1;
                false
            } else {
                retained_bytes = retained_bytes.checked_add(
                    payload.byte_len().expect("queued payload byte count was validated")
                ).expect("retained queue bytes cannot exceed the previous budget");
                true
            }
        });
        self.queued_bytes = retained_bytes;
        removed
    }

    pub fn clear(&mut self) {
        self.pending.clear();
        self.queued_bytes = 0;
    }
}

/// Horizon's Sides reports ranges in four-vertex quad units; GLES2 stores
/// six triangle vertices per quad. Reject arithmetic overflow before packing.
fn expand_solid_quad_range(first_quad: u32, quad_count: u32) -> Result<(u32, u32), &'static str> {
    let first = first_quad.checked_mul(6)
        .ok_or("visible Horizon solid range start overflows triangle vertices")?;
    let count = quad_count.checked_mul(6)
        .ok_or("visible Horizon solid range length overflows triangle vertices")?;
    first.checked_add(count)
        .ok_or("visible Horizon solid range end overflows triangle vertices")?;
    Ok((first, count))
}

/// Return visible solid draw ranges in GLES2 triangle-vertex units.
pub fn visible_solid_triangle_ranges(
    sides: &Sides,
    eye: [f32; 3],
) -> Result<Vec<(u32, u32)>, &'static str> {
    let mut ranges = Vec::new();
    ranges.try_reserve(sides.quads() as usize)
        .map_err(|_| "not enough memory to allocate visible Horizon solid ranges")?;
    let mut overflow = false;
    sides.visible(eye, |first_quad, quad_count| {
        match expand_solid_quad_range(first_quad, quad_count) {
            Ok(range) => ranges.push(range),
            Err(_) => overflow = true,
        }
    });
    if overflow {
        return Err("visible Horizon solid range exceeds GLES2 triangle-list limits");
    }
    Ok(ranges)
}

/// Pack visible triangle ranges as [first, count, first, count, ...] for the
/// native GLES2 upload boundary. Values are in expanded GL_TRIANGLES vertices.
pub fn visible_solid_triangle_ranges_packed(
    sides: &Sides,
    eye: [f32; 3],
) -> Result<Vec<u32>, &'static str> {
    let capacity = (sides.quads() as usize)
        .checked_mul(2)
        .ok_or("visible Horizon solid range capacity overflow")?;
    let mut packed = Vec::new();
    packed.try_reserve(capacity)
        .map_err(|_| "not enough memory to pack visible Horizon solid ranges")?;
    let mut overflow = false;
    sides.visible(eye, |first_quad, quad_count| {
        match expand_solid_quad_range(first_quad, quad_count) {
            Ok((first, count)) => {
                packed.push(first);
                packed.push(count);
            }
            Err(_) => overflow = true,
        }
    });
    if overflow {
        return Err("visible Horizon solid range exceeds GLES2 triangle-list limits");
    }
    Ok(packed)
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
    let quad_count = vertices.len() / 6;
    let mut quads = Vec::new();
    quads.try_reserve_exact(quad_count)
        .map_err(|_| "not enough memory to allocate translucent terrain sort keys")?;
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
    // Avoid the stable sort's hidden scratch allocation on the 1 GiB target.
    // The original quad index preserves stable ordering for equal distances.
    quads.sort_unstable_by(|left, right| {
        right.1.total_cmp(&left.1).then_with(|| left.0.cmp(&right.0))
    });
    let mut sorted = Vec::new();
    sorted.try_reserve_exact(vertices.len())
        .map_err(|_| "not enough memory to reorder translucent terrain quads")?;
    for (source, _) in quads {
        sorted.extend_from_slice(&vertices[source * 6..source * 6 + 6]);
    }
    vertices.swap_with_slice(&mut sorted);
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::mesher::TerrainVertex;


    #[test]
    fn packed_solid_ranges_match_native_triangle_vertex_units() {
        let packed = visible_solid_triangle_ranges_packed(&Sides::unsorted(3), [0.0; 3]).unwrap();
        assert_eq!(packed, vec![0, 18]);
    }

    #[test]
    fn solid_quad_range_expansion_rejects_u32_overflow() {
        assert_eq!(expand_solid_quad_range(3, 2).unwrap(), (18, 12));
        assert!(expand_solid_quad_range(u32::MAX, 1).is_err());
        assert!(expand_solid_quad_range(1, u32::MAX).is_err());
        assert!(expand_solid_quad_range(u32::MAX / 6, 1).is_err());
    }

    fn test_payload(key: (i32, i32, i32), vertex_count: usize) -> Gles2SectionPayload {
        Gles2SectionPayload {
            key,
            geometry: Gles2TerrainMesh {
                solid: vec![Gles2TerrainVertex {
                    position: [0.0; 3], uv: [0.0; 2], atlas_origin: [0.0; 2],
                    atlas_scale: [1.0; 2], color: [255; 4], emission: 0,
                    material_flags: 0, light: [0; 2], opaque_layer: 0.0,
                }; vertex_count],
                ..Default::default()
            },
            visibility: 0,
            block_entities: Vec::new(),
            lights: Vec::new(),
            sprite_layers: Vec::new(),
            bounds: None,
            keep: false,
        }
    }

    #[test]
    fn queue_replaces_pending_section_and_moves_latest_update_to_back() {
        let first = test_payload((1, 2, 3), 1);
        let second = test_payload((4, 5, 6), 1);
        let replacement = test_payload((1, 2, 3), 2);
        let mut queue = Gles2SectionQueue::new(4096);
        assert!(matches!(queue.enqueue(first), Ok(None)));
        assert!(matches!(queue.enqueue(second), Ok(None)));
        let replaced = match queue.enqueue(replacement) { Ok(Some(payload)) => payload, _ => panic!("expected replaced section") };
        assert_eq!(replaced.key, (1, 2, 3));
        assert_eq!(queue.len(), 2);
        assert_eq!(queue.pop_front().map(|payload| payload.key), Some((4, 5, 6)));
        assert_eq!(queue.pop_front().map(|payload| payload.key), Some((1, 2, 3)));
        assert!(queue.is_empty());
        assert_eq!(queue.queued_bytes(), 0);
    }

    #[test]
    fn queue_rejects_over_budget_payload_without_dropping_existing_update() {
        let existing = test_payload((1, 2, 3), 1);
        let bytes = existing.byte_len().unwrap();
        let mut queue = Gles2SectionQueue::new(bytes);
        assert!(matches!(queue.enqueue(existing), Ok(None)));
        let rejected = match queue.enqueue(test_payload((4, 5, 6), 1)) { Err(payload) => payload, Ok(_) => panic!("expected budget rejection") };
        assert_eq!(rejected.key, (4, 5, 6));
        assert_eq!(queue.len(), 1);
        assert_eq!(queue.queued_bytes(), bytes);
        assert_eq!(queue.pop_front().map(|payload| payload.key), Some((1, 2, 3)));
    }

    #[test]
    fn queue_replacement_budget_is_atomic_when_new_payload_is_too_large() {
        let existing = test_payload((1, 2, 3), 1);
        let bytes = existing.byte_len().unwrap();
        let mut queue = Gles2SectionQueue::new(bytes);
        assert!(matches!(queue.enqueue(existing), Ok(None)));
        let rejected = match queue.enqueue(test_payload((1, 2, 3), 2)) { Err(payload) => payload, Ok(_) => panic!("expected replacement rejection") };
        assert_eq!(rejected.key, (1, 2, 3));
        assert_eq!(queue.len(), 1);
        assert_eq!(queue.queued_bytes(), bytes);
        assert_eq!(queue.pop_front().map(|payload| payload.geometry.solid.len()), Some(1));
    }

    #[test]
    fn queue_removes_unloaded_sections_and_horizontal_columns() {
        let mut queue = Gles2SectionQueue::new(8192);
        assert!(matches!(queue.enqueue(test_payload((1, 0, 2), 1)), Ok(None)));
        assert!(matches!(queue.enqueue(test_payload((1, 1, 2), 1)), Ok(None)));
        assert!(matches!(queue.enqueue(test_payload((3, 0, 4), 1)), Ok(None)));
        assert_eq!(queue.remove_section((3, 0, 4)).map(|payload| payload.key), Some((3, 0, 4)));
        assert_eq!(queue.remove_columns(&[(1, 2)]), 2);
        assert!(queue.is_empty());
        assert_eq!(queue.queued_bytes(), 0);
    }

    #[test]
    fn queue_accounts_bytes_after_pop_remove_and_clear() {
        let mut queue = Gles2SectionQueue::new(8192);
        assert!(matches!(queue.enqueue(test_payload((1, 0, 1), 1)), Ok(None)));
        assert!(matches!(queue.enqueue(test_payload((2, 0, 2), 2)), Ok(None)));
        assert!(queue.queued_bytes() > 0);
        let _ = queue.pop_front();
        assert!(queue.queued_bytes() > 0);
        queue.clear();
        assert_eq!(queue.len(), 0);
        assert_eq!(queue.queued_bytes(), 0);
    }

    #[test]
    fn payload_budget_includes_inline_payload_and_vector_descriptors() {
        let payload = test_payload((7, 0, 7), 0);
        assert!(payload.byte_len().unwrap() >= std::mem::size_of::<Gles2SectionPayload>());
    }

    #[test]
    fn payload_budget_counts_reserved_capacity_not_only_live_vertices() {
        let mut payload = test_payload((9, 0, 9), 1);
        let live_bytes = payload.byte_len().unwrap();
        payload.geometry.solid.reserve(256);
        let reserved_bytes = payload.byte_len().unwrap();
        assert!(reserved_bytes > live_bytes);

        let mut queue = Gles2SectionQueue::new(live_bytes);
        let rejected = match queue.enqueue(payload) {
            Err(payload) => payload,
            Ok(_) => panic!("queue must reject a payload whose reserved buffers exceed its budget"),
        };
        assert!(rejected.byte_len().unwrap() > queue.max_bytes());
        assert!(queue.is_empty());
        assert_eq!(queue.queued_bytes(), 0);
    }

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
    fn translucent_sort_keeps_original_order_for_equal_distances() {
        let make_quad = |x: f32| {
            let vertex = Gles2TerrainVertex {
                position: [x, 0.0, 0.0], uv: [0.0; 2], atlas_origin: [0.0; 2],
                atlas_scale: [1.0; 2], color: [255; 4], emission: 0,
                material_flags: 0, light: [0; 2], opaque_layer: 0.0,
            };
            [vertex; 6]
        };
        let mut vertices = make_quad(-1.0).into_iter()
            .chain(make_quad(1.0)).collect::<Vec<_>>();
        sort_translucent_back_to_front(&mut vertices, [0.0; 3], [0.0; 3]).unwrap();
        assert!(vertices[..6].iter().all(|vertex| vertex.position[0] == -1.0));
        assert!(vertices[6..].iter().all(|vertex| vertex.position[0] == 1.0));
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
    fn section_payload_preserves_horizon_identity_and_non_geometry_metadata() {
        let atlas = AtlasGrid::new(16, 1, 4096).unwrap();
        let mesh = SectionMesh {
            visibility: 0x1234,
            block_entities: vec![([1, 2, 3], 44)],
            lights: vec![([4, 5, 6], 55)],
            sprite_layers: vec![0],
            bounds: Some([
                glam::Vec3::new(1.0, 2.0, 3.0),
                glam::Vec3::new(4.0, 5.0, 6.0),
            ]),
            keep: true,
            ..Default::default()
        };
        let payload = convert_section((7, 8, 9), &mesh, &atlas, 1.0 / crate::mesher::UV_STEPS).unwrap();
        assert_eq!(payload.key, (7, 8, 9));
        assert_eq!(payload.visibility, 0x1234);
        assert_eq!(payload.block_entities, vec![([1, 2, 3], 44)]);
        assert_eq!(payload.lights, vec![([4, 5, 6], 55)]);
        assert_eq!(payload.sprite_layers, vec![0]);
        assert_eq!(payload.bounds, Some([[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]));
        assert!(payload.keep);
        assert!(payload.byte_len().unwrap() >= 64);
    }

    #[test]
    fn converts_horizon_visible_quad_ranges_to_triangle_vertex_ranges() {
        let sides = Sides::unsorted(3);
        let ranges = visible_solid_triangle_ranges(&sides, [0.0, 0.0, 0.0]).unwrap();
        assert_eq!(ranges, vec![(0, 18)]);
        let sides = Sides::default();
        assert!(visible_solid_triangle_ranges(&sides, [0.0; 3]).unwrap().is_empty());
    }

    #[test]
    fn replaces_stale_side_ranges_when_solid_quad_count_changes() {
        let atlas = AtlasGrid::new(16, 1, 4096).unwrap();
        let vertex = TerrainVertex::new([0.0; 3], [0.0; 2], 0, u32::MAX, 0);
        let mesh = SectionMesh {
            solid: vec![vertex; 8],
            sides: Sides::unsorted(1),
            ..Default::default()
        };
        let converted = convert_mesh(&mesh, &atlas).unwrap();
        assert_eq!(converted.sides, Sides::unsorted(2));
        assert_eq!(visible_solid_triangle_ranges(&converted.sides, [0.0; 3]).unwrap(), vec![(0, 12)]);
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
        assert_eq!((atlas.cell_size, atlas.width, atlas.height), (18, 324, 306));
        assert_eq!(atlas.tile(0).unwrap().0, [1.0 / 324.0, 1.0 / 306.0]);
        assert_eq!(atlas.tile(17).unwrap().0, [307.0 / 324.0, 1.0 / 306.0]);
        assert_eq!(atlas.tile(18).unwrap().0, [1.0 / 324.0, 19.0 / 306.0]);
        assert_eq!(atlas.tile(0).unwrap().1, [16.0 / 324.0, 16.0 / 306.0]);
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
        assert_eq!((atlas.cell_size, atlas.width, atlas.height), (4, 8, 8));
        assert_eq!(packed.len(), 8 * 8 * 4);
        let pixel = |x: usize, y: usize| &packed[(y * 8 + x) * 4..(y * 8 + x + 1) * 4];
        assert_eq!(pixel(1, 1), &[1, 1, 1, 255]);
        assert_eq!(pixel(0, 0), &[1, 1, 1, 255]);
        assert_eq!(pixel(3, 3), &[1, 1, 1, 255]);
        assert_eq!(pixel(5, 1), &[2, 2, 2, 255]);
        assert_eq!(pixel(4, 0), &[2, 2, 2, 255]);
        assert_eq!(pixel(1, 5), &[3, 3, 3, 255]);
        assert_eq!(pixel(0, 7), &[3, 3, 3, 255]);
        assert!(atlas.pack_rgba(&pixels[..pixels.len() - 1]).is_err());
    }

    #[test]
    fn duplicates_each_sprite_edge_into_its_filtering_gutter() {
        let atlas = AtlasGrid::new(2, 1, 8).unwrap();
        let pixels = [
            10, 0, 0, 255, 20, 0, 0, 255,
            30, 0, 0, 255, 40, 0, 0, 255,
        ];
        let packed = atlas.pack_rgba(&pixels).unwrap();
        let pixel = |x: usize, y: usize| &packed[(y * atlas.width as usize + x) * 4..][..4];
        assert_eq!(pixel(0, 0), &[10, 0, 0, 255]);
        assert_eq!(pixel(1, 0), &[10, 0, 0, 255]);
        assert_eq!(pixel(2, 0), &[20, 0, 0, 255]);
        assert_eq!(pixel(3, 0), &[20, 0, 0, 255]);
        assert_eq!(pixel(0, 3), &[30, 0, 0, 255]);
        assert_eq!(pixel(1, 3), &[30, 0, 0, 255]);
        assert_eq!(pixel(2, 3), &[40, 0, 0, 255]);
        assert_eq!(pixel(3, 3), &[40, 0, 0, 255]);
        assert_eq!(atlas.tile(0).unwrap().0, [1.0 / 4.0, 1.0 / 4.0]);
        assert_eq!(atlas.tile(0).unwrap().1, [2.0 / 4.0, 2.0 / 4.0]);
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
        // Layer 256 occupies column 4, row 14; origin excludes the duplicated gutter.
        assert_eq!(converted.atlas_origin, [73.0 / 324.0, 253.0 / 306.0]);
        assert_eq!(converted.atlas_scale, [16.0 / 324.0, 16.0 / 306.0]);
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
