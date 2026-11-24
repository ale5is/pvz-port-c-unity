#!/usr/bin/env python3
"""Download every pinned Minecraft library artifact needed by Horizon's prepare step.

Horizon's launcher import filters artifacts by the runner's platform, while
horizon-prepare also inspects some unfiltered client artifacts. The version
manifest is pinned and SHA-1 checked by the workflow before this script runs.
"""
from concurrent.futures import ThreadPoolExecutor
import argparse
import hashlib
import json
from pathlib import Path
import tempfile
import urllib.request


def sha1(path: Path) -> str:
    digest = hashlib.sha1()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def download(item: tuple[str, Path, str]) -> None:
    url, destination, checksum = item
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.is_file() and sha1(destination) == checksum:
        return
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(dir=destination.parent, delete=False) as stream:
            temporary = Path(stream.name)
            request = urllib.request.Request(
                url,
                headers={"User-Agent": "PavoHorizon-CI/1.0"},
            )
            with urllib.request.urlopen(request, timeout=120) as response:
                while chunk := response.read(1024 * 1024):
                    stream.write(chunk)
        actual = sha1(temporary)
        if actual != checksum:
            raise ValueError(
                f"SHA-1 mismatch for {destination}: expected {checksum}, got {actual}"
            )
        temporary.replace(destination)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("manifest", type=Path)
    parser.add_argument("libraries_dir", type=Path)
    args = parser.parse_args()

    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    downloads = []
    for library in manifest.get("libraries", []):
        artifact = library.get("downloads", {}).get("artifact")
        if not artifact:
            continue
        relative = Path(artifact["path"])
        if relative.is_absolute() or ".." in relative.parts:
            raise ValueError(f"Unsafe library path in Mojang manifest: {artifact['path']}")
        downloads.append((
            artifact["url"],
            args.libraries_dir / relative,
            artifact["sha1"],
        ))

    with ThreadPoolExecutor(max_workers=8) as pool:
        list(pool.map(download, downloads))
    print(f"Verified {len(downloads)} Minecraft library artifacts for Horizon prepare.")


if __name__ == "__main__":
    main()
