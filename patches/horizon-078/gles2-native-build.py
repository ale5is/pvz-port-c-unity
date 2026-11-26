#!/usr/bin/env python3
"""Install the reusable GLES2 C++ backend into Horizon's Android cdylib build."""
import argparse
from pathlib import Path
import shutil

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
repo = Path(__file__).resolve().parents[2]
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
    println!("cargo:rustc-link-arg=-Wl,--build-id=sha1");

    let root = PathBuf::from(env::var_os("CARGO_MANIFEST_DIR").expect("manifest directory"));
    let native = root.join("gles2-native");
    for file in [
        "gles2_terrain_renderer.h",
        "gles2_terrain_renderer.cpp",
        "horizon_terrain_adapter.h",
        "horizon_terrain_adapter.cpp",
        "horizon_gles2_bridge.h",
        "horizon_gles2_bridge.cpp",
        "horizon_egl_owner.h",
        "horizon_egl_owner.cpp",
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
upstream_build = '''fn main() {
    // Android tombstones can match a native frame to the exact private symbols.
    // This affects the final ELF only, not code generation or release optimization.
    if std::env::var("CARGO_CFG_TARGET_OS").as_deref() == Ok("android") {
        println!("cargo:rustc-link-arg=-Wl,--build-id=sha1");
    }
}
'''
if build_rs.exists():
    existing_build = build_rs.read_text(encoding="utf-8")
    if existing_build == upstream_build or existing_build == build_source:
        build_rs.write_text(build_source, encoding="utf-8")
    else:
        raise SystemExit("Existing horizon-android/build.rs differs from pinned upstream; refusing to overwrite")
else:
    build_rs.write_text(build_source, encoding="utf-8")

rust_module = root / "crates/horizon-android/gles2_native.rs"
rust_source = r'''use std::ffi::{CStr, c_char, c_void};
use std::marker::PhantomData;
use std::sync::Arc;
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
const _: () = assert!(std::mem::size_of::<HorizonTerrainVertex>() == 24);
const _: () = assert!(std::mem::offset_of!(HorizonTerrainVertex, position) == 0);
const _: () = assert!(std::mem::offset_of!(HorizonTerrainVertex, uv) == 12);
const _: () = assert!(std::mem::offset_of!(HorizonTerrainVertex, layer) == 16);
const _: () = assert!(std::mem::offset_of!(HorizonTerrainVertex, light) == 18);
const _: () = assert!(std::mem::offset_of!(HorizonTerrainVertex, color) == 20);

/// Copy Horizon's native SectionMesh vertices into the stable C ABI layout.
/// Mesh workers retain ownership of the source; the returned vectors can be
/// moved to the EGL owner thread without carrying wgpu or GL objects.
pub(crate) fn copy_section_vertices(
    source: &[horizon_client::mesher::TerrainVertex],
) -> Result<Vec<HorizonTerrainVertex>, String> {
    let mut output = Vec::new();
    output.try_reserve_exact(source.len())
        .map_err(|_| "Not enough memory to stage Horizon terrain vertices".to_owned())?;
    output.extend(source.iter().map(|vertex| HorizonTerrainVertex {
        position: vertex.pos,
        uv: vertex.uv().map(|value| (value * horizon_client_render::mesher::UV_STEPS).round() as u16),
        layer: vertex.layer,
        light: vertex.light,
        color: vertex.color,
    }));
    Ok(output)
}

pub(crate) fn visible_solid_ranges(
    sides: &horizon_client::mesher::Sides,
    eye: [f32; 3],
) -> Result<Vec<u32>, String> {
    horizon_client_render::gles2_terrain::visible_solid_triangle_ranges_packed(sides, eye)
        .map_err(str::to_owned)
}

unsafe extern "C" {
    fn pavo_gles2_create() -> *mut c_void;
    fn pavo_gles2_destroy(handle: *mut c_void);
    fn pavo_gles2_abandon(handle: *mut c_void);
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
    ) -> i32;
    fn pavo_gles2_update_lightmap(handle: *mut c_void, rgba: *const u8, byte_count: usize) -> i32;
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
    abandoned: bool,
    _not_send_or_sync: PhantomData<*mut ()>,
}

impl Backend {
    pub(crate) fn new() -> Result<Self, String> {
        let handle = NonNull::new(unsafe { pavo_gles2_create() })
            .ok_or_else(|| "Could not allocate the native GLES2 renderer".to_owned())?;
        Ok(Self {
            handle,
            owner: thread::current().id(),
            abandoned: false,
            _not_send_or_sync: PhantomData,
        })
    }

    pub(crate) fn abandon(&mut self) {
        if self.abandoned {
            return;
        }
        if thread::current().id() != self.owner {
            log::error!("Refusing to abandon GLES2 backend outside its EGL owner thread");
            return;
        }
        unsafe { pavo_gles2_abandon(self.handle.as_ptr()) };
        self.abandoned = true;
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
        let ok = unsafe {
            pavo_gles2_draw_sections(
                handle, mvp.as_ptr(), camera.as_ptr(), fog_color.as_ptr(),
                fog_range.as_ptr(), max_distance,
            )
        };
        if ok == 1 { Ok(()) } else { Err(self.native_error()) }
    }

    pub(crate) fn update_lightmap(&mut self, rgba: &[u8]) -> Result<(), String> {
        let handle = self.check_owner()?;
        let ok = unsafe { pavo_gles2_update_lightmap(handle, rgba.as_ptr(), rgba.len()) };
        if ok == 1 { Ok(()) } else { Err(self.native_error()) }
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
        if self.abandoned {
            return;
        }
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
module_decl = '#[path = "../../horizon-android/gles2_native.rs"]\npub(crate) mod gles2_native;\n'
if module_decl not in main_text:
    anchor_main = "mod game;\n"
    if main_text.count(anchor_main) != 1:
        raise SystemExit("Could not uniquely locate Android game module declaration")
    android_main.write_text(main_text.replace(anchor_main, anchor_main + module_decl), encoding="utf-8")

egl_module = root / "crates/horizon-client/src/android/gles2_egl.rs"
egl_source = r'''use raw_window_handle::{HasWindowHandle, RawWindowHandle};
use std::ffi::{CStr, c_char, c_void};
use std::ptr::NonNull;
use std::sync::Arc;
use std::thread::{self, ThreadId};
use winit::window::Window;

unsafe extern "C" {
    fn pavo_egl_create(native_window: *mut c_void) -> *mut c_void;
    fn pavo_egl_make_current(owner: *mut c_void) -> i32;
    fn pavo_egl_context_generation(owner: *const c_void) -> u64;
    fn pavo_egl_suspend(owner: *mut c_void) -> i32;
    fn pavo_egl_resume(owner: *mut c_void, native_window: *mut c_void) -> i32;
    fn pavo_egl_resize(owner: *mut c_void, width: *mut i32, height: *mut i32) -> i32;
    fn pavo_egl_begin_frame(owner: *mut c_void, r: f32, g: f32, b: f32, a: f32) -> i32;
    fn pavo_egl_swap(owner: *mut c_void) -> i32;
    fn pavo_egl_destroy(owner: *mut c_void);
    fn pavo_egl_last_error(owner: *const c_void) -> *const c_char;
}

struct EglOwner {
    handle: NonNull<c_void>,
    owner: ThreadId,
}

impl EglOwner {
    fn new(window: &Window) -> Result<Self, String> {
        let raw = window.window_handle().map_err(|error| error.to_string())?;
        let native_window = match raw.as_raw() {
            RawWindowHandle::AndroidNdk(handle) => handle.a_native_window.as_ptr(),
            _ => return Err("GLES2 EGL owner requires an Android native window".to_owned()),
        };
        let handle = NonNull::new(unsafe { pavo_egl_create(native_window) })
            .ok_or_else(|| "Could not create an EGL ES2 context and window surface".to_owned())?;
        Ok(Self { handle, owner: thread::current().id() })
    }

    fn check_thread(&self) -> Result<*mut c_void, String> {
        if self.owner != thread::current().id() {
            return Err("EGL operation attempted outside the Android render thread".to_owned());
        }
        Ok(self.handle.as_ptr())
    }

    fn error(&self) -> String {
        let pointer = unsafe { pavo_egl_last_error(self.handle.as_ptr()) };
        if pointer.is_null() {
            return "EGL owner failed without an error message".to_owned();
        }
        unsafe { CStr::from_ptr(pointer) }.to_string_lossy().into_owned()
    }

    fn generation(&self) -> Result<u64, String> {
        let handle = self.check_thread()?;
        Ok(unsafe { pavo_egl_context_generation(handle) })
    }

    fn suspend(&self) -> Result<(), String> {
        let handle = self.check_thread()?;
        if unsafe { pavo_egl_suspend(handle) } == 1 { Ok(()) } else { Err(self.error()) }
    }

    fn resume(&self, window: &Window) -> Result<u64, String> {
        let raw = window.window_handle().map_err(|error| error.to_string())?;
        let native_window = match raw.as_raw() {
            RawWindowHandle::AndroidNdk(handle) => handle.a_native_window.as_ptr(),
            _ => return Err("GLES2 EGL resume requires an Android native window".to_owned()),
        };
        let handle = self.check_thread()?;
        if unsafe { pavo_egl_resume(handle, native_window) } != 1 { return Err(self.error()); }
        self.generation()
    }

    fn make_current(&self) -> Result<(), String> {
        let handle = self.check_thread()?;
        if unsafe { pavo_egl_make_current(handle) } == 1 { Ok(()) } else { Err(self.error()) }
    }

    fn begin_frame(&self, clear: [f32; 4]) -> Result<(), String> {
        let handle = self.check_thread()?;
        let ok = unsafe { pavo_egl_begin_frame(handle, clear[0], clear[1], clear[2], clear[3]) };
        if ok == 1 { Ok(()) } else { Err(self.error()) }
    }

    fn resize(&self) -> Result<[u32; 2], String> {
        let handle = self.check_thread()?;
        let mut width = 0;
        let mut height = 0;
        let ok = unsafe { pavo_egl_resize(handle, &mut width, &mut height) };
        if ok != 1 { return Err(self.error()); }
        Ok([width as u32, height as u32])
    }

    fn present(&self) -> Result<(), String> {
        let handle = self.check_thread()?;
        if unsafe { pavo_egl_swap(handle) } == 1 { Ok(()) } else { Err(self.error()) }
    }
}

impl Drop for EglOwner {
    fn drop(&mut self) {
        if self.owner != thread::current().id() {
            log::error!("Refusing to destroy EGL owner outside its render thread");
            return;
        }
        unsafe { pavo_egl_destroy(self.handle.as_ptr()) };
    }
}

/// Owns one EGL context and one GLES2 renderer. Field order is intentional:
/// Rust drops the GL renderer first, while its EGL context is still current,
/// and only then destroys the EGL surface/context/display.
struct CachedSection {
    solid: Arc<[super::gles2_native::HorizonTerrainVertex]>,
    translucent: Arc<[super::gles2_native::HorizonTerrainVertex]>,
    ranges: Arc<[u32]>,
}

struct CachedAssets {
    sprite_pixels: Arc<[u8]>,

    layers: usize,
    sprite_size: i32,
    lightmap: Arc<[u8]>,

    lightmap_width: i32,
    lightmap_height: i32,
    uv_steps: f32,
    animated_layers: std::collections::HashMap<u32, Arc<[u8]>>,
}

pub(crate) struct Runtime {
    renderer: super::gles2_native::Backend,
    egl: EglOwner,
    context_generation: u64,
    surface_attached: bool,
    initialized: bool,
    cached_assets: Option<CachedAssets>,
    cached_sections: std::collections::HashMap<[i32; 3], CachedSection>,
}

impl Runtime {
    pub(crate) fn new(window: &Window) -> Result<Self, String> {
        let egl = EglOwner::new(window)?;
        egl.make_current()?;
        let renderer = super::gles2_native::Backend::new()?;
        let context_generation = egl.generation()?;
        Ok(Self {
            renderer, egl, context_generation, surface_attached: true, initialized: false,
            cached_assets: None, cached_sections: std::collections::HashMap::new(),
        })
    }

    pub(crate) fn initialize(
        &mut self, sprite_pixels: &[u8], layers: usize, sprite_size: i32,
        lightmap: &[u8], lightmap_width: i32, lightmap_height: i32, uv_steps: f32,
    ) -> Result<(), String> {
        let mut cached_sprites = Vec::new();
        cached_sprites.try_reserve_exact(sprite_pixels.len())
            .map_err(|_| "Not enough memory to retain GLES2 atlas for context recovery".to_owned())?;
        cached_sprites.extend_from_slice(sprite_pixels);
        let mut cached_lightmap = Vec::new();
        cached_lightmap.try_reserve_exact(lightmap.len())
            .map_err(|_| "Not enough memory to retain GLES2 lightmap for context recovery".to_owned())?;
        cached_lightmap.extend_from_slice(lightmap);
        let cached_assets = CachedAssets {
            sprite_pixels: Arc::from(cached_sprites), layers, sprite_size,
            lightmap: Arc::from(cached_lightmap), lightmap_width, lightmap_height, uv_steps,
            animated_layers: std::collections::HashMap::new(),
        };
        self.egl.make_current()?;
        self.renderer.initialize(sprite_pixels, layers, sprite_size,
            lightmap, lightmap_width, lightmap_height, uv_steps)?;
        self.cached_assets = Some(cached_assets);
        self.initialized = true;
        Ok(())
    }

    /// Release only the native window surface; retain the context and GPU objects when possible.
    pub(crate) fn suspend(&mut self) -> Result<(), String> {
        self.egl.suspend()?;
        self.surface_attached = false;
        Ok(())
    }

    /// Reattach to the resumed Android window. If EGL had to recreate its context,
    /// replace the invalid GL backend and require the caller to upload assets again.
    pub(crate) fn resume(&mut self, window: &Window) -> Result<bool, String> {
        let generation = self.egl.resume(window)?;
        self.surface_attached = true;
        let context_changed = generation != self.context_generation;
        if context_changed {
            self.renderer.abandon();
            self.renderer = super::gles2_native::Backend::new()?;
            self.context_generation = generation;
            self.initialized = false;
        }
        if self.initialized {
            return Ok(context_changed);
        }
        if let Some(assets) = self.cached_assets.as_ref() {
            let restore = (|| -> Result<(), String> {
                self.renderer.initialize(
                    &assets.sprite_pixels, assets.layers, assets.sprite_size,
                    &assets.lightmap, assets.lightmap_width, assets.lightmap_height, assets.uv_steps,
                )?;
                for (layer, pixels) in &assets.animated_layers {
                    self.renderer.update_atlas_layer(*layer, pixels)?;
                }
                for (key, section) in &self.cached_sections {
                    self.renderer.upload_section(
                        *key, &section.solid, &section.translucent, &section.ranges,
                    )?;
                }
                Ok(())
            })();
            if let Err(error) = restore {
                self.renderer = super::gles2_native::Backend::new()
                    .map_err(|reset| format!("{error}; additionally failed to reset GLES2 backend: {reset}"))?;
                return Err(error);
            }
            self.initialized = true;
        }
        Ok(context_changed)
    }

    pub(crate) fn is_initialized(&self) -> bool {
        self.initialized
    }

    pub(crate) fn begin_frame(&mut self, clear: [f32; 4]) -> Result<[u32; 2], String> {
        self.egl.begin_frame(clear)?;
        self.egl.resize()
    }

    pub(crate) fn upload_horizon_section(
        &mut self, key: [i32; 3],
        mesh: &horizon_client::mesher::SectionMesh,
        eye_in_section: [f32; 3],
    ) -> Result<(), String> {
        let solid = super::gles2_native::copy_section_vertices(&mesh.solid)?;
        let translucent = super::gles2_native::copy_section_vertices(&mesh.translucent)?;
        let ranges = super::gles2_native::visible_solid_ranges(&mesh.sides, eye_in_section)?;
        self.upload_section(key, &solid, &translucent, &ranges)
    }

    pub(crate) fn upload_section(
        &mut self, key: [i32; 3],
        solid: &[super::gles2_native::HorizonTerrainVertex],
        translucent: &[super::gles2_native::HorizonTerrainVertex],
        ranges: &[u32],
    ) -> Result<(), String> {
        if !self.initialized { return Err("GLES2 assets must be reinitialized after EGL context loss".to_owned()); }
        let mut cached_solid = Vec::new();
        cached_solid.try_reserve_exact(solid.len())
            .map_err(|_| "Not enough memory to retain GLES2 solid section".to_owned())?;
        cached_solid.extend_from_slice(solid);
        let mut cached_translucent = Vec::new();
        cached_translucent.try_reserve_exact(translucent.len())
            .map_err(|_| "Not enough memory to retain GLES2 translucent section".to_owned())?;
        cached_translucent.extend_from_slice(translucent);
        let mut cached_ranges = Vec::new();
        cached_ranges.try_reserve_exact(ranges.len())
            .map_err(|_| "Not enough memory to retain GLES2 section ranges".to_owned())?;
        cached_ranges.extend_from_slice(ranges);
        let cached = CachedSection {
            solid: Arc::from(cached_solid),
            translucent: Arc::from(cached_translucent),
            ranges: Arc::from(cached_ranges),
        };
        self.egl.make_current()?;
        self.renderer.upload_section(key, solid, translucent, ranges)?;
        self.cached_sections.insert(key, cached);
        Ok(())
    }

    pub(crate) fn remove_column(&mut self, x: i32, z: i32) -> Result<(), String> {
        if !self.initialized { return Err("GLES2 assets must be reinitialized after EGL context loss".to_owned()); }
        self.egl.make_current()?;
        self.renderer.remove_column(x, z)?;
        self.cached_sections.retain(|key, _| key[0] != x || key[2] != z);
        Ok(())
    }

    pub(crate) fn clear_sections(&mut self) -> Result<(), String> {
        if !self.initialized { return Err("GLES2 assets must be reinitialized after EGL context loss".to_owned()); }
        self.egl.make_current()?;
        self.renderer.clear_sections()?;
        self.cached_sections.clear();
        Ok(())
    }

    pub(crate) fn draw_sections(
        &mut self, mvp: &[f32; 16], camera: &[f32; 3],
        fog_color: &[f32; 4], fog_range: &[f32; 2], max_distance: f32,
    ) -> Result<(), String> {
        if !self.initialized { return Err("GLES2 assets must be reinitialized after EGL context loss".to_owned()); }
        self.egl.make_current()?;
        self.renderer.draw_sections(mvp, camera, fog_color, fog_range, max_distance)
    }

    pub(crate) fn present(&mut self) -> Result<(), String> {
        self.egl.present()
    }

    pub(crate) fn update_lightmap(&mut self, rgba: &[u8]) -> Result<(), String> {
        if !self.initialized { return Err("GLES2 assets must be reinitialized after EGL context loss".to_owned()); }
        let mut replacement = Vec::new();
        replacement.try_reserve_exact(rgba.len())
            .map_err(|_| "Not enough memory to retain updated GLES2 lightmap".to_owned())?;
        replacement.extend_from_slice(rgba);
        let replacement = Arc::from(replacement);
        self.egl.make_current()?;
        self.renderer.update_lightmap(rgba)?;
        if let Some(assets) = self.cached_assets.as_mut() {
            assets.lightmap = replacement;
        }
        Ok(())
    }

    pub(crate) fn update_atlas_layer(&mut self, layer: u32, rgba: &[u8]) -> Result<(), String> {
        if !self.initialized { return Err("GLES2 assets must be reinitialized after EGL context loss".to_owned()); }
        let mut replacement = Vec::new();
        replacement.try_reserve_exact(rgba.len())
            .map_err(|_| "Not enough memory to retain animated GLES2 atlas layer".to_owned())?;
        replacement.extend_from_slice(rgba);
        let replacement = Arc::from(replacement);
        self.egl.make_current()?;
        self.renderer.update_atlas_layer(layer, rgba)?;
        if let Some(assets) = self.cached_assets.as_mut() {
            assets.animated_layers.insert(layer, replacement);
        }
        Ok(())
    }
}

impl Drop for Runtime {
    fn drop(&mut self) {
        // Backend::drop issues GL deletes. They are valid only while this EGL
        // context is current; after suspend or context loss, abandon the GL
        // names and let eglDestroyContext reclaim them instead.
        if !self.surface_attached || self.egl.make_current().is_err() {
            self.renderer.abandon();
        }
    }
}
'''
if egl_module.exists() and egl_module.read_text(encoding="utf-8") != egl_source:
    raise SystemExit("Existing android/gles2_egl.rs differs; refusing to overwrite")
if not egl_module.exists():
    egl_module.write_text(egl_source, encoding="utf-8")

render_lib = root / "crates/horizon-client/src/android_main.rs"
render_text = render_lib.read_text(encoding="utf-8")
egl_decl = '#[path = "android/gles2_egl.rs"]\npub(crate) mod gles2_egl;\n'
if egl_decl not in render_text:
    anchor_decl = '#[path = "../../horizon-android/gles2_native.rs"]\npub(crate) mod gles2_native;\n'
    if render_text.count(anchor_decl) != 1:
        raise SystemExit("Could not uniquely locate gles2_native module declaration")
    render_lib.write_text(render_text.replace(anchor_decl, anchor_decl + egl_decl), encoding="utf-8")

android_cargo = root / "crates/horizon-android/Cargo.toml"
android_cargo_text = android_cargo.read_text(encoding="utf-8")
if 'raw-window-handle = "0.6"' not in android_cargo_text:
    marker = 'winit = { version = "0.30.13", features = ["android-native-activity"] }\n'
    if android_cargo_text.count(marker) != 1:
        raise SystemExit("Could not uniquely locate winit dependency for raw-window-handle")
    android_cargo.write_text(android_cargo_text.replace(marker, marker + 'raw-window-handle = "0.6"\n'), encoding="utf-8")
print("Installed reusable GLES2 native sources and Android ARMv7 C++ build integration.")
