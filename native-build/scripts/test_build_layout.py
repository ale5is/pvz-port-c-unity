#!/usr/bin/env python3
from __future__ import annotations
import pathlib
import tomllib

ROOT = pathlib.Path(__file__).resolve().parents[2]
PIN = "68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29"


def manifest(folder: str, expected_id: str) -> dict:
    path = ROOT / "mod" / folder / "horizon-mixin.toml"
    value = tomllib.loads(path.read_text())
    assert value["compatibility"]["horizon_revision"] == PIN
    assert value["mod"]["id"] == expected_id
    return value


def main() -> None:
    svc = manifest("simple-voice-chat", "simple-voice-chat")
    builder = manifest("native-mod-builder", "horizon-native-builder")
    assert any(item.get("id") == "svc_client" for item in svc.get("crate", []))
    builder_hooks = {item["hook"] for item in builder.get("injection", [])}
    assert builder_hooks == {"mods", "settings", "settings_click"}

    source = (ROOT / "mod/native-mod-builder/native_mod_builder.rs").read_text()
    assert "thread::Builder::new()" in source
    assert "Game::native_config_dir()" in source
    assert "std::env::current_dir()" not in source
    assert "fn on_frame" not in source
    assert "disable_compiled_build()" in source
    assert 'native.join("current")' in source
    assert "disabled_mod_ids" in source
    assert "set_mod_enabled" in source
    assert "disabled.contains(&item.id)" in source
    assert "Compile enabled native mods" in source

    build = (ROOT / "native-build/scripts/native_build.sh").read_text()
    prepare = (ROOT / "native-build/scripts/prepare.sh").read_text()
    assert 'cp -r "$HOME/builtin/simple-voice-chat"' in build
    assert 'cp -r "$HOME/builtin/native-mod-builder"' in build
    assert "native-mod-builder|horizon-native-builder)" in build
    assert '"$work/mods/horizon-native-builder"' in build
    assert 'cp -r "$repo_root/mod/native-mod-builder"' in prepare
    assert 'cp "$repo_root/native-build/versions.json" "$root/versions.json"' in prepare
    smoke = (ROOT / "native-build/scripts/create_smoke_request.py").read_text()
    assert '"hooks": 80' in smoke
    assert 'hook = "mods"' in smoke
    assert "smoke_mod_list" in smoke

    versions = __import__("json").loads((ROOT / "native-build/versions.json").read_text())
    assert versions["0.7.8"]["hooks"] == 80
    assert versions["0.7.8"]["game"] == "26.3"
    build_script = (ROOT / "native-build/scripts/native_build.sh").read_text()
    assert "HORIZON_VERSION=0.7.8" in build_script
    assert "GAME_VERSION=26.3" in build_script
    assert "HOOKS_API=80" in build_script
    assert '"hooks": int(hooks_api)' in build_script
    assert "horizon-native-builder" in build_script

    base_workflow = (ROOT / ".github/workflows/horizon-mod-build-base.yml").read_text()
    build_workflow = (ROOT / ".github/workflows/horizon-mod-build.yml").read_text()
    assert "horizon-078-native-base-arm64-v1" in base_workflow
    assert "horizon-078-native-base-arm64-v1" in build_workflow
    assert 'MOD_BUILD_SECONDS_OVERRIDE: "14000"' in base_workflow
    assert "create_smoke_request.py" in base_workflow

    workflow = (ROOT / ".github/workflows/svc-client-check.yml").read_text()
    assert "native-mod-system.py upstream/horizon-078" in workflow
    assert "--mods mod" in workflow
    loader_patch = (ROOT / "patches/horizon-078/native-mod-system.py").read_text()
    assert "postDelayed(markNativeBuildStarted, 45000)" in loader_patch
    assert "MAX_STARTS = 2" in loader_patch

    sandbox = (ROOT / "native-build/scripts/sandbox_inner.sh").read_text()
    assert "unshare --net" in (ROOT / "native-build/scripts/mod_build_sandbox.sh").read_text()
    assert "CARGO_NET_OFFLINE=true" in sandbox
    assert 'env -i HOME=' in sandbox

    print("Native mod build layout, pinned manifests, and performance boundaries passed.")


if __name__ == "__main__":
    main()
