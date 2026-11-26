#!/usr/bin/env python3
"""Move Horizon's CPU-only frame contract out of the wgpu renderer module."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
render = root / "crates/horizon-client-render/src"
gfx = render / "gfx.rs"
lib = render / "lib.rs"
frame = render / "frame.rs"

gfx_source = gfx.read_text(encoding="utf-8")
lib_source = lib.read_text(encoding="utf-8")

frame_export = "pub use crate::frame::{BorderDraw, Frame};\n"
frame_module = "pub mod frame;\n"

if frame_export in gfx_source:
    if not frame.exists() or "pub struct Frame<'a>" not in frame.read_text(encoding="utf-8"):
        raise SystemExit("Frame re-export exists but the extracted frame contract is missing")
    if frame_module not in lib_source:
        raise SystemExit("Frame re-export exists but lib.rs does not declare the frame module")
    print("Horizon CPU frame contract is already separated from gfx.rs.")
    raise SystemExit(0)

if frame.exists():
    raise SystemExit("frame.rs already exists without the expected gfx.rs re-export; refusing overwrite")

frame_start = gfx_source.find("pub struct Frame<'a> {")
border_start = gfx_source.find("/// One frame of the world border wall", frame_start)
border_end = gfx_source.find("\n#[repr(C)]", border_start)
if min(frame_start, border_start, border_end) < 0 or not (frame_start < border_start < border_end):
    raise SystemExit("Could not uniquely locate the Frame and BorderDraw CPU contract definitions")

definitions = gfx_source[frame_start:border_end]
for old, new in (
    ("pub sky: Sky,", "pub sky: crate::environment::Sky,"),
    ("pub hud: &'a HudBatch,", "pub hud: &'a crate::hud::HudBatch,"),
    ("pub lightning: lightning::Input<'a>,", "pub lightning: crate::gfx::lightning::Input<'a>,"),
    ("pub entity_spans: &'a [pack_draw::EntitySpan],", "pub entity_spans: &'a [crate::gfx::pack_draw::EntitySpan],"),
    ("pub pack: pack_uniforms::PackInputs,", "pub pack: crate::gfx::pack_uniforms::PackInputs,"),
):
    if definitions.count(old) != 1:
        raise SystemExit(f"Expected exactly one frame-contract type reference: {old}")
    definitions = definitions.replace(old, new)

frame_source = (
    "//! Backend-neutral CPU data submitted by Horizon's game loop for one rendered frame.\n"
    "//! This module deliberately contains no wgpu handles, surfaces, queues or pipelines.\n"
    "use glam::{DVec3, Mat4};\n"
    "use crate::mesher::Vertex;\n\n"
    + definitions
)
if "wgpu::" in frame_source:
    raise SystemExit("Extracted frame contract unexpectedly depends on wgpu")

gfx_source = gfx_source[:frame_start] + gfx_source[border_end:]
import_anchor = "use winit::window::Window;\n"
if gfx_source.count(import_anchor) != 1:
    raise SystemExit("Could not uniquely locate gfx.rs imports for the Frame re-export")
gfx_source = gfx_source.replace(import_anchor, import_anchor + frame_export)

if frame_module not in lib_source:
    anchor = "pub mod font;\n"
    if lib_source.count(anchor) != 1:
        raise SystemExit("Could not uniquely locate lib.rs module declarations")
    lib_source = lib_source.replace(anchor, anchor + frame_module)

frame.write_text(frame_source, encoding="utf-8")
gfx.write_text(gfx_source, encoding="utf-8")
lib.write_text(lib_source, encoding="utf-8")
print("Moved Frame and BorderDraw into the wgpu-independent frame contract module.")
