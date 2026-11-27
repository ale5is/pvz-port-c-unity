#!/usr/bin/env python3
"""Verify block-entity visibility is backend-neutral at the Game boundary."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_renderer_visible_block_entities.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
api = (root / "crates/horizon-render-api/src/lib.rs").read_text(encoding="utf-8")
gfx = (root / "crates/horizon-client-render/src/gfx.rs").read_text(encoding="utf-8")
game = (root / "crates/horizon-client/src/game.rs").read_text(encoding="utf-8")

signature = "fn visible_block_entities<'a>("
if api.count(signature) != 1:
    raise SystemExit("FAIL: RenderBackend block-entity visibility method missing or duplicated")
adapter = "Box::new(Renderer::visible_block_entities(self, fade_seconds))"
if gfx.count(adapter) != 1:
    raise SystemExit("FAIL: wgpu adapter does not forward block-entity visibility")
neutral = "RenderBackend::visible_block_entities(r, self.settings.chunk_fade)"
if game.count(neutral) != 1:
    raise SystemExit("FAIL: Game does not use backend-neutral block-entity visibility")
if "r.visible_block_entities(self.settings.chunk_fade)" in game:
    raise SystemExit("FAIL: Game still calls concrete renderer visibility")
for forbidden in ("wgpu::", "winit::", "EGLDisplay", "EGLContext", "GLuint"):
    if forbidden in api:
        raise SystemExit(f"FAIL: backend-neutral API leaks implementation type: {forbidden}")

print("PASS: block-entity fade visibility is exposed through RenderBackend.")
print("PASS: Game no longer calls the concrete wgpu Renderer for this operation.")
