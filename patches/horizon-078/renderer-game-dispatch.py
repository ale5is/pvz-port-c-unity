#!/usr/bin/env python3
"""Route every Game frame submission through the backend-neutral render contract."""
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
for marker in ("pub trait RenderBackend<Frame>", "fn render_frame(", "fn resize(", "fn suspend_surface(", "fn recreate_surface(", "fn release_surface("):
    if marker not in api_text:
        raise SystemExit(f"Backend render contract is incomplete: {marker}")

normal = "renderer.render(&frame)"
normal_new = "horizon_render_api::RenderBackend::render_frame(renderer, &frame).map_err(|error| Box::new(error) as Box<dyn std::error::Error>)"
loading = "return renderer.render(&loading_frame(&hud));"
loading_new = "return horizon_render_api::RenderBackend::render_frame(renderer, &loading_frame(&hud)).map_err(|error| Box::new(error) as Box<dyn std::error::Error>);"
normal_count = game_text.count(normal)
loading_count = game_text.count(loading)
if normal_count not in (0, 2) or loading_count not in (0, 5):
    raise SystemExit(f"Unexpected Game frame call-site inventory: normal={normal_count}, loading={loading_count}")
if normal_count:
    game_text = game_text.replace(normal, normal_new)
elif game_text.count(normal_new) != 2:
    raise SystemExit("Normal Game frame calls are neither pristine nor fully migrated")
if loading_count:
    game_text = game_text.replace(loading, loading_new)
elif game_text.count(loading_new) != 5:
    raise SystemExit("Game loading-frame calls are neither pristine nor fully migrated")
if normal in game_text or loading in game_text:
    raise SystemExit("Game still submits a frame through the concrete renderer API")
game.write_text(game_text, encoding="utf-8")
none_old = 'None => Err("renderer".into()),'
none_new = 'None => Err(horizon_render_api::RenderBackendError::new("renderer")),'
if game_text.count(none_old) != 1:
    raise SystemExit(f"Expected one missing-renderer error branch, got {game_text.count(none_old)}")
game_text = game_text.replace(none_old, none_new)
result_old = "        result\\n    }\\n    /// The frame shows the title"
result_new = "        result.map_err(|error| Box::new(error) as Box<dyn std::error::Error>)\\n    }\\n    /// The frame shows the title"
if game_text.count(result_old) != 1:
    raise SystemExit("Could not uniquely locate Game frame result return")
game_text = game_text.replace(result_old, result_new)
game.write_text(game_text, encoding="utf-8")
print("Migrated all 2 normal and 5 loading Game frame submissions and normalized Game error types.")
