#!/usr/bin/env python3
"""Audit the pinned Atlas source against Pavo's Horizon 0.7.8 hook registry."""

from pathlib import Path
import json
import tomllib

ROOT = Path(__file__).resolve().parents[1]
ATLAS = ROOT / "mod/atlas-upstream"
MANIFEST = ATLAS / "horizon-mixin.toml"
REGISTRY = ROOT / ".build/horizon-source/native-hooks.json"

EXPECTED_MISSING = {
    "horizon_client::game::native_hooks::Game<'_>::native_screen_mouse_dragged_pre",
    "horizon_client::game::native_hooks::Game<'_>::native_screen_mouse_scrolled_pre",
    "horizon_client::game::native_hooks::Game<'_>::native_screen_key_pressed_pre",
    "horizon_client::game::native_hooks::Game<'_>::native_screen_char_typed_pre",
    "horizon_client::game::native_hooks::Game<'_>::native_camera_pose_now",
    "horizon_client::game::native_hooks::Game<'_>::native_level_unload",
}


def main() -> None:
    if not MANIFEST.is_file() or not REGISTRY.is_file():
        raise SystemExit("Atlas manifest or pinned Horizon native-hooks.json is missing.")

    manifest = tomllib.loads(MANIFEST.read_text(encoding="utf-8"))
    registry = json.loads(REGISTRY.read_text(encoding="utf-8"))
    injections = manifest.get("injection", [])
    hooks = {item["target"] for item in registry.get("hooks", [])}

    if len(injections) != 23:
        raise SystemExit(f"Expected 23 pinned Atlas injections; found {len(injections)}.")

    missing = {item["target"] for item in injections if item["target"] not in hooks}
    if missing != EXPECTED_MISSING:
        raise SystemExit(
            "Atlas/Horizon hook compatibility changed. Re-audit before porting.\n"
            + "\n".join(sorted(missing))
        )

    absent_sources = []
    for module in manifest.get("module", []):
        path = ATLAS / module["source"]
        if not path.is_file():
            absent_sources.append(str(path.relative_to(ROOT)))
    for injection in injections:
        helper = ATLAS / injection["helper"]
        if not helper.is_file():
            absent_sources.append(str(helper.relative_to(ROOT)))
    if absent_sources:
        raise SystemExit("Atlas source files are missing:\n" + "\n".join(absent_sources))

    print("Atlas source inventory: OK")
    print(f"Injection targets present in Horizon API 80: {len(injections) - len(missing)}/23")
    print(f"Injection targets requiring adaptation: {len(missing)}")
    print("Atlas remains reference-only; no incompatible hooks are registered in the APK.")


if __name__ == "__main__":
    main()
