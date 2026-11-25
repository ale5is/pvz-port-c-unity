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

if "renderer.hud.sprite_animations" in game_source or ".set_text_shader(&renderer.device" in game_source:
    raise SystemExit("Game still accesses HUD/backend internals after migration")

gfx.write_text(gfx_source, encoding="utf-8")
game.write_text(game_source, encoding="utf-8")
print("Installed Renderer::configure_hud and Renderer::screen_size; migrated all HUD setup and 21 framebuffer-size reads in Game.")
