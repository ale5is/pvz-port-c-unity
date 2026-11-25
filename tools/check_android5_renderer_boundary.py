#!/usr/bin/env python3
"""Guard the Horizon 0.7.8 Android renderer boundary used by the GLES2 port."""
import sys
from pathlib import Path

root = Path(sys.argv[1]).resolve()

checks = {
    "Android entry owns the winit event loop": (
        root / "crates/horizon-client/src/android_main.rs",
        ["launcher::Launcher::new(home.clone())", "events.run_app(&mut launcher)"],
    ),
    "Launcher stores the concrete wgpu renderer": (
        root / "crates/horizon-client/src/android/launcher.rs",
        [
            "renderer: Option<Renderer>",
            "game::loading_renderer(",
            "renderer.set_loading_ui(&gui.atlas, gui.panorama.as_ref())?",
            "renderer.recreate_surface()?",
            "renderer.suspend_surface();",
            "WindowEvent::RedrawRequested => self.draw_pages()",
        ],
    ),
    "Game loading path creates the same Renderer": (
        root / "crates/horizon-client/src/game.rs",
        [
            "pollster::block_on(Renderer::new(",
            "pub(crate) fn loading_renderer(",
            "return renderer.render(&loading_frame(&hud));",
        ],
    ),
    "Renderer is directly coupled to wgpu": (
        root / "crates/horizon-client-render/src/gfx.rs",
        [
            "pub struct Renderer {",
            "pub device: wgpu::Device",
            "pub queue: wgpu::Queue",
            "pub fn render(&mut self, frame: &Frame) -> Result<bool>",
            "self.draw_frame(frame, None)",
            "pub fn suspend_surface(&mut self)",
            "pub fn recreate_surface(&mut self) -> Result<()>",
        ],
    ),
    "Render crate forbids unsafe EGL FFI": (
        root / "crates/horizon-client-render/src/lib.rs",
        ["#![forbid(unsafe_code)]"],
    ),
}

failed = []
for label, (path, needles) in checks.items():
    if not path.is_file():
        failed.append(f"{label}: missing {path.relative_to(root)}")
        continue
    source = path.read_text(encoding="utf-8")
    missing = [needle for needle in needles if needle not in source]
    if missing:
        failed.append(f"{label}: missing markers: {missing}")
    else:
        print(f"PASS: {label}")

if failed:
    print("\n".join(f"FAIL: {item}" for item in failed), file=sys.stderr)
    raise SystemExit(1)

print("Android GLES2 integration boundary is unchanged; EGL must branch before wgpu Renderer creation.")
