#!/usr/bin/env python3
"""Install the reusable GLES2 C++ backend into Horizon's Android cdylib build."""
import argparse
from pathlib import Path
import shutil

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
repo = Path(__file__).resolve().parents[1]
cpp_source = repo / "tools/gles2-hud-prototype/app/src/main/cpp"
destination = root / "crates/horizon-android/gles2-native"
destination.mkdir(parents=True, exist_ok=True)

for name in (
    "gles2_terrain_renderer.h",
    "gles2_terrain_renderer.cpp",
    "horizon_terrain_adapter.h",
    "horizon_terrain_adapter.cpp",
    "horizon_gles2_bridge.h",
    "horizon_gles2_bridge.cpp",
    "horizon_egl_owner.h",
    "horizon_egl_owner.cpp",
):
    source_file = cpp_source / name
    if not source_file.is_file():
        raise SystemExit(f"Missing native GLES2 source: {source_file}")
    target = destination / name
    content = source_file.read_bytes()
    if target.exists() and target.read_bytes() != content:
        target.write_bytes(content)
    elif not target.exists():
        target.write_bytes(content)

cargo = root / "crates/horizon-android/Cargo.toml"
cargo_text = cargo.read_text(encoding="utf-8")
build_dep = '[build-dependencies]\ncc = "1.2"\n'
if "[build-dependencies]" not in cargo_text:
    cargo.write_text(cargo_text.rstrip() + "\n\n" + build_dep, encoding="utf-8")
elif 'cc = "1.2"' not in cargo_text:
    raise SystemExit("horizon-android already has build-dependencies without the expected cc pin")

build_rs = root / "crates/horizon-android/build.rs"
build_source = r'''use std::env;
use std::path::PathBuf;

fn main() {
    println!("cargo:rerun-if-changed=build.rs");
    let target = env::var("CARGO_CFG_TARGET_OS").unwrap_or_default();
    if target != "android" {
        return;
    }

    let root = PathBuf::from(env::var_os("CARGO_MANIFEST_DIR").expect("manifest directory"));
    let native = root.join("gles2-native");
    for file in [
        "gles2_terrain_renderer.h",
        "gles2_terrain_renderer.cpp",
        "horizon_terrain_adapter.h",
        "horizon_terrain_adapter.cpp",
        "horizon_gles2_bridge.h",
        "horizon_gles2_bridge.cpp",
    ] {
        println!("cargo:rerun-if-changed={}", native.join(file).display());
    }

    cc::Build::new()
        .cpp(true)
        .std("c++17")
        .include(&native)
        .file(native.join("gles2_terrain_renderer.cpp"))
        .file(native.join("horizon_terrain_adapter.cpp"))
        .file(native.join("horizon_gles2_bridge.cpp"))
        .file(native.join("horizon_egl_owner.cpp"))
        .compile("pavo_horizon_gles2");

    println!("cargo:rustc-link-lib=EGL");
    println!("cargo:rustc-link-lib=GLESv2");
    println!("cargo:rustc-link-lib=android");
    println!("cargo:rustc-link-lib=log");
}
'''
if build_rs.exists() and build_rs.read_text(encoding="utf-8") != build_source:
    raise SystemExit("Existing horizon-android/build.rs differs; refusing to overwrite")
if not build_rs.exists():
    build_rs.write_text(build_source, encoding="utf-8")

