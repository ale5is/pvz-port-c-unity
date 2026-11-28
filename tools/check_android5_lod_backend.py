#!/usr/bin/env python3
"""Verify far-terrain LOD queue operations cross a GPU-independent contract."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_lod_backend.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
types = root / "crates/horizon-render-types/src/lib.rs"
api = root / "crates/horizon-render-api/src/lib.rs"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
game = root / "crates/horizon-client/src/game.rs"
for path in (types, api, gfx, game):
    if not path.is_file():
        raise SystemExit("FAIL: missing " + str(path))
t, a, g, m = (path.read_text(encoding="utf-8") for path in (types, api, gfx, game))
for marker in ("pub struct LodTileKey", "pub x: i32", "pub z: i32", "pub level: u8"):
    if marker not in t:
        raise SystemExit("FAIL: neutral LOD identity missing " + marker)
for marker in (
    "pub trait LodStreamBackend<Mesh>",
    "fn clear_lods(&mut self);",
    "fn queue_lods(",
    "fn upload_lods(&mut self, budget: std::time::Duration);",
    "fn lod_detail_columns(&self) -> Vec<(i32, i32)>;",
):
    if marker not in a:
        raise SystemExit("FAIL: neutral LOD contract missing " + marker)
if any(token in a for token in ("wgpu::", "winit::", "EGLDisplay", "EGLContext", "GLuint", "glam::")):
    raise SystemExit("FAIL: LOD backend API leaks renderer implementation types")
impl_marker = "impl horizon_render_api::LodStreamBackend<SectionMesh> for Renderer {"
if g.count(impl_marker) != 1:
    raise SystemExit("FAIL: expected exactly one wgpu LOD adapter")
for marker in (
    "Renderer::clear_lods(self)",
    "Renderer::queue_lods(self, meshes, removed, cutout.map(|columns| columns.into_iter().collect()))",
    "Renderer::upload_lods(self, budget)",
    "Renderer::lod_detail_columns(self).collect()",
):
    if marker not in g:
        raise SystemExit("FAIL: wgpu LOD adapter missing " + marker)
for marker in (
    "use horizon_render_api::LodStreamBackend as _;",
    "horizon_render_types::LodTileKey { x: key.x, z: key.z, level: key.level }",
    "renderer.queue_lods(meshes, removed, cutout);",
    ".lod_detail_columns()\n                            .into_iter()",
):
    if marker not in m:
        raise SystemExit("FAIL: Game does not use neutral LOD API: " + marker)
if "renderer.queue_lods(meshes, lod.removed(), lod.take_cutout());" in m:
    raise SystemExit("FAIL: Game still calls concrete LOD queue")
print("PASS: LOD tile identity is GPU-independent.")
print("PASS: wgpu preserves the original clear/queue/upload algorithm behind the trait.")
print("PASS: Game submits LOD jobs and reads detail columns through the backend contract.")
print("LIMITATION: the native GLES2 renderer has not implemented LodStreamBackend yet.")
