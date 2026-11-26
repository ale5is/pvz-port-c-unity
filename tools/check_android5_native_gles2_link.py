#!/usr/bin/env python3
"""Check that Horizon's Android cdylib links the reusable GLES2 backend."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_native_gles2_link.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
native = root / "crates/horizon-android/gles2-native"
required = (
    "gles2_terrain_renderer.h",
    "gles2_terrain_renderer.cpp",
    "horizon_terrain_adapter.h",
    "horizon_terrain_adapter.cpp",
    "horizon_gles2_bridge.h",
    "horizon_gles2_bridge.cpp",
)
for name in required:
    if not (native / name).is_file():
        raise SystemExit(f"FAIL: missing linked native source {name}")
    print(f"PASS: native source {name}")

build = root / "crates/horizon-android/build.rs"
if not build.is_file():
    raise SystemExit("FAIL: missing horizon-android/build.rs")
text = build.read_text(encoding="utf-8")
for marker in (
    'target != "android"',
    '.cpp(true)',
    '.file(native.join("horizon_gles2_bridge.cpp"))',
    'cargo:rustc-link-lib=EGL',
    'cargo:rustc-link-lib=GLESv2',
):
    if marker not in text:
        raise SystemExit(f"FAIL: Android native build is missing {marker}")
    print(f"PASS: {marker}")

cargo = (root / "crates/horizon-android/Cargo.toml").read_text(encoding="utf-8")
if '[build-dependencies]' not in cargo or 'cc = "1.2"' not in cargo:
    raise SystemExit("FAIL: cc build dependency is not installed")
print("PASS: cc build dependency")
print("Native GLES2 library is linked into the Android cdylib; EGL ownership and Game call sites remain separate integration gates.")
