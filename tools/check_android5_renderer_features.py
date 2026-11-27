#!/usr/bin/env python3
"""Verify Game uses backend-neutral feature queries and timing controls."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_renderer_features.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
api = (root / "crates/horizon-render-api/src/lib.rs").read_text(encoding="utf-8")
gfx = (root / "crates/horizon-client-render/src/gfx.rs").read_text(encoding="utf-8")
game = (root / "crates/horizon-client/src/game.rs").read_text(encoding="utf-8")

api_methods = (
    "fn native_shadow(&self) -> bool;",
    "fn wants_water_plane(&self) -> bool;",
    "fn display_changed(&mut self) -> bool;",
    "fn invalidate_menu_blur(&mut self);",
    "fn has_cached_menu_blur(&self, radius: f32) -> bool;",
    "fn set_gpu_timing(&mut self, enabled: bool);",
)
for marker in api_methods:
    if api.count(marker) != 1:
        raise SystemExit(f"FAIL: backend API declaration missing or duplicated: {marker}")

wgpu_methods = (
    "Renderer::native_shadow(self)",
    "Renderer::wants_water_plane(self)",
    "Renderer::display_changed(self)",
    "Renderer::invalidate_menu_blur(self)",
    "Renderer::has_cached_menu_blur(self, radius)",
    "Renderer::set_detailed_gpu_timing(self, enabled)",
)
for marker in wgpu_methods:
    if gfx.count(marker) != 1:
        raise SystemExit(f"FAIL: wgpu adapter missing or duplicated: {marker}")

game_calls = (
    "RenderBackend::native_shadow(r)",
    "RenderBackend::wants_water_plane(r)",
    "RenderBackend::display_changed(r)",
    "RenderBackend::invalidate_menu_blur(renderer)",
    "RenderBackend::has_cached_menu_blur(renderer, r)",
    "RenderBackend::set_gpu_timing(renderer,",
)
for marker in game_calls:
    if marker not in game:
        raise SystemExit(f"FAIL: Game does not use neutral feature API: {marker}")

for stale in (
    "r.native_shadow()",
    "r.wants_water_plane()",
    "r.display_changed()",
    "renderer.invalidate_menu_blur()",
    "renderer.has_cached_menu_blur(r)",
    "renderer.set_detailed_gpu_timing(",
):
    if stale in game:
        raise SystemExit(f"FAIL: concrete Game renderer call remains: {stale}")

for forbidden in ("wgpu::", "winit::", "EGLDisplay", "EGLContext", "GLuint"):
    if forbidden in api:
        raise SystemExit(f"FAIL: backend-neutral API leaks implementation type: {forbidden}")

print("PASS: renderer feature queries and detailed GPU timing are behind RenderBackend.")
print("PASS: wgpu behavior is isolated in its adapter; Game has no concrete calls for this feature set.")
