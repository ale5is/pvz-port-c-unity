#!/usr/bin/env python3
"""Audit the deliberately small set of remaining game-to-wgpu ownership leaks."""
import collections
import re
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_renderer_ownership.py <horizon-source-root>")

root = Path(sys.argv[1])
game = root / "crates/horizon-client/src/game.rs"
launcher = root / "crates/horizon-client/src/android/launcher.rs"
source = game.read_text(encoding="utf-8")
launcher_source = launcher.read_text(encoding="utf-8")

# The launcher creates the loading renderer before Game exists. Audit this
# independent owner too, so a Game-only check cannot miss a second surface.
launcher_field = re.findall(r"(?m)^\s*renderer:\s*Option<Renderer>,\s*$", launcher_source)
loading_factory = re.findall(r"game::loading_renderer\s*\(", launcher_source)
if len(launcher_field) != 1 or len(loading_factory) != 1:
    raise SystemExit(
        "Unexpected launcher renderer boundary: "
        f"Renderer fields={len(launcher_field)}, "
        f"loading_renderer factory calls={len(loading_factory)}"
    )

pattern = re.compile(
    r"\b(renderer|previous|r)\."
    r"(device|queue|surface|spare_surface|instance|adapter|background_context|"
    r"config|screenshot_result|screenshot|drawn_sections|last_wait|last_encode|"
    r"gpu_times|info|hud|suspended)\b"
)
found = collections.Counter(f"{owner}.{member}" for owner, member in pattern.findall(source))
expected = collections.Counter(
    {
        "previous.background_context": 1,
    }
)

if found != expected:
    details = {
        "expected": dict(sorted(expected.items())),
        "found": dict(sorted(found.items())),
    }
    raise SystemExit(f"Unexpected game-side renderer ownership boundary: {details}")

print(
    "Renderer ownership audit passed: Game has only the documented "
    "background-context handoff; Android Launcher still has exactly one "
    "concrete Renderer field and one loading_renderer factory call. "
    "Both paths remain an explicit paired migration requirement."
)
