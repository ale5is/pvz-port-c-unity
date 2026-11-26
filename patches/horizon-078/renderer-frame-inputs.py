#!/usr/bin/env python3
"""Move CPU-only render inputs out of wgpu implementation modules."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
render = root / "crates/horizon-client-render/src"
frame_path = render / "frame.rs"
pack_draw_path = render / "gfx/pack_draw.rs"
lightning_path = render / "gfx/lightning.rs"
pack_uniforms_path = render / "gfx/pack_uniforms.rs"

frame = frame_path.read_text(encoding="utf-8")
pack_draw = pack_draw_path.read_text(encoding="utf-8")
lightning = lightning_path.read_text(encoding="utf-8")
pack_uniforms = pack_uniforms_path.read_text(encoding="utf-8")

if "pub struct LightningInput<'a>" in frame:
    required = ("pub struct EntitySpan", "pub struct LightningBolt", "pub struct LightningInput<'a>", "pub struct PackInputs")
    missing = [item for item in required if item not in frame]
    if missing:
        raise SystemExit(f"Partial CPU input extraction detected: {missing}")
    if "crate::gfx::" in frame or "wgpu::" in frame:
        raise SystemExit("CPU frame still depends on wgpu implementation modules")
    print("CPU frame inputs are already separated from wgpu implementation modules.")
    raise SystemExit(0)

def extract(source: str, start: str, end: str, label: str) -> tuple[str, str]:
    if source.count(start) != 1 or source.count(end) != 1:
        raise SystemExit(f"Could not uniquely locate {label} boundaries")
    first = source.index(start)
    last = source.index(end, first)
    if last <= first:
        raise SystemExit(f"Invalid {label} boundaries")
    return source[:first] + source[last:], source[first:last]

pack_draw, entity_span = extract(
    pack_draw,
    "#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]\npub struct EntitySpan {",
    "\n/// HzDraw size:",
    "EntitySpan",
)
lightning, lightning_inputs = extract(
    lightning,
    "/// A bolt's render state. Simulation lifetime and flashes stay in the game.\n#[derive(Clone, Copy)]\npub struct Bolt {",
    "\n/// The atlas point an untextured quad",
    "lightning input",
)
pack_uniforms, pack_inputs = extract(
    pack_uniforms,
    "/// What the game passes for the pack's uniforms, beyond what Frame carries.\n#[derive(Clone, Copy, Debug, Default)]\npub struct PackInputs {",
    "\n/// One uniform's value:",
    "PackInputs",
)

lightning_inputs = lightning_inputs.replace("pub struct Bolt {", "pub struct LightningBolt {")
lightning_inputs = lightning_inputs.replace("pub struct Input<'a> {", "pub struct LightningInput<'a> {")
lightning_inputs = lightning_inputs.replace("pub bolts: &'a [Bolt],", "pub bolts: &'a [LightningBolt],")
if "pub struct LightningBolt" not in lightning_inputs or "pub struct LightningInput<'a>" not in lightning_inputs:
    raise SystemExit("Failed to rename extracted lightning CPU input types")

for old, new in (
    ("pub entity_spans: &'a [crate::gfx::pack_draw::EntitySpan],", "pub entity_spans: &'a [EntitySpan],"),
    ("pub lightning: crate::gfx::lightning::Input<'a>,", "pub lightning: LightningInput<'a>,"),
    ("pub pack: crate::gfx::pack_uniforms::PackInputs,", "pub pack: PackInputs,"),
):
    if frame.count(old) != 1:
        raise SystemExit(f"Could not uniquely migrate Frame field: {old}")
    frame = frame.replace(old, new)

if pack_draw.count("use horizon_shaders::{RENDER_STAGES, render_stage};") != 1:
    raise SystemExit("Could not locate pack_draw imports")
pack_draw = pack_draw.replace(
    "use horizon_shaders::{RENDER_STAGES, render_stage};",
    "use horizon_shaders::{RENDER_STAGES, render_stage};\npub use crate::frame::EntitySpan;",
)
if lightning.count("use wgpu::util::DeviceExt;") != 1:
    raise SystemExit("Could not locate lightning imports")
lightning = lightning.replace(
    "use wgpu::util::DeviceExt;",
    "use wgpu::util::DeviceExt;\npub use crate::frame::{LightningBolt as Bolt, LightningInput as Input};",
)
if pack_uniforms.count("use super::Frame;") != 1:
    raise SystemExit("Could not locate pack_uniforms imports")
pack_uniforms = pack_uniforms.replace(
    "use super::Frame;",
    "use super::Frame;\npub use crate::frame::PackInputs;",
)

insert_anchor = "use crate::mesher::Vertex;\n\n"
if frame.count(insert_anchor) != 1:
    raise SystemExit("Could not locate frame.rs CPU type insertion point")
cpu_inputs = entity_span.strip() + "\n\n" + lightning_inputs.strip() + "\n\n" + pack_inputs.strip() + "\n\n"
frame = frame.replace(insert_anchor, insert_anchor + cpu_inputs)
if "crate::gfx::" in frame or "wgpu::" in frame:
    raise SystemExit("CPU frame module still depends on wgpu renderer implementation modules")

frame_path.write_text(frame, encoding="utf-8")
pack_draw_path.write_text(pack_draw, encoding="utf-8")
lightning_path.write_text(lightning, encoding="utf-8")
pack_uniforms_path.write_text(pack_uniforms, encoding="utf-8")
print("Moved EntitySpan, lightning inputs and PackInputs into the CPU frame module.")
