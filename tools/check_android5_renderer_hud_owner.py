#!/usr/bin/env python3
import sys
from pathlib import Path
if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_renderer_hud_owner.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
game = root / "crates/horizon-client/src/game.rs"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
if not game.is_file() or not gfx.is_file():
    raise SystemExit("FAIL: missing renderer source")
m = game.read_text(encoding="utf-8")
g = gfx.read_text(encoding="utf-8")
if "renderer.device" in m or "renderer.hud" in m:
    raise SystemExit("FAIL: Game still accesses wgpu-owned HUD/device fields")
if "pub fn configure_hud_atlas(" not in g:
    raise SystemExit("FAIL: renderer HUD configuration boundary missing")
if m.count("renderer.configure_hud_atlas(") < 4:
    raise SystemExit("FAIL: not all HUD atlas construction/handoff paths migrated")
print("PASS: loading, world construction and replacement handoff use renderer-owned HUD configuration.")
