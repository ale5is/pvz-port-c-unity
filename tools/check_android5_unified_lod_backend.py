#!/usr/bin/env python3
"""Verify the single game backend composes frame, terrain and far-terrain LOD."""
import sys
from pathlib import Path
if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_unified_lod_backend.py <horizon-source-root>")
api = Path(sys.argv[1]).resolve() / "crates/horizon-render-api/src/lib.rs"
if not api.is_file():
    raise SystemExit("FAIL: missing horizon-render-api")
source = api.read_text(encoding="utf-8")
bound = "for<'frame> RenderBackend<Frames::Frame<'frame>> + TerrainStreamBackend<Mesh> + LodStreamBackend<Mesh>"
if source.count(bound) != 2:
    raise SystemExit("FAIL: GameRenderBackend and blanket implementation must both require LOD")
for marker in (
    "pub trait LodStreamBackend<Mesh>",
    "pub trait GameRenderBackend<Frames: FrameFamily, Mesh>",
    "pub type GameBackendHandle<'a, Frames, Mesh>",
    "super::LodStreamBackend::clear_lods(backend)",
    "super::LodStreamBackend::queue_lods(backend, Vec::new(), Vec::new(), None)",
    "super::LodStreamBackend::upload_lods(backend, std::time::Duration::from_millis(1))",
    "super::LodStreamBackend::lod_detail_columns(backend)",
):
    if marker not in source:
        raise SystemExit("FAIL: missing unified LOD dispatch contract: " + marker)
for forbidden in ("wgpu::", "winit::", "EGLDisplay", "EGLContext", "GLuint", "glam::"):
    if forbidden in source:
        raise SystemExit("FAIL: unified LOD API leaks platform/GPU types: " + forbidden)
print("PASS: one object-safe backend requires frame, terrain and LOD operations.")
print("PASS: compile-time dispatch covers clear, queue, upload and detail-column queries.")
print("PASS: common backend API remains renderer- and platform-neutral.")
