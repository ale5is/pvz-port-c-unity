#!/usr/bin/env python3
"""Move debug render-timing control behind RenderBackend."""
import argparse
from pathlib import Path
parser=argparse.ArgumentParser()
parser.add_argument("source",type=Path)
root=parser.parse_args().source.resolve()
api=root/"crates/horizon-render-api/src/lib.rs"
gfx=root/"crates/horizon-client-render/src/gfx.rs"
game=root/"crates/horizon-client/src/game.rs"
for p in (api,gfx,game):
    if not p.is_file(): raise SystemExit(f"Missing source: {p}")
a=api.read_text(encoding="utf-8"); g=gfx.read_text(encoding="utf-8"); m=game.read_text(encoding="utf-8")
trait="pub trait RenderBackend<Frame> {"
method="    fn set_debug_timing(&mut self, enabled: bool);\n"
if method not in a:
    if a.count(trait)!=1: raise SystemExit("Could not uniquely locate RenderBackend")
    a=a.replace(trait,trait+"\n"+method,1)
elif a.count(method)!=1: raise SystemExit("Duplicate set_debug_timing method")
impl="impl<'frame> horizon_render_api::RenderBackend<Frame<'frame>> for Renderer {"
impl_method="    fn set_debug_timing(&mut self, enabled: bool) {\n        Renderer::set_pass_timing(self, enabled);\n    }\n\n"
if impl_method not in g:
    if g.count(impl)!=1: raise SystemExit("Could not uniquely locate wgpu RenderBackend implementation")
    pos=g.find("    fn resize(&mut self, width: u32, height: u32) {",g.index(impl))
    if pos<0: raise SystemExit("Could not locate wgpu adapter resize method")
    g=g[:pos]+impl_method+g[pos:]
elif g.count(impl_method)!=1: raise SystemExit("Duplicate wgpu debug timing adapter")
old="renderer.set_pass_timing(self.debug);"
new="horizon_render_api::RenderBackend::set_debug_timing(renderer, self.debug);"
if old in m:
    if m.count(old)!=1: raise SystemExit("Expected exactly one debug timing call")
    m=m.replace(old,new,1)
elif m.count(new)!=1: raise SystemExit("Game debug timing call is neither pristine nor migrated")
if old in m: raise SystemExit("Concrete debug timing call remains")
api.write_text(a,encoding="utf-8"); gfx.write_text(g,encoding="utf-8"); game.write_text(m,encoding="utf-8")
print("Game debug timing now dispatches through RenderBackend.")
