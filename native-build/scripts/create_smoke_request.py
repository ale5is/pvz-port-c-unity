#!/usr/bin/env python3
from __future__ import annotations
import json
import pathlib
import sys
import zipfile

HORIZON = "68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29"
MIXINS = "a2af5c0dd4117d51ecd70066ba7a00b4a2c10dab"
MOD_ID = "native_build_smoke"


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("usage: create_smoke_request.py OUTPUT")
    output = pathlib.Path(sys.argv[1])
    output.parent.mkdir(parents=True, exist_ok=True)
    bundle_path = output.with_suffix(".hnmod")
    manifest = {
        "schema": 1,
        "id": MOD_ID,
        "version": "1.0.0",
        "game": "26.3",
        "horizon": HORIZON,
        "mixins": MIXINS,
        "hooks": 80,
    }
    mixin = (
        '[mod]\nid = "' + MOD_ID + '"\nversion = "1.0.0"\npriority = 1\n'
        '[compatibility]\nhorizon_revision = "' + HORIZON + '"\n'
        '[[injection]]\nid = "smoke_mod_list"\nkind = "HEAD"\n'
        'hook = "mods"\nhook_version = 1\nhelper = "mods.rs"\n'
        'bindings = [\n'
        '  { name = "game", ty = "&mut crate::game::Game<\'_>", value = "&mut *self" },\n'
        '  { name = "mods", ty = "&mut Vec<crate::game::native_hooks::NativeMod>", value = "&mut *mods" },\n'
        ']\n'
    )
    with zipfile.ZipFile(bundle_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("horizon-native.json", json.dumps(manifest, separators=(",", ":")))
        archive.writestr("mixins/" + MOD_ID + "/horizon-mixin.toml", mixin)
        archive.writestr(
            "mixins/" + MOD_ID + "/mods.rs",
            """fn smoke_mod_list(
    _game: &mut crate::game::Game<'_>,
    _mods: &mut Vec<crate::game::native_hooks::NativeMod>,
) {}
""",
        )
    data = bundle_path.read_bytes()
    output.write_bytes(len(data).to_bytes(4, "big") + data)
    bundle_path.unlink()
    print(f"created {MOD_ID} request: {output.stat().st_size} bytes")


if __name__ == "__main__":
    main()
