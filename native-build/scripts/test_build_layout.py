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

    build = (ROOT / "native-build/scripts/native_build.sh").read_text()
    prepare = (ROOT / "native-build/scripts/prepare.sh").read_text()
    assert 'cp -r "$HOME/builtin/simple-voice-chat"' in build
    assert 'cp -r "$HOME/builtin/native-mod-builder"' in build
    assert "native-mod-builder|horizon-native-builder)" in build
    assert '"$work/mods/horizon-native-builder"' in build
    assert 'cp -r "$repo_root/mod/native-mod-builder"' in prepare

    sandbox = (ROOT / "native-build/scripts/sandbox_inner.sh").read_text()
    assert "unshare --net" in (ROOT / "native-build/scripts/mod_build_sandbox.sh").read_text()
    assert "CARGO_NET_OFFLINE=true" in sandbox
    assert 'env -i HOME=' in sandbox

    print("Native mod build layout, pinned manifests, and performance boundaries passed.")


if __name__ == "__main__":
    main()
