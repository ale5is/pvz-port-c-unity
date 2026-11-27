#!/usr/bin/env python3
"""Verify Game and Launcher dispatch surface lifecycle via RenderBackend."""
import sys
from pathlib import Path
if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_game_lifecycle_dispatch.py <horizon-source-root>")
root = Path(sys.argv[1])
api = (root / "crates/horizon-render-api/src/lib.rs").read_text(encoding="utf-8")
for method in ("resize", "suspend_surface", "recreate_surface", "release_surface"):
    if f"fn {method}(" not in api:
        raise SystemExit(f"FAIL: RenderBackend missing {method}")
checks = {
    "Game": (root / "crates/horizon-client/src/game.rs", (
        "horizon_render_api::RenderBackend::suspend_surface(",
        "horizon_render_api::RenderBackend::recreate_surface(",
        "horizon_render_api::RenderBackend::resize(",
        "horizon_render_api::RenderBackend::release_surface(",
    )),
    "Android Launcher": (root / "crates/horizon-client/src/android/launcher.rs", (
        "horizon_render_api::RenderBackend::suspend_surface(",
        "horizon_render_api::RenderBackend::recreate_surface(",
        "horizon_render_api::RenderBackend::resize(",
    )),
}
for label, (path, markers) in checks.items():
    source = path.read_text(encoding="utf-8")
    missing = [m for m in markers if m not in source]
    if missing:
        raise SystemExit(f"FAIL: {label} lifecycle dispatch missing {missing}")
    for stale in ("renderer.suspend_surface();", "renderer.recreate_surface()", "renderer.resize(", "renderer.release_surface();", "r.resize(", "r.suspend_surface();"):
        if stale in source:
            raise SystemExit(f"FAIL: {label} still calls concrete lifecycle method {stale}")
    print(f"PASS: {label} surface lifecycle dispatches through RenderBackend.")
print("LIMITATION: renderer storage and construction remain concrete wgpu types.")
