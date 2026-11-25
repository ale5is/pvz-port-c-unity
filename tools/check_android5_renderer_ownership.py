#!/usr/bin/env python3
"""Audit the deliberately small set of remaining game-to-wgpu ownership leaks."""
import collections
import re
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_renderer_ownership.py <horizon-source-root>")

game = Path(sys.argv[1]) / "crates/horizon-client/src/game.rs"
source = game.read_text(encoding="utf-8")

pattern = re.compile(
    r"\b(renderer|previous|r)\.(device|queue|surface|spare_surface|instance|adapter|background_context)\b"
)
found = collections.Counter(f"{owner}.{member}" for owner, member in pattern.findall(source))
expected = collections.Counter(
    {
        "previous.device": 1,
        "previous.background_context": 1,
        "r.device": 1,
    }
)

if found != expected:
    details = {
        "expected": dict(sorted(expected.items())),
        "found": dict(sorted(found.items())),
    }
    raise SystemExit(f"Unexpected game-side renderer ownership boundary: {details}")

print(
    "Renderer ownership audit passed: only the three documented device-wait/"
    "background-context handoff references remain."
)
