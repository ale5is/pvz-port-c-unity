#!/usr/bin/env python3
"""Keep Android OpenGL renderer replacement on the window/event thread."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
game = root / "crates/horizon-client/src/game.rs"
source = game.read_text(encoding="utf-8")

anchor = """        if gl && (!self.menu.needs_world_renderer() || !self.menu.entry_presented) {
            return Ok(());
        }
        previous.wait_idle();"""
replacement = """        if gl && (!self.menu.needs_world_renderer() || !self.menu.entry_presented) {
            return Ok(());
        }
        if gl {
            // The current wgpu-GL renderer remains the sole surface owner until
            // Loading::finish releases its surface and builds the replacement.
            // Never transfer its background context to a worker: native EGL/GLES
            // must be created on this event thread after the old surface is released.
            return Ok(());
        }
        previous.wait_idle();"""
if source.count(anchor) == 0:
    raise SystemExit("Could not locate the post-HUD-boundary renderer handoff anchor")
if source.count(anchor) != 1:
    raise SystemExit(f"Expected one renderer handoff anchor, found {source.count(anchor)}")
if replacement not in source:
    source = source.replace(anchor, replacement)
if source.count(replacement) != 1:
    raise SystemExit("Android OpenGL event-thread guard was not installed exactly once")
if source.index("if gl {", source.index("let gl = previous")) > source.index("previous.wait_idle();"):
    raise SystemExit("OpenGL guard must run before any background-context handoff")
game.write_text(source, encoding="utf-8")
print("Android OpenGL renderer replacement deferred to Loading::finish on the event thread.")