rust_module = root / "crates/horizon-android/gles2_native.rs"
rust_source = r'''use std::ffi::{CStr, c_char, c_void};
use std::marker::PhantomData;
use std::ptr::NonNull;
use std::thread::{self, ThreadId};

#[repr(C)]
#[derive(Clone, Copy)]
pub(crate) struct HorizonTerrainVertex {
    pub position: [f32; 3],
    pub uv: [u16; 2],
    pub layer: u16,
    pub light: u16,
    pub color: u32,
}

unsafe extern "C" {
    fn pavo_gles2_create() -> *mut c_void;
    fn pavo_gles2_destroy(handle: *mut c_void);
    fn pavo_gles2_initialize(
        handle: *mut c_void, sprite_pixels: *const u8, sprite_bytes: usize,
        layers: usize, sprite_size: i32, lightmap: *const u8,
        lightmap_width: i32, lightmap_height: i32, uv_steps: f32,
    ) -> i32;
    fn pavo_gles2_upload_section(
        handle: *mut c_void, key: *const i32,
        solid: *const HorizonTerrainVertex, solid_count: usize,
        translucent: *const HorizonTerrainVertex, translucent_count: usize,
        ranges: *const u32, range_count: usize,
    ) -> i32;
    fn pavo_gles2_remove_column(handle: *mut c_void, x: i32, z: i32);
    fn pavo_gles2_clear_sections(handle: *mut c_void);
    fn pavo_gles2_draw_sections(
        handle: *mut c_void, mvp: *const f32, camera: *const f32,
        fog_color: *const f32, fog_range: *const f32, max_distance: f32,
    );
    fn pavo_gles2_update_atlas_layer(
        handle: *mut c_void, layer: u32, rgba: *const u8, byte_count: usize,
    ) -> i32;
    fn pavo_gles2_last_error(handle: *const c_void) -> *const c_char;
}

/// Thin safe owner for the C++ GLES2 backend. Every method must be called on
/// the thread that owns the current EGL context; this is checked before FFI.
pub(crate) struct Backend {
    handle: NonNull<c_void>,
    owner: ThreadId,
    _not_send_or_sync: PhantomData<*mut ()>,
}

impl Backend {
    pub(crate) fn new() -> Result<Self, String> {
        let handle = NonNull::new(unsafe { pavo_gles2_create() })
            .ok_or_else(|| "Could not allocate the native GLES2 renderer".to_owned())?;
        Ok(Self {
            handle,
            owner: thread::current().id(),
            _not_send_or_sync: PhantomData,
        })
    }

    fn check_owner(&self) -> Result<*mut c_void, String> {
        if thread::current().id() != self.owner {
            return Err("GLES2 backend called outside its EGL owner thread".to_owned());
        }
        Ok(self.handle.as_ptr())
    }

    fn native_error(&self) -> String {
        let pointer = unsafe { pavo_gles2_last_error(self.handle.as_ptr()) };
        if pointer.is_null() {
            return "Native GLES2 operation failed without an error message".to_owned();
        }
        unsafe { CStr::from_ptr(pointer) }.to_string_lossy().into_owned()
    }

    pub(crate) fn initialize(
        &mut self, sprite_pixels: &[u8], layers: usize, sprite_size: i32,
        lightmap: &[u8], lightmap_width: i32, lightmap_height: i32, uv_steps: f32,
    ) -> Result<(), String> {
        let handle = self.check_owner()?;
        let expected = (lightmap_width as usize)
            .checked_mul(lightmap_height as usize)
            .and_then(|pixels| pixels.checked_mul(4))
            .ok_or_else(|| "Lightmap dimensions overflow".to_owned())?;
        if lightmap_width <= 0 || lightmap_height <= 0 || lightmap.len() != expected {
            return Err("Lightmap must contain exactly width × height × 4 RGBA bytes".to_owned());
        }
        let ok = unsafe {
            pavo_gles2_initialize(
                handle, sprite_pixels.as_ptr(), sprite_pixels.len(), layers,
                sprite_size, lightmap.as_ptr(), lightmap_width, lightmap_height, uv_steps,
            )
        };
        if ok == 1 { Ok(()) } else { Err(self.native_error()) }
    }

    pub(crate) fn upload_section(
        &mut self, key: [i32; 3], solid: &[HorizonTerrainVertex],
        translucent: &[HorizonTerrainVertex], ranges: &[u32],
    ) -> Result<(), String> {
        let handle = self.check_owner()?;
        if ranges.len() % 2 != 0 {
            return Err("Solid face ranges must contain first/count pairs".to_owned());
        }
        let ok = unsafe {
            pavo_gles2_upload_section(
                handle, key.as_ptr(), solid.as_ptr(), solid.len(),
                translucent.as_ptr(), translucent.len(), ranges.as_ptr(), ranges.len() / 2,
            )
        };
        if ok == 1 { Ok(()) } else { Err(self.native_error()) }
    }

    pub(crate) fn remove_column(&mut self, x: i32, z: i32) -> Result<(), String> {
        let handle = self.check_owner()?;
        unsafe { pavo_gles2_remove_column(handle, x, z) };
        Ok(())
    }

    pub(crate) fn clear_sections(&mut self) -> Result<(), String> {
        let handle = self.check_owner()?;
        unsafe { pavo_gles2_clear_sections(handle) };
        Ok(())
    }

    pub(crate) fn draw_sections(
        &mut self, mvp: &[f32; 16], camera: &[f32; 3],
        fog_color: &[f32; 4], fog_range: &[f32; 2], max_distance: f32,
    ) -> Result<(), String> {
        let handle = self.check_owner()?;
        unsafe {
            pavo_gles2_draw_sections(
                handle, mvp.as_ptr(), camera.as_ptr(), fog_color.as_ptr(),
                fog_range.as_ptr(), max_distance,
            )
        };
        Ok(())
    }

    pub(crate) fn update_atlas_layer(&mut self, layer: u32, rgba: &[u8]) -> Result<(), String> {
        let handle = self.check_owner()?;
        let ok = unsafe {
            pavo_gles2_update_atlas_layer(handle, layer, rgba.as_ptr(), rgba.len())
        };
        if ok == 1 { Ok(()) } else { Err(self.native_error()) }
    }
}

impl Drop for Backend {
    fn drop(&mut self) {
        if thread::current().id() != self.owner {
            log::error!("Refusing to destroy GLES2 backend outside its EGL owner thread");
            return;
        }
        unsafe { pavo_gles2_destroy(self.handle.as_ptr()) };
    }
}
'''
if rust_module.exists() and rust_module.read_text(encoding="utf-8") != rust_source:
    raise SystemExit("Existing gles2_native.rs differs; refusing to overwrite")
if not rust_module.exists():
    rust_module.write_text(rust_source, encoding="utf-8")

android_main = root / "crates/horizon-client/src/android_main.rs"
main_text = android_main.read_text(encoding="utf-8")
module_decl = "mod gles2_native;\n"
if module_decl not in main_text:
    anchor_main = "mod game;\n"
    if main_text.count(anchor_main) != 1:
        raise SystemExit("Could not uniquely locate Android game module declaration")
    android_main.write_text(main_text.replace(anchor_main, anchor_main + module_decl), encoding="utf-8")
print("Installed reusable GLES2 native sources and Android ARMv7 C++ build integration.")
