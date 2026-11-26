#!/usr/bin/env python3
"""Guard the concrete renderer call sites that an Android GLES2 factory must replace."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_renderer_callsite_inventory.py <horizon-source-root>")

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
        raise SystemExit(f"FAIL: {label}: source order changed")
    print(f"PASS: {label}")


game = read("crates/horizon-client/src/game.rs")
launcher = read("crates/horizon-client/src/android/launcher.rs")
gfx = read("crates/horizon-client-render/src/gfx.rs")

require(
    game,
    "Game still stores a concrete renderer and exposes both construction paths",
    (
        "renderer: Option<Renderer>",
        "fn build_renderer(",
        "previous: Option<&Renderer>",
        ") -> Result<Renderer>",
        "pub(crate) fn loading_renderer(",
    ),
)
require(
    game,
    "Game renderer lifecycle and streaming call sites remain visible",
    (
        "renderer.upload(key, &mesh)",
        "renderer.remove_columns(",
        "renderer.suspend_surface()",
        "renderer.recreate_surface()",
        "renderer.render(&frame)",
        "renderer.set_world_assets(",
        "renderer.queue_lods(",
        "renderer.upload_lods(",
    ),
)
require(
    game,
    "Game still has direct wgpu-specific field access to extract",
    (
        "renderer.device",
        "renderer.hud",
        "renderer.info",
        "renderer.gpu_times",
        "renderer.config",
    ),
)
require(
    launcher,
    "Android launcher has its own concrete renderer owner",
    (
        "renderer: Option<Renderer>",
        "game::loading_renderer(",
        "renderer.set_loading_ui(&gui.atlas, gui.panorama.as_ref())?",
        "renderer.suspend_surface();",
        "renderer.recreate_surface()?",
    ),
)
require_order(
    launcher,
    "Android loading renderer is constructed before launcher ownership",
    (
        "game::loading_renderer(",
        "renderer.set_loading_ui(",
        "self.renderer = Some(renderer);",
    ),
)
require(
    gfx,
    "Current renderer owns wgpu resources and is not an EGL-compatible handle",
    (
        "pub struct Renderer {",
        "instance: wgpu::Instance",
        "adapter: wgpu::Adapter",
        "surface: Option<wgpu::Surface<'static>>",
        "pub device: wgpu::Device",
        "pub queue: wgpu::Queue",
        "pub fn render(&mut self, frame: &Frame) -> Result<bool>",
    ),
)

print(
    "Renderer call-site inventory passed: launcher, loading, world construction, "
    "streaming, lifecycle and wgpu-specific field accesses are accounted for."
)
