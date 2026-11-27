#!/usr/bin/env python3
"""Verify that frame submission and terrain streaming form one backend boundary."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_unified_backend.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
api = (root / "crates/horizon-render-api/src/lib.rs").read_text(encoding="utf-8")
gfx = (root / "crates/horizon-client-render/src/gfx.rs").read_text(encoding="utf-8")

required_api = (
    "pub trait RenderBackend<Frame>",
    "pub trait TerrainStreamBackend<Mesh>",
    "pub trait FrameFamily",
    "type Frame<'frame>;",
    "pub trait GameRenderBackend<Frames: FrameFamily, Mesh>",
    "for<'frame> RenderBackend<Frames::Frame<'frame>> + TerrainStreamBackend<Mesh>",
    "pub type GameBackendHandle<'a, Frames, Mesh>",
    "game_backend_object_dispatches_borrowed_frames_and_terrain",
    "super::TerrainStreamBackend::upload_section(backend, (0, 0, 0), mesh)",
)
missing = [item for item in required_api if item not in api]
if missing:
    raise SystemExit(f"FAIL: unified backend contract is incomplete: {missing}")

required_wgpu = (
    "impl<'frame> horizon_render_api::RenderBackend<Frame<'frame>> for Renderer",
    "impl horizon_render_api::TerrainStreamBackend<SectionMesh> for Renderer",
    "pub struct HorizonFrameFamily;",
    "type Frame<'frame> = Frame<'frame>;",
    "pub fn as_game_backend_handle<'a>(",
    "horizon_render_api::GameBackendHandle<'a, HorizonFrameFamily, SectionMesh>",
)
missing = [item for item in required_wgpu if item not in gfx]
if missing:
    raise SystemExit(f"FAIL: wgpu adapter does not satisfy both backend contracts: {missing}")

for forbidden in ("wgpu::", "winit::", "EGLDisplay", "EGLContext", "GLuint"):
    if forbidden in api:
        raise SystemExit(f"FAIL: unified API leaks platform/GPU type: {forbidden}")

print("PASS: one object-safe game backend composes borrowed frame submission and terrain streaming.")
print("PASS: existing wgpu Renderer implements both component contracts.")
print("PASS: the API remains independent of wgpu, EGL and window handles.")
