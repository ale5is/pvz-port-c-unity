#!/usr/bin/env python3
"""Print a source-derived inventory of Horizon's concrete renderer contract."""
import re
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: report_android5_gles2_migration.py <horizon-source-root>")

root = Path(sys.argv[1]).resolve()
gfx_path = root / "crates/horizon-client-render/src/gfx.rs"
game_path = root / "crates/horizon-client/src/game.rs"
launcher_path = root / "crates/horizon-client/src/android/launcher.rs"
for path in (gfx_path, game_path, launcher_path):
    if not path.is_file():
        raise SystemExit(f"Missing pinned Horizon source: {path.relative_to(root)}")

gfx = gfx_path.read_text(encoding="utf-8")
game = game_path.read_text(encoding="utf-8")
launcher = launcher_path.read_text(encoding="utf-8")


def block(source: str, start: str, end: str) -> str:
    first = source.find(start)
    if first < 0:
        raise SystemExit(f"Missing source declaration: {start}")
    last = source.find(end, first)
    if last < 0:
        raise SystemExit(f"Missing end marker for: {start}")
    return source[first:last]


frame = block(gfx, "pub struct Frame<'a> {", "\n}\n/// One frame of the world border")
renderer = block(gfx, "pub struct Renderer {", "\n}\n")
frame_fields = re.findall(r"^\s*pub\s+([A-Za-z_]\w*)\s*:", frame, re.M)
renderer_fields = re.findall(r"^\s*pub\s+([A-Za-z_]\w*)\s*:", renderer, re.M)

methods = set()
for match in re.finditer(r"impl Renderer\s*\{", gfx):
    tail = gfx[match.end():]
    methods.update(re.findall(r"^\s*pub(?:\([^)]*\))?\s+(?:async\s+)?fn\s+([A-Za-z_]\w*)", tail, re.M))

print(f"Frame public fields: {len(frame_fields)}")
print("  " + ", ".join(frame_fields))
print(f"Renderer public fields: {len(renderer_fields)}")
print("  " + ", ".join(renderer_fields))
print(f"Renderer public methods (including impl blocks): {len(methods)}")
print("  " + ", ".join(sorted(methods)))

for label, source, markers in (
    ("Game", game, ("renderer: Option<Renderer>", "fn build_renderer(", "renderer.render(&frame)", "renderer.suspend_surface()", "renderer.recreate_surface()")),
    ("Launcher", launcher, ("renderer: Option<Renderer>", "game::loading_renderer(", "renderer.set_loading_ui(", "renderer.suspend_surface();")),
):
    missing = [marker for marker in markers if marker not in source]
    if missing:
        raise SystemExit(f"{label} renderer contract changed; review migration inventory: {missing}")
    print(f"{label} concrete renderer call sites: verified")

if len(frame_fields) < 60 or len(renderer_fields) < 15 or len(methods) < 30:
    raise SystemExit("Renderer contract unexpectedly shrank; manually review before porting")
print("AUDIT ONLY: this report does not indicate GLES2 Game/Launcher integration.")
