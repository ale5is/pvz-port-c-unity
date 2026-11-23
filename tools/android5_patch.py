#!/usr/bin/env python3
"""Apply the first, deliberately limited Android 5 diagnostic patch to Horizon 0.7.8."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()

def replace_once(path: Path, old: str, new: str, label: str) -> None:
    source = path.read_text(encoding="utf-8")
    count = source.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    path.write_text(source.replace(old, new), encoding="utf-8")

provider = root / "packaging/android/java/dev/horizon/android/HorizonDocumentsProvider.java"
old_provider = """    @Override public DocumentsContract.Path findDocumentPath(String parent, String child)
            throws FileNotFoundException {
        try {
            JSONArray ids = new JSONArray(call("path", child, parent == null ? "" : parent));
            java.util.ArrayList<String> path = new java.util.ArrayList<>();
            for (int i = 0; i < ids.length(); i++) path.add(ids.getString(i));
            return new DocumentsContract.Path(parent == null ? "horizon" : null, path);
        } catch (JSONException error) { throw new FileNotFoundException("Invalid document path"); }
    }
"""
replace_once(provider, old_provider, "", "Remove API-26 DocumentsProvider.findDocumentPath")

build = root / "tools/android_build.py"
replace_once(
    build,
    "    min_sdk = 27 if legacy else 28\n",
    "    min_sdk = 22 if args.abi == 'armeabi-v7a' else (27 if legacy else 28)\n",
    "Set ARMv7 native API floor to 22",
)
replace_once(
    build,
    "    features = ['--no-default-features', '--features', 'audio'] if legacy else []\n",
    "    features = ['--no-default-features'] if args.abi == 'armeabi-v7a' else (['--no-default-features', '--features', 'audio'] if legacy else [])\n",
    "Disable audio and optional mods in ARMv7 API-22 diagnostic",
)

manifest = root / "packaging/android/AndroidManifest.xml"
replace_once(
    manifest,
    '<uses-feature android:glEsVersion="0x00030000" android:required="true" />',
    '<uses-feature android:glEsVersion="0x00020000" android:required="false" />',
    "Allow installation on GLES2 for diagnostic only",
)
print("Applied Android 5 API-22 diagnostic patch (SAF API 26 removed; audio disabled; GLES2 requirement non-blocking).")
