#!/usr/bin/env python3
"""Install the safe CPU-side GLES2 adapter into the pinned Horizon 0.7.8 tree."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
lib = root / "crates/horizon-client-render/src/lib.rs"
adapter = root / "crates/horizon-client-render/src/gles2_hud.rs"

module = "pub mod gles2_hud;\n"
source = lib.read_text(encoding="utf-8")
if source.count(module) == 0:
    anchor = "pub mod gfx;\n"
    if source.count(anchor) != 1:
        raise SystemExit("Could not uniquely locate horizon-client-render module list")
    lib.write_text(source.replace(anchor, anchor + module), encoding="utf-8")
elif source.count(module) != 1:
    raise SystemExit("Unexpected duplicate gles2_hud module declaration")

adapter_source = r'''//! Safe CPU adapter from Horizon's native HUD batch to a GLES2-friendly vertex stream.
//!
//! This module deliberately contains no OpenGL calls and no unsafe code. It preserves
//! draw order, blend/depth runs and glyph patches so a separate GLES2 backend can consume
//! the exact frame data without depending on wgpu types.
use crate::{
    font::Patch,
    hud::{Blend, EndPortalFill, HudAtlas, HudBatch},
};

/// GLES2 vertex layout: position/UV/depth are floats and color is normalized RGBA bytes.
/// The text flag is a float because GLES2 has no integer vertex attributes.
#[repr(C)]
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Gles2HudVertex {
    pub position: [f32; 2],
    pub uv: [f32; 2],
    pub color: [u8; 4],
    pub depth: f32,
    pub glint_uv: [f32; 2],
    pub text: f32,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum Gles2Blend {
    Alpha,
    Invert,
    Additive,
    Depth { scissor_top_left: [u32; 4] },
    Vignette,
    EndPortal,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Gles2DrawRun {
    pub first_vertex: u32,
    pub vertex_count: u32,
    pub blend: Gles2Blend,
}

pub struct Gles2HudFrame<'a> {
    pub width: f32,
    pub height: f32,
    pub scale: f32,
    pub glint_alpha: f32,
    pub game_time: f32,
    pub blur_at: u32,
    pub end_portal: EndPortalFill,
    pub atlas_width: u32,
    pub atlas_height: u32,
    pub atlas_rgba: &'a [u8],
    pub glint_width: u32,
    pub glint_height: u32,
    pub glint_rgba: &'a [u8],
    pub vertices: Vec<Gles2HudVertex>,
    pub runs: Vec<Gles2DrawRun>,
    pub patches: &'a [Patch],
}

fn convert_blend(mode: Blend) -> Gles2Blend {
    match mode {
        Blend::Alpha => Gles2Blend::Alpha,
        Blend::Invert => Gles2Blend::Invert,
        Blend::Additive => Gles2Blend::Additive,
        Blend::Depth([x, y, w, h]) => Gles2Blend::Depth {
            scissor_top_left: [x, y, w, h],
        },
        Blend::Vignette => Gles2Blend::Vignette,
        Blend::EndPortal => Gles2Blend::EndPortal,
    }
}

/// Convert one Horizon HUD frame without changing vertex order or run boundaries.
/// Packed color is AABBGGRR; little-endian bytes are the RGBA attribute GLES2 expects.
pub fn prepare<'a>(batch: &'a HudBatch, atlas: &'a HudAtlas) -> Gles2HudFrame<'a> {
    let vertices = batch
        .vertices
        .iter()
        .map(|vertex| Gles2HudVertex {
            position: vertex.pos,
            uv: vertex.uv,
            color: vertex.color.to_le_bytes(),
            depth: vertex.depth,
            glint_uv: vertex.glint_uv,
            text: if vertex.text == 0 { 0.0 } else { 1.0 },
        })
        .collect();

    let runs = batch
        .runs()
        .into_iter()
        .map(|(range, mode)| Gles2DrawRun {
            first_vertex: range.start,
            vertex_count: range.end - range.start,
            blend: convert_blend(mode),
        })
        .collect();

    Gles2HudFrame {
        width: batch.width,
        height: batch.height,
        scale: batch.scale,
        glint_alpha: batch.glint_alpha,
        game_time: batch.game_time,
        blur_at: batch.blur_at,
        end_portal: batch.end_portal,
        atlas_width: atlas.width,
        atlas_height: atlas.height,
        atlas_rgba: &atlas.pixels,
        glint_width: atlas.glint.1,
        glint_height: atlas.glint.2,
        glint_rgba: &atlas.glint.0,
        vertices,
        runs,
        patches: &batch.patches,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::hud::{rgba, HudAtlas, HudVertex};

    fn vertex(color: u32) -> HudVertex {
        HudVertex {
            pos: [12.0, 24.0],
            uv: [0.25, 0.75],
            color,
            depth: 0.5,
            glint_uv: [0.125, 0.875],
            text: 1,
        }
    }

    #[test]
    fn converts_hud_vertex_to_gles2_attribute_layout() {
        let mut batch = HudBatch::new([1280, 720]);
        batch.vertices.push(vertex(rgba(0x11, 0x22, 0x33, 0x44)));
        let atlas = HudAtlas::boot();
        let frame = prepare(&batch, &atlas);
        assert_eq!(frame.vertices.len(), 1);
        assert_eq!([frame.atlas_width, frame.atlas_height], [1024, 1024]);
        assert_eq!(frame.atlas_rgba.len(), 1024 * 1024 * 4);
        assert_eq!(frame.glint_rgba.len(), (frame.glint_width * frame.glint_height * 4) as usize);
        assert_eq!(frame.vertices[0].position, [12.0, 24.0]);
        assert_eq!(frame.vertices[0].uv, [0.25, 0.75]);
        assert_eq!(frame.vertices[0].color, [0x11, 0x22, 0x33, 0x44]);
        assert_eq!(frame.vertices[0].depth, 0.5);
        assert_eq!(frame.vertices[0].glint_uv, [0.125, 0.875]);
        assert_eq!(frame.vertices[0].text, 1.0);
    }

    #[test]
    fn preserves_alpha_default_runs_and_additive_ranges() {
        let mut batch = HudBatch::new([1280, 720]);
        batch.vertices.push(vertex(u32::MAX));
        batch.vertices.push(vertex(u32::MAX));
        batch.blend_since(1, Blend::Additive);
        let atlas = HudAtlas::boot();
        let frame = prepare(&batch, &atlas);
        assert_eq!(frame.runs.len(), 2);
        assert_eq!(frame.runs[0], Gles2DrawRun {
            first_vertex: 0,
            vertex_count: 1,
            blend: Gles2Blend::Alpha,
        });
        assert_eq!(frame.runs[1], Gles2DrawRun {
            first_vertex: 1,
            vertex_count: 1,
            blend: Gles2Blend::Additive,
        });
    }

    #[test]
    fn maps_every_horizon_blend_variant_without_dropping_it() {
        assert_eq!(convert_blend(Blend::Alpha), Gles2Blend::Alpha);
        assert_eq!(convert_blend(Blend::Invert), Gles2Blend::Invert);
        assert_eq!(convert_blend(Blend::Additive), Gles2Blend::Additive);
        assert_eq!(convert_blend(Blend::Depth([1, 2, 3, 4])), Gles2Blend::Depth {
            scissor_top_left: [1, 2, 3, 4],
        });
        assert_eq!(convert_blend(Blend::Vignette), Gles2Blend::Vignette);
        assert_eq!(convert_blend(Blend::EndPortal), Gles2Blend::EndPortal);
    }

    #[test]
    fn preserves_depth_scissor_coordinates() {
        let mut batch = HudBatch::new([1280, 720]);
        batch.vertices.push(vertex(u32::MAX));
        batch.depth_since(0, [10.0, 20.0, 30.0, 40.0]);
        let atlas = HudAtlas::boot();
        let frame = prepare(&batch, &atlas);
        assert_eq!(frame.runs.len(), 1);
        assert_eq!(
            frame.runs[0].blend,
            Gles2Blend::Depth {
                scissor_top_left: [
                    (10.0 * batch.scale) as u32,
                    (20.0 * batch.scale) as u32,
                    (20.0 * batch.scale) as u32,
                    (20.0 * batch.scale) as u32,
                ],
            }
        );
    }
}
'''
if adapter.exists():
    existing = adapter.read_text(encoding="utf-8")
    if existing != adapter_source:
        raise SystemExit("gles2_hud.rs already exists with different content; refusing to overwrite")
else:
    adapter.write_text(adapter_source, encoding="utf-8")
print("Installed horizon-client-render::gles2_hud safe CPU adapter and unit tests.")
