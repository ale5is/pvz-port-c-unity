#!/usr/bin/env python3
"""Verify debug timing is part of the neutral render contract."""
import sys
from pathlib import Path
if len(sys.argv)!=2: raise SystemExit("usage: check_android5_debug_timing_dispatch.py <horizon-source-root>")
root=Path(sys.argv[1]).resolve()
api=(root/"crates/horizon-render-api/src/lib.rs").read_text(encoding="utf-8")
gfx=(root/"crates/horizon-client-render/src/gfx.rs").read_text(encoding="utf-8")
game=(root/"crates/horizon-client/src/game.rs").read_text(encoding="utf-8")
if "fn set_debug_timing(&mut self, enabled: bool);" not in api: raise SystemExit("FAIL: RenderBackend lacks debug timing contract")
if "Renderer::set_pass_timing(self, enabled);" not in gfx: raise SystemExit("FAIL: wgpu adapter does not implement debug timing")
call="horizon_render_api::RenderBackend::set_debug_timing(renderer, self.debug);"
if game.count(call)!=1: raise SystemExit("FAIL: Game does not dispatch debug timing exactly once")
if "renderer.set_pass_timing(self.debug);" in game: raise SystemExit("FAIL: concrete renderer debug timing remains")
print("PASS: Game debug timing uses RenderBackend.")
