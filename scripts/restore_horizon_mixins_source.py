#!/usr/bin/env python3
"""Restore the exact pinned Horizon Mixins source from its hash-verified mirror."""
from concurrent.futures import ThreadPoolExecutor
import argparse
import hashlib
import json
from pathlib import Path
import tempfile
import urllib.request

MIXINS_REPO = "https://github.com/horizon-rust/horizon-mixins"
MIXINS_REV = "a2af5c0dd4117d51ecd70066ba7a00b4a2c10dab"
MIRROR_REPO = "Freeze0102/FRE-LAUNCH"
MIRROR_REV = "12510e0e885d1250acfc164b8f9bb2e0d4aa2f6a"
MIRROR_PATH = "vendor/fre-mixins-source"


def fetch(url: str) -> bytes:
    request = urllib.request.Request(
        url,
        headers={"User-Agent": "PavoHorizon-CI/1.0"},
    )
    with urllib.request.urlopen(request, timeout=120) as response:
        return response.read()


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def restore(destination: Path) -> None:
    base = f"https://raw.githubusercontent.com/{MIRROR_REPO}/{MIRROR_REV}/{MIRROR_PATH}"
    metadata_bytes = fetch(f"{base}/FRE-VENDORED-SOURCE.json")
    metadata = json.loads(metadata_bytes)
    if metadata.get("repository") != MIXINS_REPO:
        raise ValueError("Vendored source points to a different upstream repository.")
    if metadata.get("commit") != MIXINS_REV or metadata.get("license") != "MIT":
        raise ValueError("Vendored source does not match the pinned MIT revision.")

    entries = []
    for relative, expected in metadata["files"].items():
        path = Path(relative)
        if path.is_absolute() or ".." in path.parts:
            raise ValueError(f"Unsafe vendored source path: {relative}")
        entries.append((relative, expected, destination / path))

    def restore_one(entry: tuple[str, str, Path]) -> None:
        relative, expected, target = entry
        url = f"{base}/{relative}"
        data = fetch(url)
        actual = sha256(data)
        if actual != expected:
            raise ValueError(
                f"SHA-256 mismatch for {relative}: expected {expected}, got {actual}"
            )
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.is_file() and sha256(target.read_bytes()) == expected:
            return
        with tempfile.NamedTemporaryFile(dir=target.parent, delete=False) as stream:
            temporary = Path(stream.name)
            stream.write(data)
        temporary.replace(target)

    with ThreadPoolExecutor(max_workers=8) as pool:
        list(pool.map(restore_one, entries))
    (destination / "FRE-VENDORED-SOURCE.json").write_bytes(metadata_bytes)
    print(
        f"Restored {len(entries)} hash-verified files from {MIRROR_REPO}@{MIRROR_REV}; "
        f"upstream pin {MIXINS_REV} (MIT)."
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    restore(args.out)


if __name__ == "__main__":
    main()
