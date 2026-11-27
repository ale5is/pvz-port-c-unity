#!/usr/bin/env python3
"""Verify renderer replacement state crosses Game only through opaque backend context."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_renderer_replacement_context.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
api = (root / "crates/horizon-render-api/src/lib.rs").read_text(encoding="utf-8")
gfx = (root / "crates/horizon-client-render/src/gfx.rs").read_text(encoding="utf-8")
game = (root / "crates/horizon-client/src/game.rs").read_text(encoding="utf-8")

for marker in (
    "pub struct BackendContext",
    "pub fn into_inner<T: Any + Send>",
    "fn replacement_context(&self) -> Result<BackendContext, RenderBackendError>;",
):
    if marker not in api:
        raise SystemExit(f"FAIL: neutral API missing {marker}")
trait_start = gfx.find("impl<'frame> horizon_render_api::RenderBackend<Frame<'frame>> for Renderer {")
if trait_start < 0:
    raise SystemExit("FAIL: wgpu RenderBackend implementation is missing")
trait_impl = gfx[trait_start:]
for marker in (
    "fn replacement_context(",
    "horizon_render_api::BackendContext::new(",
    "self.background_context()",
):
    if marker not in trait_impl:
        raise SystemExit(f"FAIL: wgpu RenderBackend adapter missing {marker}")
for marker in (
    "context.into_inner::<ReplacementContext>",
    "BackendKind::Wgpu",
):
    if marker not in gfx:
        raise SystemExit(f"FAIL: wgpu constructor missing checked context operation {marker}")
if "previous.background_context()" in game:
    raise SystemExit("FAIL: Game still reaches into the concrete wgpu renderer")
if "RenderBackend::<horizon_client::gfx::Frame<'_>>::replacement_context(previous)" not in game:
    raise SystemExit("FAIL: Game does not request replacement state through RenderBackend")
if "mut previous: Option<ReplacementContext>" in gfx:
    raise SystemExit("FAIL: constructor still accepts a concrete replacement context")
print("PASS: Game transfers only an opaque backend-tagged context; wgpu downcasts at its adapter boundary.")
print("LIMITATION: the context factory still has a wgpu implementation only; GLES2 owner selection is not integrated.")
