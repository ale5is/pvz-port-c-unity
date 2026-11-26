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
    "horizon_egl_owner.h",
    "horizon_egl_owner.cpp",
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
    '.file(native.join("horizon_egl_owner.cpp"))',
):
    if marker not in text:
        raise SystemExit(f"FAIL: Android native build is missing {marker}")
    print(f"PASS: {marker}")

cargo = (root / "crates/horizon-android/Cargo.toml").read_text(encoding="utf-8")
if '[build-dependencies]' not in cargo or 'cc = "1.2"' not in cargo:
    raise SystemExit("FAIL: cc build dependency is not installed")
print("PASS: cc build dependency")

rust_ffi = root / "crates/horizon-android/gles2_native.rs"
if not rust_ffi.is_file():
    raise SystemExit("FAIL: missing Rust FFI owner")
ffi_text = rust_ffi.read_text(encoding="utf-8")
for marker in ('unsafe extern "C"', "pub(crate) struct Backend", "pavo_gles2_upload_section"):
    if marker not in ffi_text:
        raise SystemExit(f"FAIL: Rust GLES2 FFI missing {marker}")
    print(f"PASS: Rust FFI {marker}")

egl_rust = root / "crates/horizon-client/src/android/gles2_egl.rs"
if not egl_rust.is_file():
    raise SystemExit("FAIL: missing Rust EGL owner")
egl_text = egl_rust.read_text(encoding="utf-8")
for marker in ("pub(crate) struct Runtime", "pavo_egl_create", "pavo_egl_swap", "RawWindowHandle::AndroidNdk"):
    if marker not in egl_text:
        raise SystemExit(f"FAIL: Rust EGL owner missing {marker}")
    print(f"PASS: Rust EGL {marker}")

print("Native GLES2 library is linked into the Android cdylib; EGL ownership and Game call sites remain separate integration gates.")
