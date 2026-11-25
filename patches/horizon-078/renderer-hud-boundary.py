#!/usr/bin/env python3
"""Keep HUD setup and framebuffer sizing behind Renderer instead of exposing wgpu internals."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
gfx = root / "crates/horizon-client-render/src/gfx.rs"
game = root / "crates/horizon-client/src/game.rs"

gfx_source = gfx.read_text(encoding="utf-8")
game_source = game.read_text(encoding="utf-8")

method = """    /// Install the GUI atlas state without exposing the backend device or HUD renderer.
    pub fn configure_hud(&mut self, atlas: &crate::hud::HudAtlas) {
        self.hud.sprite_animations = atlas
            .sprite_animations
            .iter()
            .map(|(rect, animation)| (*rect, animation.clone(), (usize::MAX, 0, 0)))
            .collect();
        self.hud
            .set_text_shader(&self.device, atlas.text_shader.as_deref());
    }

    /// Current framebuffer dimensions, without exposing the wgpu surface configuration.
    pub fn screen_size(&self) -> [u32; 2] {
        [self.config.width, self.config.height]
    }

    /// Apply the renderer's adapter-specific far-terrain cap without leaking wgpu::AdapterInfo.
    pub fn lod_distance_capped(&self, distance: u32) -> u32 {
        crate::graphics::lod_distance_capped(distance, &self.info)
    }

    /// Number of world sections drawn by the last completed frame.
    pub fn drawn_section_count(&self) -> usize {
        self.drawn_sections
    }

    /// Whether a screenshot request is waiting for a frame.
    pub fn has_pending_screenshot(&self) -> bool {
        self.screenshot.is_some()
    }

    /// Queue a screenshot without exposing the renderer's request slot.
    pub fn request_screenshot(&mut self, request: crate::gfx::ScreenshotRequest) -> bool {
        if self.screenshot.is_some() {
            false
        } else {
            self.screenshot = Some(request);
            true
        }
    }

"""
anchor = "impl Renderer {\n    /// Sections with GPU buffers, for soak tests watching residency.\n"
if gfx_source.count(method) == 0:
    if gfx_source.count(anchor) != 1:
        raise SystemExit("Could not uniquely locate Renderer implementation insertion point")
    gfx_source = gfx_source.replace(anchor, anchor + method)
elif gfx_source.count(method) != 1:
    raise SystemExit("Unexpected duplicate Renderer::configure_hud method")

atlas_block = """renderer.hud.sprite_animations = atlas
        .sprite_animations
        .iter()
        .map(|(rect, animation)| (*rect, animation.clone(), (usize::MAX, 0, 0)))
        .collect();"""
atlas_block_deep = """renderer.hud.sprite_animations = atlas
            .sprite_animations
            .iter()
            .map(|(rect, animation)| (*rect, animation.clone(), (usize::MAX, 0, 0)))
            .collect();"""
gui_atlas_block = """renderer.hud.sprite_animations = gui
                .atlas
                .sprite_animations
                .iter()
                .map(|(rect, animation)| (*rect, animation.clone(), (usize::MAX, 0, 0)))
                .collect();"""
screen_size_calls = "[renderer.config.width, renderer.config.height]"
screen_size_closure = "[r.config.width, r.config.height]"
if game_source.count(screen_size_calls) != 4:
    raise SystemExit(f"Expected 4 loading HUD framebuffer-size reads, found {game_source.count(screen_size_calls)}")
if game_source.count(screen_size_closure) != 17:
    raise SystemExit(f"Expected 17 renderer framebuffer-size reads, found {game_source.count(screen_size_closure)}")

atlas_shader = """renderer
        .hud
        .set_text_shader(&renderer.device, atlas.text_shader.as_deref());"""
gui_shader = """renderer
                .hud
                .set_text_shader(&renderer.device, gui.atlas.text_shader.as_deref());"""

if game_source.count(atlas_block) != 1 or game_source.count(atlas_block_deep) != 1:
    raise SystemExit(f"Expected one standard and one nested atlas animation assignment, found {game_source.count(atlas_block)} and {game_source.count(atlas_block_deep)}")
if game_source.count(gui_atlas_block) != 2:
    raise SystemExit(f"Expected 2 GUI atlas animation assignments, found {game_source.count(gui_atlas_block)}")
if game_source.count(atlas_shader) != 1:
    raise SystemExit(f"Expected 1 atlas text shader setup, found {game_source.count(atlas_shader)}")
if game_source.count(gui_shader) != 2:
    raise SystemExit(f"Expected 2 GUI text shader setups, found {game_source.count(gui_shader)}")

game_source = game_source.replace(atlas_block, "renderer.configure_hud(atlas);")
game_source = game_source.replace(atlas_block_deep, "renderer.configure_hud(atlas);")
game_source = game_source.replace(gui_atlas_block, "renderer.configure_hud(&gui.atlas);")
game_source = game_source.replace(atlas_shader, "")
game_source = game_source.replace(screen_size_calls, "renderer.screen_size()")
game_source = game_source.replace(screen_size_closure, "r.screen_size()")

game_source = game_source.replace(gui_shader, "")


if game_source.count("horizon_client::graphics::lod_distance_capped(distance, &renderer.info)") != 1:
    raise SystemExit("Expected one direct adapter-info LOD cap call for the render distance")
if game_source.count("horizon_client::graphics::lod_distance_capped(far_distance, &renderer.info)") != 1:
    raise SystemExit("Expected one direct adapter-info LOD cap call for far terrain")
game_source = game_source.replace("horizon_client::graphics::lod_distance_capped(distance, &renderer.info)", "renderer.lod_distance_capped(distance)")
game_source = game_source.replace("horizon_client::graphics::lod_distance_capped(far_distance, &renderer.info)", "renderer.lod_distance_capped(far_distance)")
if "lod_distance_capped(distance, &renderer.info)" in game_source or "lod_distance_capped(far_distance, &renderer.info)" in game_source:
    raise SystemExit("Game still leaks wgpu::AdapterInfo into LOD policy")

if "renderer.hud.sprite_animations" in game_source or ".set_text_shader(&renderer.device" in game_source:
    raise SystemExit("Game still accesses HUD/backend internals after migration")

if game_source.count("renderer.drawn_sections <= 10") != 1:
    raise SystemExit("Expected one world-icon drawn-section threshold")
if game_source.count("renderer.screenshot.is_some()") != 1:
    raise SystemExit("Expected one pending-screenshot check")
if game_source.count("renderer.screenshot = Some(horizon_client::gfx::ScreenshotRequest {") != 1:
    raise SystemExit("Expected one world-icon screenshot request")
game_source = game_source.replace("renderer.drawn_sections <= 10", "renderer.drawn_section_count() <= 10")
game_source = game_source.replace("renderer.screenshot.is_some()", "renderer.has_pending_screenshot()")
game_source = game_source.replace(
    "renderer.screenshot = Some(horizon_client::gfx::ScreenshotRequest {",
    "renderer.request_screenshot(horizon_client::gfx::ScreenshotRequest {",
)
game_source = game_source.replace(
    "            scaled: false,\n        });",
    "            scaled: false,\n        });",
)

if "renderer.drawn_sections" in game_source or "renderer.screenshot" in game_source:
    raise SystemExit("Game still accesses renderer screenshot or drawn-section storage directly")

gfx.write_text(gfx_source, encoding="utf-8")
game.write_text(game_source, encoding="utf-8")
print("Installed renderer HUD/screen/LOD accessors and encapsulated world-icon screenshot and drawn-section state.")
