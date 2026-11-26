#!/usr/bin/env python3
"""Guard the pinned Horizon game-loop and Android surface lifecycle seams."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_game_loop.py <horizon-source-root>")

root = Path(sys.argv[1])
game_path = root / "crates/horizon-client/src/game.rs"
launcher_path = root / "crates/horizon-client/src/android/launcher.rs"
game = game_path.read_text(encoding="utf-8")
launcher = launcher_path.read_text(encoding="utf-8")

def require(source: str, label: str, markers: tuple[str, ...]) -> None:
    missing = [marker for marker in markers if marker not in source]
    if missing:
        raise SystemExit(f"FAIL: {label}: missing markers: {missing}")
    print(f"PASS: {label}")

def require_order(source: str, label: str, markers: tuple[str, ...]) -> None:
    positions = [source.find(marker) for marker in markers]
    if any(position < 0 for position in positions):
        missing = [marker for marker, position in zip(markers, positions) if position < 0]
        raise SystemExit(f"FAIL: {label}: missing markers: {missing}")
    if positions != sorted(positions):
        raise SystemExit(f"FAIL: {label}: expected source order {markers}")
    print(f"PASS: {label}")

require(
    game,
    "Game retains a CPU-side pending mesh queue",
    ("pending_uploads: Vec<((i32, i32, i32), horizon_client::mesher::SectionMesh)>",),
)
require(
    game,
    "Android clients enter the budgeted upload path",
    (
        "let mobile_uploads =",
        "cfg!(target_os = \"android\") && self.options.execution_role.is_client()",
        "let budgeted = g.chunk_updates > 0 || g.upload_budget_kib > 0 || mobile_uploads;",
        "UploadBudget::with_frame_state(",
    ),
)
require_order(
    game,
    "Budgeted upload preserves mesh ownership and unload cleanup",
    (
        "let pending = &mut self.pending_uploads;",
        "renderer.upload(key, &mesh);",
        "self.streaming.free(spent);",
        "renderer.remove_columns(unloaded.iter().map(|c| (c.x, c.z)));",
    ),
)
require(
    game,
    "Android game suspension releases the active renderer surface",
    (
        "fn suspended(&mut self, _event_loop: &ActiveEventLoop)",
        "self.surface_lost = true;",
        "renderer.suspend_surface();",
    ),
)
require_order(
    game,
    "Android resume restores size before recreating and resizing the surface",
    (
        "fn resumed(&mut self, event_loop: &ActiveEventLoop)",
        "renderer.resize(size[0], size[1])",
        "renderer.recreate_surface()",
        "renderer.resize(size.width, size.height)",
    ),
)
require(
    launcher,
    "Android launcher delegates lifecycle to the active game",
    (
        "return game.resumed(events);",
        "return game.suspended(events);",
        "self.game = Some(game);",
        "self.game.as_mut().unwrap().resumed(events);",
    ),
)

print("Pinned Horizon game-loop/lifecycle contract passed; no renderer integration is implied.")
