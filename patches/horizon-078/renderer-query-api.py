#!/usr/bin/env python3
"""Move Game surface-size reads behind the backend-neutral renderer API."""
import argparse
import re
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
api = root / "crates/horizon-render-api/src/lib.rs"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
game = root / "crates/horizon-client/src/game.rs"

api_text = api.read_text(encoding="utf-8")
trait_sig = "    fn backend_info(&self) -> BackendInfo;"
if api_text.count(trait_sig) != 1:
    raise SystemExit("Expected exactly one RenderBackend backend_info declaration")
api_text = api_text.replace(
    trait_sig,
    "    fn surface_size(&self) -> [u32; 2];\n" + trait_sig,
)

gfx_text = gfx.read_text(encoding="utf-8")
impl_sig = "    fn backend_info(&self) -> horizon_render_api::BackendInfo {"
if gfx_text.count(impl_sig) != 1:
    raise SystemExit("Expected exactly one wgpu RenderBackend backend_info implementation")
gfx_text = gfx_text.replace(
    impl_sig,
    "    fn surface_size(&self) -> [u32; 2] {\n"
    "        [self.config.width, self.config.height]\n"
    "    }\n\n" + impl_sig,
)

game_text = game.read_text(encoding="utf-8")
pattern = re.compile(
    r"\[([A-Za-z_][A-Za-z0-9_]*)\.config\.width,\s*"
    r"\1\.config\.height\]"
)
game_text, replaced = pattern.subn(
    r"horizon_render_api::RenderBackend::surface_size(\1)",
    game_text,
)
if replaced < 10:
    raise SystemExit(f"Expected at least 10 Game surface-size reads, migrated {replaced}")
if ".config.width" in game_text or ".config.height" in game_text:
    raise SystemExit("Game still reads the concrete wgpu surface configuration")

api.write_text(api_text, encoding="utf-8")
gfx.write_text(gfx_text, encoding="utf-8")
game.write_text(game_text, encoding="utf-8")
print(f"Migrated {replaced} Game surface-size reads to RenderBackend::surface_size.")
