#!/usr/bin/env python3
"""Guard Android EGL lifecycle assumptions against the pinned Horizon source."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_egl_lifecycle.py <horizon-source-root>")

root = Path(sys.argv[1]).resolve()


def read(relative: str) -> str:
    path = root / relative
    if not path.is_file():
        raise SystemExit(f"FAIL: missing upstream source: {relative}")
    return path.read_text(encoding="utf-8")


def require(source: str, label: str, markers: tuple[str, ...]) -> None:
    missing = [marker for marker in markers if marker not in source]
    if missing:
        raise SystemExit(f"FAIL: {label}: missing {missing}")
    print(f"PASS: {label}")


def require_order(source: str, label: str, markers: tuple[str, ...]) -> None:
    positions = [source.find(marker) for marker in markers]
    if any(position < 0 for position in positions):
        missing = [marker for marker, position in zip(markers, positions) if position < 0]
        raise SystemExit(f"FAIL: {label}: missing {missing}")
    if positions != sorted(positions):
        raise SystemExit(f"FAIL: {label}: lifecycle order changed")
    print(f"PASS: {label}")


game = read("crates/horizon-client/src/game.rs")
launcher = read("crates/horizon-client/src/android/launcher.rs")
entry = read("crates/horizon-client/src/android_main.rs")

require(
    entry,
    "Android entry point owns one winit launcher event loop",
    (
        "launcher::Launcher::new(home.clone())",
        "events.run_app(&mut launcher)",
    ),
)
require_order(
    launcher,
    "Launcher constructs its loading renderer before publishing ownership",
    (
        "game::loading_renderer(",
        "renderer.set_loading_ui(&gui.atlas, gui.panorama.as_ref())?",
        "self.renderer = Some(renderer);",
    ),
)
require(
    launcher,
    "Launcher resume recreates the existing surface before resizing",
    (
        "renderer.recreate_surface()?",
        "let size = window.inner_size();",
        "renderer.resize(size.width, size.height);",
    ),
)
require(
    launcher,
    "Launcher suspension stops its renderer surface",
    ("renderer.suspend_surface();",),
)
require(
    game,
    "Game suspension preserves input/pause handling and suspends rendering",
    (
        "self.surface_lost = true;",
        "self.android_release_touch_keys(true);",
        "self.sync_server_pause();",
        "renderer.suspend_surface();",
    ),
)
require(
    game,
    "Game resume prepares, recreates and resizes its renderer",
    (
        "self.surface_lost = false;",
        "crate::display::prepare(window)",
        "renderer.recreate_surface()",
        "renderer.resize(size.width, size.height);",
        "self.previous = Instant::now();",
    ),
)
require(
    game,
    "Game exit funnels through the established shutdown method",
    (
        "fn exiting(&mut self, _event_loop: &ActiveEventLoop)",
        "self.shut_down();",
    ),
)

native = Path(__file__).resolve().parents[1] / "tools/gles2-hud-prototype/app/src/main/cpp/horizon_egl_owner.cpp"
if not native.is_file():
    raise SystemExit("FAIL: missing native EGL owner implementation")
native_text = native.read_text(encoding="utf-8")
require(
    native_text,
    "Runtime frame failures detect EGL_CONTEXT_LOST and recover or invalidate the context",
    (
        "void invalidate_lost_context(PavoEglOwner* owner)",
        "bool recreate_context(PavoEglOwner* owner)",
        "if (owner->context == EGL_NO_CONTEXT)",
        "if (error != EGL_CONTEXT_LOST)",
        "invalidate_lost_context(owner);",
        "eglSwapBuffers failed (EGL_CONTEXT_LOST); context invalidated for resume recovery",
        "owner->context = EGL_NO_CONTEXT;",
        "++owner->context_generation;",
    ),
)
require_order(
    native_text,
    "Context-loss invalidation is followed by EGL context recreation on resume",
    (
        "void invalidate_lost_context(PavoEglOwner* owner)",
        "extern \"C\" int pavo_egl_make_current",
        "extern \"C\" int pavo_egl_resume",
        "if (context_recreated) ++owner->context_generation;",
    ),
)

generator = Path(__file__).resolve().parents[1] / "patches/horizon-078/gles2-native-build.py"
if not generator.is_file():
    raise SystemExit("FAIL: missing GLES2 Android source generator")
generator_text = generator.read_text(encoding="utf-8")
require(
    generator_text,
    "Generated Runtime advances a distinct native-window generation on every successful resume",
    (
        "window_generation: WindowGenerationClock",
        "pub(crate) fn window_generation(&self) -> WindowGeneration",
        "fn ensure_window_generation(&self, expected: WindowGeneration) -> Result<(), String>",
        "fn advance_window_generation(&mut self) -> Result<WindowGeneration, String>",
        "self.advance_window_generation()?;",
        "Stale GLES2 window work: expected generation",
    ),
)
runtime_start = generator_text.index("pub(crate) struct Runtime")
runtime_source = generator_text[runtime_start:]
recovery_start = runtime_source.index("fn ensure_context_current(")
recovery_end = runtime_source.index("\n    pub(crate) fn initialize(", recovery_start)
recovery_source = runtime_source[recovery_start:recovery_end]
require_order(
    recovery_source,
    "EGL generation is committed only after cached GPU resources restore successfully",
    (
        "self.renderer = super::gles2_native::Backend::new()?;",
        "if let Some(assets) = self.cached_assets.as_ref()",
        "if let Err(error) = restore",
        "self.initialized = true;",
        "self.context_generation = generation;",
    ),
)
for method in ("initialize", "suspend", "resume", "begin_frame", "upload_horizon_section", "upload_section", "remove_column", "clear_sections", "draw_sections", "present", "update_lightmap", "update_atlas_layer"):
    start = runtime_source.find(f"pub(crate) fn {method}(")
    if start < 0:
        raise SystemExit(f"FAIL: missing generation-aware Runtime::{method}")
    body = runtime_source.find("{", start)
    first_statement = runtime_source[body + 1:body + 220]
    if "self.ensure_window_generation(generation)?;" not in first_statement:
        raise SystemExit(f"FAIL: Runtime::{method} must reject stale window work before touching EGL/GL")
    print(f"PASS: Runtime::{method} rejects stale window generations")
suspend_source = runtime_source[runtime_source.index("pub(crate) fn suspend("):]
require_order(
    suspend_source,
    "Suspend rejects stale generations before detaching EGL",
    (
        "pub(crate) fn suspend(&mut self, generation: WindowGeneration)",
        "self.ensure_window_generation(generation)?;",
        "self.egl.suspend()?;",
        "self.surface_attached = false;",
    ),
)
resume_source = runtime_source[runtime_source.index("pub(crate) fn resume("):]
require_order(
    resume_source,
    "Resume rejects stale generations and advances only after EGL reattachment",
    (
        "pub(crate) fn resume(&mut self, generation: WindowGeneration, window: &Window)",
        "self.ensure_window_generation(generation)?;",
        "let generation = self.egl.resume(window)?;",
        "self.advance_window_generation()?;",
        "self.surface_attached = true;",
    ),
)
require(
    generator_text,
    "Generated terrain adapter forwards the same window generation after CPU staging",
    (
        "self.upload_section(generation, key, solid, translucent, ranges)",
    ),
)
require(
    generator_text,
    "Generated Runtime rejects GL work while the Android window surface is detached",
    (
        "fn ensure_surface_attached(&self) -> Result<(), String>",
        "EGL window surface is detached; resume it before rendering or uploading",
        "self.ensure_surface_attached()?;",
        "if self.surface_attached {",
        "if !self.surface_attached {",
        "GLES2 assets must be initialized before presenting a frame",
    ),
)
if generator_text.count("self.ensure_surface_attached()?;") != 10:
    raise SystemExit("FAIL: every EGL make-current, frame-begin, present and CPU staging path must guard surface attachment")

runtime_start = generator_text.index("pub(crate) struct Runtime")
runtime = generator_text[runtime_start:]
for method, signature in (
    ("initialize", "    pub(crate) fn initialize("),
    ("upload_horizon_section", "    pub(crate) fn upload_horizon_section("),
    ("upload_section", "    pub(crate) fn upload_section("),
    ("update_lightmap", "    pub(crate) fn update_lightmap("),
    ("update_atlas_layer", "    pub(crate) fn update_atlas_layer("),
):
    start = runtime.find(signature)
    if start < 0:
        raise SystemExit(f"FAIL: missing Runtime::{method}")
    body = runtime.find("{", start)
    first_statement = runtime[body + 1:body + 100]
    if "self.ensure_surface_attached()?;" not in first_statement:
        raise SystemExit(f"FAIL: Runtime::{method} must reject a detached EGL surface before CPU staging allocations")
    print(f"PASS: Runtime::{method} guards before CPU staging allocations")

require(
    generator_text,
    "Generated Runtime never destroys GL resources without a current EGL context",
    (
        "surface_attached: bool",
        "self.surface_attached = false;",
        "self.surface_attached = true;",
        "impl Drop for Runtime",
        "let current_generation = self.egl.make_current();",
        "if !self.surface_attached || current_generation != Ok(self.context_generation)",
        "self.renderer.abandon();",
    ),
)

require(
    generator_text,
    "GLES2 terrain context-recovery cache takes ownership of staging vectors without a second copy",
    (
        "solid: Vec<super::gles2_native::HorizonTerrainVertex>",
        "translucent: Vec<super::gles2_native::HorizonTerrainVertex>",
        "ranges: Vec<u32>",
        "self.renderer.upload_section(key, &solid, &translucent, &ranges)?;",
        "self.cached_sections.insert(key, CachedSection { solid, translucent, ranges });",
    ),
)
if "cached_solid.extend_from_slice(solid)" in generator_text:
    raise SystemExit("FAIL: GLES2 terrain cache duplicates the solid vertex staging allocation")
if "cached_translucent.extend_from_slice(translucent)" in generator_text:
    raise SystemExit("FAIL: GLES2 terrain cache duplicates the translucent vertex staging allocation")
print("PASS: GLES2 terrain cache avoids duplicate CPU vertex buffers")

contract = Path(__file__).resolve().parents[1] / "patches/horizon-078/ANDROID5-EGL-LIFECYCLE-CONTRACT.md"
if not contract.is_file():
    raise SystemExit("FAIL: missing Android EGL lifecycle contract")
contract_text = contract.read_text(encoding="utf-8")
require(
    contract_text,
    "EGL contract defines ownership, generations, loss recovery and teardown",
    (
        "EGLDisplay",
        "EGLContext",
        "ANativeWindow",
        "generation",
        "EGL_CONTEXT_LOST",
        "destroy EGL surface",
        "context -> terminate display",
    ),
)

print(
    "Android EGL lifecycle audit passed. This checks source assumptions only; "
    "it does not claim an EGL backend has been implemented."
)
