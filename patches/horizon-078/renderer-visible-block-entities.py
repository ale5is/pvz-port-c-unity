#!/usr/bin/env python3
"""Move renderer-owned block-entity visibility behind RenderBackend."""
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
        raise SystemExit(f"Missing block-entity renderer source: {path}")

a, g, m = (p.read_text(encoding="utf-8") for p in (api, gfx, game))
api_anchor = "    fn native_shadow(&self) -> bool;"
api_method = """    /// Iterate block entities whose terrain sections have completed their fade-in.
    fn visible_block_entities<'a>(
        &'a self,
        fade_seconds: f32,
    ) -> Box<dyn Iterator<Item = &'a ([i32; 3], u32)> + 'a>;
"""
if "fn visible_block_entities<'a>(" not in a:
    if a.count(api_anchor) != 1:
        raise SystemExit("Could not uniquely locate RenderBackend feature methods")
    a = a.replace(api_anchor, api_method + api_anchor, 1)

trait_anchor = "impl<'frame> horizon_render_api::RenderBackend<Frame<'frame>> for Renderer {"
start = g.find(trait_anchor)
if start < 0:
    raise SystemExit("Could not locate exact wgpu RenderBackend implementation")
next_impl = g.find("\nimpl ", start + len(trait_anchor))
end = next_impl if next_impl >= 0 else len(g)
body = g[start:end]
impl_anchor = "    fn backend_info(&self) -> horizon_render_api::BackendInfo {"
impl_method = """    fn visible_block_entities<'a>(
        &'a self,
        fade_seconds: f32,
    ) -> Box<dyn Iterator<Item = &'a ([i32; 3], u32)> + 'a> {
        Box::new(Renderer::visible_block_entities(self, fade_seconds))
    }

"""
if "fn visible_block_entities<'a>(" not in body:
    if body.count(impl_anchor) != 1:
        raise SystemExit("Could not uniquely locate backend_info in wgpu RenderBackend implementation")
    g = g[:start] + body.replace(impl_anchor, impl_method + impl_anchor, 1) + g[end:]

old = "r.visible_block_entities(self.settings.chunk_fade)"
new = "horizon_render_api::RenderBackend::visible_block_entities(r, self.settings.chunk_fade)"
if old in m:
    m = m.replace(old, new)
elif new not in m:
    raise SystemExit("Expected Game block-entity visibility call was not found")

if old in m:
    raise SystemExit("Game still calls concrete Renderer::visible_block_entities")
for forbidden in ("wgpu::", "winit::", "EGLDisplay", "EGLContext", "GLuint"):
    if forbidden in a:
        raise SystemExit(f"RenderBackend leaks implementation type: {forbidden}")

api.write_text(a, encoding="utf-8")
gfx.write_text(g, encoding="utf-8")
game.write_text(m, encoding="utf-8")
print("Moved renderer-owned block-entity fade visibility behind RenderBackend.")
