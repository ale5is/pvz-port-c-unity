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
    "Runtime frame failures invalidate lost EGL contexts for the next resume",
    (
        "void invalidate_lost_context(PavoEglOwner* owner)",
        "eglMakeCurrent failed (EGL_CONTEXT_LOST); context invalidated for resume recovery",
        "eglSwapBuffers failed (EGL_CONTEXT_LOST); context invalidated for resume recovery",
        "owner->context = EGL_NO_CONTEXT;",
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
