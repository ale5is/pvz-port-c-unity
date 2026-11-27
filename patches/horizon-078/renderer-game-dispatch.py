#!/usr/bin/env python3
"""Route Game frame submission through the backend-neutral render contract."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
api = root / "crates/horizon-render-api/src/lib.rs"
game = root / "crates/horizon-client/src/game.rs"
if not api.is_file() or not game.is_file():
    raise SystemExit("Missing backend API or Game source")

api_text = api.read_text(encoding="utf-8")
game_text = game.read_text(encoding="utf-8")
required_api = (
    "pub trait RenderBackend<Frame>",
    "fn render_frame(",
    "fn resize(",
    "fn suspend_surface(",
    "fn recreate_surface(",
    "fn release_surface(",
)
missing = [item for item in required_api if item not in api_text]
if missing:
    raise SystemExit(f"Backend render contract is incomplete: {missing}")

# These are the two known Game-owned frame submission paths: normal frames
# and the loading frame. Use UFCS to make dispatch through the contract
# explicit even though the concrete wgpu Renderer also has render().
normal = "renderer.render(&frame)"
normal_new = "horizon_render_api::RenderBackend::render_frame(renderer, &frame)"
loading = "return renderer.render(&loading_frame(&hud));"
loading_new = "return horizon_render_api::RenderBackend::render_frame(renderer, &loading_frame(&hud));"

if normal in game_text:
    if game_text.count(normal) != 1:
        raise SystemExit("Expected one normal Game frame submission call")
    game_text = game_text.replace(normal, normal_new, 1)
elif game_text.count(normal_new) != 1:
    raise SystemExit("Normal Game frame submission is neither pristine nor migrated")

if loading in game_text:
    if game_text.count(loading) != 1:
        raise SystemExit("Expected one Game loading-frame submission call")
    game_text = game_text.replace(loading, loading_new, 1)
elif game_text.count(loading_new) != 1:
    raise SystemExit("Loading frame submission is neither pristine nor migrated")

if "renderer.render(&frame)" in game_text or "renderer.render(&loading_frame(&hud))" in game_text:
    raise SystemExit("Game still submits frames through the concrete renderer API")

game.write_text(game_text, encoding="utf-8")
print("Game normal and loading frame submission now dispatch through RenderBackend.")
