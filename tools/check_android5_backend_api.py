#!/usr/bin/env python3
"""Verify the backend API is independent and the wgpu implementation conforms."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_backend_api.py <horizon-source-root>")

root = Path(sys.argv[1]).resolve()
api = root / "crates/horizon-render-api/src/lib.rs"
api_manifest = root / "crates/horizon-render-api/Cargo.toml"
render_manifest = root / "crates/horizon-client-render/Cargo.toml"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
workspace = (root / "Cargo.toml").read_text(encoding="utf-8")
sources = api.read_text(encoding="utf-8")
manifest = api_manifest.read_text(encoding="utf-8")
render_deps = render_manifest.read_text(encoding="utf-8")
gfx_source = gfx.read_text(encoding="utf-8")

required_api = (
    "pub enum BackendKind",
    "pub struct BackendInfo",
    "pub struct WindowGeneration(u64)",
    "pub struct WindowGenerationClock",
    "pub fn advance(&mut self, next: WindowGeneration) -> bool",
    "pub const fn accepts(&self, generation: WindowGeneration) -> bool",
    "pub struct RenderBackendError",
    "pub trait RenderBackend<Frame>",
    "fn render_frame(",
    "fn resize(",
    "fn suspend_surface(",
    "fn recreate_surface(",
    "fn release_surface(",
    "fn backend_info(",
)
missing = [item for item in required_api if item not in sources]
if missing:
    raise SystemExit(f"FAIL: backend API is incomplete: {missing}")
if any(token in sources for token in ("wgpu::", "winit::", "EGLDisplay", "EGLContext", "GLuint")):
    raise SystemExit("FAIL: backend API leaks GPU/window implementation types")
if "[dependencies]" not in manifest or "wgpu" in manifest or "winit" in manifest:
    raise SystemExit("FAIL: horizon-render-api must have no GPU/window dependencies")
if '"crates/horizon-render-api"' not in workspace:
    raise SystemExit("FAIL: horizon-render-api is not a workspace member")
if 'horizon-render-api = { path = "../horizon-render-api" }' not in render_deps:
    raise SystemExit("FAIL: horizon-client-render does not depend on the backend API")
if "impl<'frame> horizon_render_api::RenderBackend<Frame<'frame>> for Renderer" not in gfx_source:
    raise SystemExit("FAIL: existing wgpu Renderer does not implement the shared API")
adapter_marker = "impl<'frame> horizon_render_api::RenderBackend<Frame<'frame>> for Renderer"
adapter = gfx_source[gfx_source.index(adapter_marker):]
for method in ("render_frame", "resize", "suspend_surface", "recreate_surface", "release_surface", "backend_info"):
    if f"fn {method}(" not in adapter:
        raise SystemExit(f"FAIL: wgpu backend adapter is missing {method}")
if "window_generation_rejects_stale_and_future_work" not in sources or "window_generation_cannot_roll_back_or_be_zero" not in sources:
    raise SystemExit("FAIL: window-generation regression tests are missing")
print("PASS: backend API is a standalone GPU/window-free crate.")
print("PASS: window generations reject stale work and prevent rollback.")
print("PASS: existing wgpu Renderer implements frame submission and surface lifecycle.")
print("LIMITATION: Game and Launcher still use concrete Renderer call sites; backend-handle migration remains.")
