#!/usr/bin/env python3
"""Prevent confusing Horizon's wgpu GL backend with the native GLES2 port."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_backend_selection.py <horizon-source-root>")

root = Path(sys.argv[1])

def read(relative: str) -> str:
    path = root / relative
    if not path.is_file():
        raise SystemExit(f"FAIL: missing upstream source {relative}")
    return path.read_text(encoding="utf-8")

def require(source: str, label: str, markers: tuple[str, ...]) -> None:
    missing = [marker for marker in markers if marker not in source]
    if missing:
        raise SystemExit(f"FAIL: {label}: missing markers: {missing}")
    print(f"PASS: {label}")

def require_order(source: str, label: str, markers: tuple[str, ...]) -> None:
    positions = [source.find(marker) for marker in markers]
    if any(position < 0 for position in positions):
        missing = [marker for marker, position in zip(markers, positions) if position < 0]
        raise SystemExit(f"FAIL: {label}: missing markers: {missing}")
    if positions != sorted(positions):
        raise SystemExit(f"FAIL: {label}: unexpected source order")
    print(f"PASS: {label}")

manifest = read("crates/horizon-client-render/Cargo.toml")
graphics = read("crates/horizon-client-render/src/graphics.rs")
gfx = read("crates/horizon-client-render/src/gfx.rs")
launcher = read("crates/horizon-client/src/android/launcher.rs")

require(
    manifest,
    "wgpu GLES feature remains the existing non-macOS GL implementation",
    ('wgpu = { version = "30.0.1", default-features = false, features = ["gles"] }',),
)
require(
    graphics,
    "OpenGL is a generic renderer API choice, not a GLES2 mode",
    ("pub enum GraphicsApi", "OpenGl,", 'GraphicsApi::OpenGl => "opengl"'),
)
require(
    gfx,
    "OpenGl selects wgpu GL backend",
    ("fn backend_order(", "GraphicsApi::OpenGl => vec![Backends::GL]", "pub struct Renderer {", "pub device: wgpu::Device"),
)
require_order(
    launcher,
    "Android loading renderer is created before it is stored for the launcher",
    ("game::loading_renderer(", "renderer.set_loading_ui(", "self.renderer = Some(renderer);"),
)

print("Backend-selection contract passed: wgpu GL is not the native GLES2 renderer.")
