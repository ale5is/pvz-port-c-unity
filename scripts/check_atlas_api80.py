#!/usr/bin/env python3
"""Audit the pinned Atlas source against Pavo's Horizon 0.7.8 hook registry."""

from pathlib import Path
import json
import tomllib

ROOT = Path(__file__).resolve().parents[1]
ATLAS = ROOT / "mod/atlas-upstream"
MANIFEST = ATLAS / "horizon-mixin.toml"
PORT_MANIFEST = ROOT / "mod/horizon-mixin.toml"
REGISTRY = ROOT / "horizon-source/native-hooks.json"

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
    compatibility = manifest.get("compatibility", {})
    if compatibility.get("horizon_revision") != "9f42064c11efe7ef3e5ed2baad7ddc2a3b36eea0":
        raise SystemExit("Pinned Atlas upstream revision changed; review its source before continuing.")
    hook_source = (ROOT / "horizon-source/crates/horizon-client/src/game/native_hooks.rs").read_text(encoding="utf-8")
    if "NATIVE_HOOKS_API: u32 = 80" not in hook_source:
        raise SystemExit("The checked-out Horizon source is not the expected hooks API 80.")
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

    print("Atlas upstream source inventory: OK")
    print(f"Upstream injection targets present in Horizon API 80: {len(injections) - len(missing)}/23")
    print(f"Upstream-only targets still requiring adaptation: {len(missing)}")

    if not PORT_MANIFEST.is_file():
        raise SystemExit("The active Pavo mixin manifest is missing.")
    port_manifest = tomllib.loads(PORT_MANIFEST.read_text(encoding="utf-8"))
    if port_manifest.get("compatibility", {}).get("horizon_revision") != "68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29":
        raise SystemExit("The active Pavo manifest is not pinned to the reviewed Horizon revision.")

    registered = {(item["name"], item["version"]) for item in registry.get("hooks", [])}
    port_injections = port_manifest.get("injection", [])
    unsupported = sorted(
        f"{item['id']}: {item['hook']}@{item.get('hook_version')}"
        for item in port_injections
        if "hook" in item and (item["hook"], item.get("hook_version")) not in registered
    )
    if unsupported:
        raise SystemExit("The active Pavo manifest uses hooks absent from Horizon API 80:\\n" + "\\n".join(unsupported))

    atlas_ids = {
        "atlas_native_minimap",
        "atlas_frame",
        "atlas_native_hud",
        "atlas_screen_render",
        "atlas_screen_mouse_pressed",
        "atlas_screen_mouse_released",
    }
    active_atlas = {item["id"] for item in port_injections}
    if not atlas_ids.issubset(active_atlas):
        raise SystemExit("The active Pavo manifest is missing Atlas API-80 adapters: " + ", ".join(sorted(atlas_ids - active_atlas)))

    missing_port_sources = []
    for module in port_manifest.get("module", []):
        if module["id"].startswith("atlas_") and not (ROOT / "mod" / module["source"]).is_file():
            missing_port_sources.append(module["source"])
    for injection in port_injections:
        if injection["id"].startswith("atlas_"):
            helper = ROOT / "mod" / injection["helper"]
            if not helper.is_file():
                missing_port_sources.append(injection["helper"])
    if missing_port_sources:
        raise SystemExit("Active Atlas adapter files are missing:\\n" + "\\n".join(missing_port_sources))

    print("Active Atlas API-80 adapter inventory: OK")
    print(f"Active Pavo hooks verified against Horizon API 80: {len(port_injections) - len(unsupported)}/" + str(len(port_injections)))
    print("World-map render and mouse input use only hooks published in the pinned API 80 registry.")


if __name__ == "__main__":
    main()
