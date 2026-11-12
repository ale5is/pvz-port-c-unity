#!/usr/bin/env python3
import argparse
import json
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

ROOT = Path(__file__).resolve().parents[1]
MOD = ROOT / "mod"
MANIFEST = MOD / "horizon-native.json"

def main():
    parser = argparse.ArgumentParser(description="Package the Horizon Port Pavo native mod as .hnmod")
    parser.add_argument("--output", type=Path, default=ROOT / "dist" / "simple-voice-chat-horizon-0.7.8.hnmod")
    args = parser.parse_args()

    native = json.loads(MANIFEST.read_text(encoding="utf-8"))
    mod_id = native["id"]
    output = args.output if args.output.is_absolute() else ROOT / args.output
    output.parent.mkdir(parents=True, exist_ok=True)

    entries = 0
    with ZipFile(output, "w", compression=ZIP_DEFLATED, compresslevel=9) as archive:
        archive.write(MANIFEST, "horizon-native.json")
        entries += 1
        for source in sorted(MOD.rglob("*")):
            if not source.is_file() or source == MANIFEST:
                continue
            relative = source.relative_to(MOD).as_posix()
            if relative.startswith("crates/svc_client/vendor/"):
                relative = "data/vendor/" + relative.split("vendor/", 1)[1]
            if relative == "README.md":
                pass
            archive.write(source, f"mixins/{mod_id}/{relative}")
            entries += 1

    print(f"Created {output} ({entries} files)")
    print("Opus C/H sources are packaged as bundle data and staged by Horizon under native-data/simple-voice-chat/vendor.")

if __name__ == "__main__":
    main()
