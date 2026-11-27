#!/usr/bin/env python3
"""Check Android OpenGL does not transfer wgpu context to the renderer worker."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_event_thread_renderer.py <horizon-source-root>")
game = (Path(sys.argv[1]) / "crates/horizon-client/src/game.rs").read_text(encoding="utf-8")

start = game.index("fn prepare_renderer(&mut self)")
end = game.index("    fn resumed(", start)
prepare = game[start:end]
guard = """        if gl {
            // The current wgpu-GL renderer remains the sole surface owner until
            // Loading::finish releases its surface and builds the replacement.
            // Never transfer its background context to a worker: native EGL/GLES
            // must be created on this event thread after the old surface is released.
            return Ok(());
        }"""
if prepare.count(guard) != 1:
    raise SystemExit("FAIL: expected exactly one Android OpenGL event-thread guard")
if prepare.index(guard) > prepare.index("previous.wait_idle();"):
    raise SystemExit("FAIL: OpenGL guard appears after the wgpu wait/context handoff")
if "previous.background_context()" in prepare:
    raise SystemExit("FAIL: prepare_renderer still exports the wgpu background context")
if "Renderer::new_with_context(" in prepare:
    raise SystemExit("FAIL: renderer worker still constructs a replacement renderer")
if 'name("Renderer preparation".into())' in prepare and prepare.index(guard) > prepare.index('name("Renderer preparation".into())'):
    raise SystemExit("FAIL: OpenGL guard is too late to prevent renderer worker creation")

finish_start = game.index("    fn finish(", end)
finish_end = game.index("    fn ", finish_start + 8)
finish = game[finish_start:finish_end]
release = finish.find("renderer.release_surface();")
build = finish.find("build_renderer(")
if release < 0 or build < 0 or release > build:
    raise SystemExit("FAIL: finish must release the old surface before event-thread replacement")
print("PASS Android OpenGL skips background renderer/context transfer")
print("PASS replacement construction remains in event-thread Loading::finish")
print("PASS old surface is released before replacement construction")
