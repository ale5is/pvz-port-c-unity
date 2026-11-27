#!/usr/bin/env python3
"""Verify terrain streaming has a GPU-independent backend contract."""
import sys
from pathlib import Path
if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_terrain_backend.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
api = root / "crates/horizon-render-api/src/lib.rs"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
if not api.is_file() or not gfx.is_file():
    raise SystemExit("FAIL: renderer API or wgpu implementation is missing")
a = api.read_text(encoding="utf-8")
g = gfx.read_text(encoding="utf-8")
for marker in (
    "pub type SectionKey = (i32, i32, i32);",
    "pub trait TerrainStreamBackend<Mesh>",
    "fn upload_section(&mut self, key: SectionKey, mesh: &Mesh);",
    "fn remove_columns(&mut self, columns: &[(i32, i32)]);",
    "fn lod_detail_revision(&self) -> u64;",
    "fn start_occlusion(&mut self, camera: [f64; 3], distance: f32);",
):
    if marker not in a:
        raise SystemExit(f"FAIL: neutral terrain API missing {marker}")
if any(token in a for token in ("wgpu::", "winit::", "EGLDisplay", "EGLContext", "GLuint", "DVec3")):
    raise SystemExit("FAIL: neutral terrain API leaks a GPU/window/math implementation type")
impl_marker = "impl horizon_render_api::TerrainStreamBackend<SectionMesh> for Renderer {"
if g.count(impl_marker) != 1:
    raise SystemExit("FAIL: wgpu Renderer must implement TerrainStreamBackend exactly once")
for marker in (
    "Renderer::upload(self, key, mesh)",
    "Renderer::remove_columns(self, columns.iter().copied())",
    "Renderer::lod_detail_revision(self)",
    "Renderer::start_occlusion(self, DVec3::new(camera[0], camera[1], camera[2]), distance)",
):
    if marker not in g:
        raise SystemExit(f"FAIL: wgpu terrain adapter missing {marker}")
print("PASS: terrain residency operations have a GPU-independent contract.")
print("PASS: existing wgpu Renderer adapts upload, eviction, LOD revision and occlusion.")
print("LIMITATION: Game still owns the concrete Renderer; GLES2 has not implemented this trait yet.")
