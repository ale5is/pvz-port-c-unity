#!/usr/bin/env python3
"""Verify Horizon's frame data contract is independent of the wgpu renderer."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_frame_contract.py <horizon-source-root>")

root = Path(sys.argv[1]) / "crates/horizon-client-render/src"
frame = (root / "frame.rs").read_text(encoding="utf-8")
gfx = (root / "gfx.rs").read_text(encoding="utf-8")
lib = (root / "lib.rs").read_text(encoding="utf-8")

required = (
    "pub struct Frame<'a>",
    "pub struct BorderDraw",
    "pub camera: DVec3",
    "use crate::mesher::Vertex;",
    "pub struct EntitySpan",
    "pub struct LightningBolt",
    "pub struct LightningInput<'a>",
    "pub struct PackInputs",
    "pub hud: &'a crate::hud::HudBatch",
    "pub entities: &'a [Vertex]",
    "pub post_effects: crate::post_chain::Request<'a>",
)
missing = [marker for marker in required if marker not in frame]
if missing:
    raise SystemExit(f"Frame contract is missing required CPU frame fields: {missing}")

if "wgpu::" in frame or "crate::gfx::" in frame:
    raise SystemExit("Frame contract still depends on wgpu implementation modules")
if "pub use crate::frame::{BorderDraw, Frame};" not in gfx:
    raise SystemExit("gfx.rs does not preserve the public Frame/BorderDraw compatibility re-export")
if "pub mod frame;" not in lib:
    raise SystemExit("lib.rs does not expose the backend-neutral frame contract module")
if "pub struct Frame<'a>" in gfx or "pub struct BorderDraw" in gfx:
    raise SystemExit("Frame or BorderDraw is still defined inside the wgpu renderer module")

print("PASS: Frame, BorderDraw, EntitySpan, lightning inputs and PackInputs are CPU-only types outside wgpu implementation modules.")
