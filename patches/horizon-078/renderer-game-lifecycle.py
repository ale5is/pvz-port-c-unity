#!/usr/bin/env python3
"""Route Game and Android Launcher surface lifecycle through RenderBackend."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
api = root / "crates/horizon-render-api/src/lib.rs"
files = {
    "Game": root / "crates/horizon-client/src/game.rs",
    "Android Launcher": root / "crates/horizon-client/src/android/launcher.rs",
}
if not api.is_file() or any(not p.is_file() for p in files.values()):
    raise SystemExit("Missing backend API, Game or Android Launcher source")
api_text = api.read_text(encoding="utf-8")
for marker in ("pub trait RenderBackend<Frame>", "fn resize(", "fn suspend_surface(", "fn recreate_surface(", "fn release_surface("):
    if marker not in api_text:
        raise SystemExit(f"RenderBackend lifecycle API missing {marker}")

replacements = (
    ("renderer.suspend_surface();", "horizon_render_api::RenderBackend::suspend_surface(renderer);"),
    ("self.renderer.as_mut().unwrap().suspend_surface();", "horizon_render_api::RenderBackend::suspend_surface(self.renderer.as_mut().unwrap());"),
    ("renderer.recreate_surface()", "horizon_render_api::RenderBackend::recreate_surface(renderer)"),
    ("renderer.resize(", "horizon_render_api::RenderBackend::resize(renderer, "),
    ("r.resize(", "horizon_render_api::RenderBackend::resize(r, "),
    ("r.suspend_surface();", "horizon_render_api::RenderBackend::suspend_surface(r);"),
    ("renderer.release_surface();", "horizon_render_api::RenderBackend::release_surface(renderer);"),
)
for label, path in files.items():
    source = path.read_text(encoding="utf-8")
    changed = 0
    for old, new in replacements:
        count = source.count(old)
        if count:
            source = source.replace(old, new)
            changed += count
    if changed == 0:
        raise SystemExit(f"No lifecycle call sites migrated in {label}")
    for stale in ("renderer.suspend_surface();", "renderer.recreate_surface()", "renderer.resize(", "renderer.release_surface();", "r.resize(", "r.suspend_surface();"):
        if stale in source:
            raise SystemExit(f"{label} retains concrete lifecycle call: {stale}")
    if label == "Game":
        lifecycle_error = "self.error = Some(error);"
        contexts = source.count("if let Err(error) = horizon_render_api::RenderBackend::recreate_surface(renderer) {")
        if contexts != 2:
            raise SystemExit(f"Expected 2 backend surface recreation branches, got {contexts}")
        for marker in ("if let Err(error) = horizon_render_api::RenderBackend::recreate_surface(renderer) {",):
            start = 0
            while True:
                start = source.find(marker, start)
                if start < 0:
                    break
                end = source.find("}", start)
                segment = source[start:end]
                if lifecycle_error not in segment:
                    raise SystemExit("Surface recreation error branch changed unexpectedly")
                source = source[:start] + segment.replace(lifecycle_error, "self.error = Some(Box::new(error));") + source[end:]
                start += len(segment)
    path.write_text(source, encoding="utf-8")
    print(f"Migrated {changed} lifecycle call sites in {label}.")
