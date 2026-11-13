#!/usr/bin/env python3
"""One isolated Horizon native-mod build, called by the GitHub Actions worker."""
from __future__ import annotations
import json
import os
import pathlib
import shutil
import subprocess
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
ROOT = pathlib.Path.home() / "modbuild"
sys.path.insert(0, str(HERE))
from mod_build_server import check_compatibility, unpack_request


def main() -> None:
    if len(sys.argv) != 4:
        raise SystemExit("usage: mod_build_once.py REQUEST.bin HORIZON_COMMIT OUTPUT")
    request = pathlib.Path(sys.argv[1])
    commit = sys.argv[2]
    output = pathlib.Path(sys.argv[3])
    output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="horizon-mod-build-", dir=ROOT / "jobs") as temp:
        job = pathlib.Path(temp)
        mods = job / "mods"
        out = job / "out"
        out.mkdir()
        manifests = unpack_request(request.read_bytes(), mods)
        versions = json.loads((ROOT / "versions.json").read_text())
        check_compatibility(manifests, versions["0.7.8"])
        subprocess.run(
            ["bash", str(HERE / "mod_build_sandbox.sh"), str(ROOT), commit, str(job)],
            check=True,
            env=os.environ.copy(),
        )
        for name in ("libhorizon_android.so", "build.json"):
            source = out / name
            if not source.is_file() or source.stat().st_size == 0:
                raise SystemExit(f"native build did not produce {name}")
            shutil.copy2(source, output / name)
    metadata = json.loads((output / "build.json").read_text())
    if metadata.get("commit") != commit or metadata.get("abi") != "arm64-v8a":
        raise SystemExit("built library metadata does not match the requested target")


if __name__ == "__main__":
    main()
