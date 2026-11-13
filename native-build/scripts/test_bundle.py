#!/usr/bin/env python3
from __future__ import annotations
import io
import json
import pathlib
import sys
import tempfile
import zipfile

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from mod_build_server import decode_bundle, unpack_request

HORIZON = "68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29"
MIXINS = "a2af5c0dd4117d51ecd70066ba7a00b4a2c10dab"
MANIFEST = {
    "schema": 1, "id": "test_mod", "version": "1.0.0", "game": "26.3",
    "horizon": HORIZON, "mixins": MIXINS, "hooks": 80,
}
MIXIN = (
    '[mod]\nid = "test_mod"\nversion = "1.0.0"\n'
    '[compatibility]\nhorizon_revision = "' + HORIZON + '"\n'
)


def archive(extra: tuple[str, bytes] | None = None) -> bytes:
    out = io.BytesIO()
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("horizon-native.json", json.dumps(MANIFEST))
        z.writestr("mixins/test_mod/horizon-mixin.toml", MIXIN)
        z.writestr("mixins/test_mod/src/lib.rs", b"pub fn test() {}")
        if extra:
            z.writestr(extra[0], extra[1])
    return out.getvalue()


def main() -> None:
    valid = archive()
    mod_id, files, manifest = decode_bundle(valid)
    assert mod_id == "test_mod"
    assert manifest["horizon"] == HORIZON
    assert "mixins/test_mod/src/lib.rs" in files
    with tempfile.TemporaryDirectory() as temp:
        body = len(valid).to_bytes(4, "big") + valid
        result = unpack_request(body, pathlib.Path(temp))
        assert result[0]["id"] == "test_mod"
        assert (pathlib.Path(temp) / "test_mod/horizon-mixin.toml").is_file()
    try:
        decode_bundle(archive(("mixins/test_mod/../../escape.rs", b"bad")))
    except ValueError:
        pass
    else:
        raise AssertionError("path traversal bundle was accepted")
    try:
        unpack_request(b"\x00\x00\x00\x20short", pathlib.Path(tempfile.mkdtemp()))
    except ValueError:
        pass
    else:
        raise AssertionError("truncated request was accepted")
    print("native .hnmod validation tests passed")


if __name__ == "__main__":
    main()
