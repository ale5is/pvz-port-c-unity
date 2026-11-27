#!/usr/bin/env python3
import argparse
from pathlib import Path
parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
gfx = root / "crates/horizon-client-render/src/gfx.rs"
game = root / "crates/horizon-client/src/game.rs"
if not gfx.is_file() or not game.is_file():
    raise SystemExit("Missing renderer source")
g = gfx.read_text(encoding="utf-8")
m = game.read_text(encoding="utf-8")
method = """impl Renderer {
    /// Configure atlas animations and text shader without exposing GPU handles.
    pub fn configure_hud_atlas(&mut self, atlas: &crate::hud::HudAtlas, install_text_shader: bool) {
        self.hud.sprite_animations = atlas
            .sprite_animations
            .iter()
            .map(|(rect, animation)| (*rect, animation.clone(), (usize::MAX, 0, 0)))
            .collect();
        if install_text_shader {
            self.hud
                .set_text_shader(&self.device, atlas.text_shader.as_deref());
        }
    }
}

"""
if "pub fn configure_hud_atlas(" not in g:
    marker = "impl Renderer {"
    if marker not in g:
        raise SystemExit("Renderer impl marker missing")
    g = g.replace(marker, method + marker, 1)
old_build = """    renderer.hud.sprite_animations = atlas
        .sprite_animations
        .iter()
        .map(|(rect, animation)| (*rect, animation.clone(), (usize::MAX, 0, 0)))
        .collect();
    renderer
        .hud
        .set_text_shader(&renderer.device, atlas.text_shader.as_deref());"""
if old_build in m:
    m = m.replace(old_build, "    renderer.configure_hud_atlas(atlas, true);", 1)
elif "renderer.configure_hud_atlas(atlas);" not in m:
    raise SystemExit("build_renderer HUD setup not found")
old_loading = """        renderer.hud.sprite_animations = atlas
            .sprite_animations
            .iter()
            .map(|(rect, animation)| (*rect, animation.clone(), (usize::MAX, 0, 0)))
            .collect();"""
if old_loading in m:
    m = m.replace(old_loading, "        renderer.configure_hud_atlas(atlas, false);", 1)
elif "renderer.configure_hud_atlas(atlas);" not in m:
    raise SystemExit("loading_renderer HUD setup not found")
old_finish = """            renderer.hud.sprite_animations = gui
                .atlas
                .sprite_animations
                .iter()
                .map(|(rect, animation)| (*rect, animation.clone(), (usize::MAX, 0, 0)))
                .collect();
            renderer
                .hud
                .set_text_shader(&renderer.device, gui.atlas.text_shader.as_deref());"""
if m.count(old_finish):
    m = m.replace(old_finish, "            renderer.configure_hud_atlas(&gui.atlas, true);")
elif m.count("renderer.configure_hud_atlas(&gui.atlas);") < 2:
    raise SystemExit("Both finish() HUD handoff branches were not found")
if "renderer.device" in m or "renderer.hud" in m:
    raise SystemExit("Game still accesses renderer HUD/device internals")
if m.count("renderer.configure_hud_atlas(") < 4:
    raise SystemExit("Expected four HUD configuration call sites")
gfx.write_text(g, encoding="utf-8")
game.write_text(m, encoding="utf-8")
print("Moved HUD atlas GPU setup into Renderer.")
