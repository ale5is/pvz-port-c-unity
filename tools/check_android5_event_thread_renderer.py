#!/usr/bin/env python3
"""Verify Android GLES CPU preparation is separated from event-thread renderer creation."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_event_thread_renderer.py <horizon-source-root>")
root = Path(sys.argv[1])
game = (root / "crates/horizon-client/src/game.rs").read_text(encoding="utf-8")

def require(label, markers):
    missing = [marker for marker in markers if marker not in game]
    if missing:
        raise SystemExit(f"FAIL {label}: missing {missing}")
    print(f"PASS {label}")

require("CPU worker owns only prepared CPU textures", (
    "gles2_cpu_job: Option<JoinHandle<horizon_client::gfx::PreparedTextures>>",
    '.name("GLES2 CPU texture preparation".into())',
    "horizon_client::gfx::PreparedTextures::new(",
    ".with_pbr(",
    "loaded.renderer_cpu = Some(prepared);",
))
require("GLES worker does not create or transfer a GPU context", (
    "if gl {",
    "let prepared = loaded.renderer_cpu.take();",
    "return Ok(());",
))
require("Loading-to-game handoff waits for CPU preparation", (
    "loading.gles2_cpu_job.is_some()",
    "loading.gles2_cpu_job.is_none()",
))
require("World renderer creation stays in the event-thread finish path", (
    "build_renderer(",
    "self.renderer.as_ref(),",
    "loaded.renderer_cpu,",
))
worker_start = game.index('.name("GLES2 CPU texture preparation".into())')
worker_end = game.index("return Ok(());", worker_start)
worker = game[worker_start:worker_end]
for forbidden in ("Renderer::new", "new_with_context", "background_context", "pavo_egl_create", "pavo_gles2_create", "wgpu::"):
    if forbidden in worker:
        raise SystemExit(f"FAIL CPU worker contains GPU/window operation: {forbidden}")
print("PASS GLES CPU worker contains no renderer/EGL/GLES construction")
