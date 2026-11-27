#!/usr/bin/env python3
"""Verify Game no longer reads wgpu surface dimensions directly."""
import argparse
import re
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
api = (root / "crates/horizon-render-api/src/lib.rs").read_text(encoding="utf-8")
gfx = (root / "crates/horizon-client-render/src/gfx.rs").read_text(encoding="utf-8")
game = (root / "crates/horizon-client/src/game.rs").read_text(encoding="utf-8")

assert api.count("fn surface_size(&self) -> [u32; 2];") == 1
assert gfx.count("fn surface_size(&self) -> [u32; 2] {") == 1
assert "[self.config.width, self.config.height]" in gfx
assert ".config.width" not in game and ".config.height" not in game
calls = game.count("RenderBackend::surface_size(")
assert calls >= 10, f"Expected >=10 backend-neutral size reads, got {calls}"
assert not re.search(r"renderer\s*\.\s*config\.", game)
print(f"Verified {calls} backend-neutral Game surface-size queries.")
