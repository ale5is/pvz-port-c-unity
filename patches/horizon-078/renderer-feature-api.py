#!/usr/bin/env python3
"""Move renderer feature queries and GPU timing controls behind RenderBackend."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
api = root / "crates/horizon-render-api/src/lib.rs"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
game = root / "crates/horizon-client/src/game.rs"
for path in (api, gfx, game):
    if not path.is_file():
        raise SystemExit(f"Missing renderer feature source: {path}")

a = api.read_text(encoding="utf-8")
g = gfx.read_text(encoding="utf-8")
m = game.read_text(encoding="utf-8")

trait_anchor = "    fn backend_info(&self) -> BackendInfo;"
methods = """    /// Whether the active backend has a native shader-pack shadow pass.
    fn native_shadow(&self) -> bool;
    /// Whether the active backend needs a water reflection plane.
    fn wants_water_plane(&self) -> bool;
    /// Report a display/adapter change that requires UI or pipeline refresh.
    fn display_changed(&mut self) -> bool;
    /// Invalidate any backend-owned cached menu backdrop.
    fn invalidate_menu_blur(&mut self);
    /// Whether a menu backdrop matching this blur radius is cached.
    fn has_cached_menu_blur(&self, radius: f32) -> bool;
    /// Enable or disable detailed GPU timing without exposing a device handle.
    fn set_gpu_timing(&mut self, enabled: bool);
"""
if "fn native_shadow(&self) -> bool;" not in a:
    if a.count(trait_anchor) != 1:
        raise SystemExit("Could not uniquely locate RenderBackend feature insertion point")
    a = a.replace(trait_anchor, methods + trait_anchor, 1)
else:
    for marker in ("fn wants_water_plane(&self) -> bool;", "fn display_changed(&mut self) -> bool;", "fn invalidate_menu_blur(&mut self);", "fn has_cached_menu_blur(&self, radius: f32) -> bool;", "fn set_gpu_timing(&mut self, enabled: bool);"):
        if marker not in a:
            raise SystemExit(f"Existing RenderBackend feature API is incomplete: {marker}")

impl_anchor = "    fn backend_info(&self) -> horizon_render_api::BackendInfo {"
impl_methods = """    fn native_shadow(&self) -> bool {
        Renderer::native_shadow(self)
    }

    fn wants_water_plane(&self) -> bool {
        Renderer::wants_water_plane(self)
    }

    fn display_changed(&mut self) -> bool {
        Renderer::display_changed(self)
    }

    fn invalidate_menu_blur(&mut self) {
        Renderer::invalidate_menu_blur(self);
    }

    fn has_cached_menu_blur(&self, radius: f32) -> bool {
        Renderer::has_cached_menu_blur(self, radius)
    }

    fn set_gpu_timing(&mut self, enabled: bool) {
        Renderer::set_detailed_gpu_timing(self, enabled);
    }

"""
trait_impl_anchor = "impl<'frame> horizon_render_api::RenderBackend<Frame<'frame>> for Renderer {"
trait_start = g.find(trait_impl_anchor)
if trait_start < 0:
    raise SystemExit("Could not locate exact wgpu RenderBackend implementation")
next_impl = g.find("\nimpl ", trait_start + len(trait_impl_anchor))
trait_end = next_impl if next_impl >= 0 else len(g)
trait_body = g[trait_start:trait_end]
if "fn native_shadow(&self) -> bool {" not in trait_body:
    if trait_body.count(impl_anchor) != 1:
        raise SystemExit("Could not uniquely locate backend_info inside wgpu RenderBackend implementation")
    g = g[:trait_start] + trait_body.replace(impl_anchor, impl_methods + impl_anchor, 1) + g[trait_end:]
else:
    for marker in ("fn wants_water_plane(&self) -> bool {", "fn display_changed(&mut self) -> bool {", "fn invalidate_menu_blur(&mut self) {", "fn has_cached_menu_blur(&self, radius: f32) -> bool {", "fn set_gpu_timing(&mut self, enabled: bool) {"):
        if marker not in trait_body:
            raise SystemExit(f"Existing wgpu feature adapter is incomplete inside RenderBackend: {marker}")

replacements = (
    ("r.native_shadow()", "horizon_render_api::RenderBackend::native_shadow(r)"),
    ("r.wants_water_plane()", "horizon_render_api::RenderBackend::wants_water_plane(r)"),
    ("r.display_changed()", "horizon_render_api::RenderBackend::display_changed(r)"),
    ("renderer.invalidate_menu_blur()", "horizon_render_api::RenderBackend::invalidate_menu_blur(renderer)"),
    ("renderer.has_cached_menu_blur(r)", "horizon_render_api::RenderBackend::has_cached_menu_blur(renderer, r)"),
    ("renderer.set_detailed_gpu_timing(", "horizon_render_api::RenderBackend::set_gpu_timing(renderer, "),
)
for old, new in replacements:
    if old not in m:
        raise SystemExit(f"Expected Game renderer feature call before migration: {old}")
    m = m.replace(old, new)

# The timing call is multiline and had one closing parenthesis for the method.
# UFCS adds no extra call nesting, so the original closing syntax remains valid.
for stale in ("r.native_shadow()", "r.wants_water_plane()", "r.display_changed()", "renderer.invalidate_menu_blur()", "renderer.has_cached_menu_blur(r)", "renderer.set_detailed_gpu_timing("):
    if stale in m:
        raise SystemExit(f"Game still calls concrete renderer feature API: {stale}")

for forbidden in ("wgpu::", "winit::", "EGLDisplay", "EGLContext", "GLuint"):
    if forbidden in a:
        raise SystemExit(f"RenderBackend feature API leaks platform/GPU type: {forbidden}")

api.write_text(a, encoding="utf-8")
gfx.write_text(g, encoding="utf-8")
game.write_text(m, encoding="utf-8")
print("Migrated native-shadow, water-reflection, display-change, menu-blur and GPU-timing calls to RenderBackend.")
