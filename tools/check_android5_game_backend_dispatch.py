#!/usr/bin/env python3
"""Verify Game frame submission uses the shared backend contract."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_game_backend_dispatch.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
api = (root / "crates/horizon-render-api/src/lib.rs").read_text(encoding="utf-8")
game = (root / "crates/horizon-client/src/game.rs").read_text(encoding="utf-8")

for marker in ("pub trait RenderBackend<Frame>", "fn render_frame("):
    if marker not in api:
        raise SystemExit(f"FAIL: backend API missing {marker}")
normal = "horizon_render_api::RenderBackend::render_frame(renderer, &frame)"
loading = "horizon_render_api::RenderBackend::render_frame(renderer, &loading_frame(&hud))"
if game.count(normal) != 1:
    raise SystemExit(f"FAIL: expected one backend-dispatched normal frame, got {game.count(normal)}")
if game.count(loading) != 1:
    raise SystemExit(f"FAIL: expected one backend-dispatched loading frame, got {game.count(loading)}")
for stale in ("renderer.render(&frame)", "renderer.render(&loading_frame(&hud))"):
    if stale in game:
        raise SystemExit(f"FAIL: concrete Game frame submission remains: {stale}")
print("PASS: normal Game frame and loading frame dispatch through RenderBackend.")
print("LIMITATION: Game still stores concrete Renderer; Launcher and renderer construction are not yet backend-polymorphic.")
