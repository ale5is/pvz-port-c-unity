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
for marker in ('unsafe extern "C"', "pub(crate) struct Backend", "pavo_gles2_upload_section", "pavo_gles2_update_lightmap", "copy_section_vertices", "visible_solid_triangle_ranges_packed"):
    if marker not in ffi_text:
        raise SystemExit(f"FAIL: Rust GLES2 FFI missing {marker}")
    print(f"PASS: Rust FFI {marker}")

egl_rust = root / "crates/horizon-client/src/android/gles2_egl.rs"
if not egl_rust.is_file():
    raise SystemExit("FAIL: missing Rust EGL owner")
egl_text = egl_rust.read_text(encoding="utf-8")
for marker in ("pub(crate) struct Runtime", "pavo_egl_create", "pavo_egl_swap", "pavo_egl_suspend", "pavo_egl_resume", "pavo_egl_context_generation", "pub(crate) fn suspend", "pub(crate) fn resume", "pub(crate) fn clear_sections", "context_generation", "initialized: bool", "RawWindowHandle::AndroidNdk"):
    if marker not in egl_text:
        raise SystemExit(f"FAIL: Rust EGL owner missing {marker}")
    print(f"PASS: Rust EGL {marker}")

runtime_resume = egl_text.split("pub(crate) fn resume", 1)[1].split("pub(crate) fn is_initialized", 1)[0]
resume_order = ("let generation = self.egl.resume(window)?;", "if generation == self.context_generation", "self.renderer = super::gles2_native::Backend::new()?;", "self.context_generation = generation;", "self.initialized = false;")
for marker in ("cached_assets: Option<CachedAssets>", "cached_sections: std::collections::HashMap", "sprite_pixels: sprite_pixels.to_vec()", "solid: solid.to_vec()", "self.cached_sections.retain", "for (key, section) in &self.cached_sections", "self.initialized = true;"):
    if marker not in egl_text:
        raise SystemExit(f"FAIL: GLES2 context recovery does not retain/replay CPU data: {marker}")
    print(f"PASS: context recovery cache {marker}")

resume_positions = [runtime_resume.find(marker) for marker in resume_order]
if any(position < 0 for position in resume_positions) or resume_positions != sorted(resume_positions):
    raise SystemExit("FAIL: Runtime does not invalidate and replace GLES2 resources in context-loss order")
initialize_body = egl_text.split("pub(crate) fn initialize", 1)[1].split("pub(crate) fn suspend", 1)[0]
if initialize_body.find("self.renderer.initialize") < 0 or initialize_body.find("self.initialized = true;") < initialize_body.find("self.renderer.initialize"):
    raise SystemExit("FAIL: Runtime marks GLES2 initialized before native asset upload succeeds")
print("PASS: Rust runtime context-loss invalidation and reinitialization order")

egl_header = (Path(__file__).resolve().parents[1] / "tools/gles2-hud-prototype/app/src/main/cpp/horizon_egl_owner.h").read_text(encoding="utf-8")
egl_cpp = (Path(__file__).resolve().parents[1] / "tools/gles2-hud-prototype/app/src/main/cpp/horizon_egl_owner.cpp").read_text(encoding="utf-8")
bridge_header = (Path(__file__).resolve().parents[1] / "tools/gles2-hud-prototype/app/src/main/cpp/horizon_gles2_bridge.h").read_text(encoding="utf-8")
bridge_cpp = (Path(__file__).resolve().parents[1] / "tools/gles2-hud-prototype/app/src/main/cpp/horizon_gles2_bridge.cpp").read_text(encoding="utf-8")
if "int pavo_gles2_draw_sections(" not in bridge_header or "return 1;" not in bridge_cpp:
    raise SystemExit("FAIL: GLES2 terrain draw status is not propagated through the native bridge")
rust_native_text = (root / "crates/horizon-android/gles2_native.rs").read_text(encoding="utf-8")
draw_ffi = rust_native_text.split("fn pavo_gles2_draw_sections", 1)[1].split(";", 1)[0]
if "-> i32" not in draw_ffi:
    raise SystemExit("FAIL: Rust GLES2 FFI does not receive native draw status")
if "if ok == 1 { Ok(()) } else { Err(self.native_error()) }" not in rust_native_text:
    raise SystemExit("FAIL: Rust terrain draw wrapper discards native draw errors")
print("PASS: terrain draw status crosses C++ and Rust FFI")

for marker in ("pavo_egl_context_generation", "pavo_egl_suspend", "pavo_egl_resume"):
    if marker not in egl_header or marker not in egl_cpp:
        raise SystemExit(f"FAIL: native EGL lifecycle is missing {marker}")
    print(f"PASS: native EGL lifecycle {marker}")
if "++owner->context_generation" not in egl_cpp:
    raise SystemExit("FAIL: EGL context generation is not advanced after context recreation")
if "bool context_recreated = owner->context == EGL_NO_CONTEXT;" not in egl_cpp:
    raise SystemExit("FAIL: EGL resume cannot recover from a prior failed context recreation")
print("PASS: EGL context-loss generation increment and failed-recreate retry")

print("Native GLES2 library is linked; EGL surface resume and context-generation recovery are exposed. Game call-site integration remains separate.")
