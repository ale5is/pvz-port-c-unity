#!/usr/bin/env python3
import sys
from pathlib import Path
if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_lod_backend.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
paths = [root / p for p in (
    "crates/horizon-render-types/src/lib.rs",
    "crates/horizon-render-api/src/lib.rs",
    "crates/horizon-client-render/src/gfx.rs",
    "crates/horizon-client/src/game.rs",
)]
for path in paths:
    if not path.is_file():
        raise SystemExit("FAIL: missing " + str(path))
t, a, g, m = [p.read_text(encoding="utf-8") for p in paths]
for marker in ("pub struct LodTileKey", "pub x: i32", "pub z: i32", "pub level: u8"):
    if marker not in t: raise SystemExit("FAIL: neutral LOD identity missing " + marker)
for marker in ("pub trait LodStreamBackend<Mesh>", "fn clear_lods(&mut self);", "fn queue_lods(", "fn upload_lods(&mut self, budget: std::time::Duration);", "fn lod_detail_columns(&self) -> Vec<(i32, i32)>;"):
    if marker not in a: raise SystemExit("FAIL: neutral LOD contract missing " + marker)
if any(token in a for token in ("wgpu::", "winit::", "EGLDisplay", "EGLContext", "GLuint", "glam::")):
    raise SystemExit("FAIL: LOD API leaks renderer implementation types")
impl_marker = "impl horizon_render_api::LodStreamBackend<SectionMesh> for Renderer {"
if g.count(impl_marker) != 1: raise SystemExit("FAIL: expected exactly one wgpu LOD adapter")
for marker in ("Renderer::clear_lods(self)", "Renderer::queue_lods(self, meshes, removed, cutout.map(|columns| columns.into_iter().collect()))", "Renderer::upload_lods(self, budget)", "Renderer::lod_detail_columns(self).collect()"):
    if marker not in g: raise SystemExit("FAIL: wgpu LOD adapter missing " + marker)
for marker in ("LodStreamBackend::<horizon_client::mesher::SectionMesh>::queue_lods(", "LodStreamBackend::<horizon_client::mesher::SectionMesh>::clear_lods(renderer)", "LodStreamBackend::<horizon_client::mesher::SectionMesh>::upload_lods(renderer", "LodStreamBackend::<horizon_client::mesher::SectionMesh>::lod_detail_columns(renderer)"):
    if marker not in m: raise SystemExit("FAIL: Game missing explicit neutral LOD dispatch " + marker)
if "renderer.queue_lods(meshes, lod.removed(), lod.take_cutout());" in m:
    raise SystemExit("FAIL: Game still calls concrete LOD queue")
print("PASS: neutral LOD tile identity and API.")
print("PASS: wgpu adapter preserves original renderer operations.")
print("PASS: Game dispatches LOD operations explicitly through the neutral trait.")
print("LIMITATION: native GLES2 has not implemented LodStreamBackend yet.")
