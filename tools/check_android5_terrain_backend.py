#!/usr/bin/env python3
"""Verify terrain streaming has a GPU-independent backend contract."""
import sys
from pathlib import Path
if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_terrain_backend.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
api = root / "crates/horizon-render-api/src/lib.rs"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
game = root / "crates/horizon-client/src/game.rs"
manifest = root / "crates/horizon-client/Cargo.toml"
if not all(path.is_file() for path in (api, gfx, game, manifest)):
    raise SystemExit("FAIL: renderer API, wgpu implementation, Game or manifest is missing")
a = api.read_text(encoding="utf-8")
g = gfx.read_text(encoding="utf-8")
m = game.read_text(encoding="utf-8")
d = manifest.read_text(encoding="utf-8")
for marker in (
    "pub type SectionKey = (i32, i32, i32);",
    "pub trait TerrainStreamBackend<Mesh>",
    "fn upload_section(&mut self, key: SectionKey, mesh: &Mesh);",
    "fn evict_columns(&mut self, columns: &[(i32, i32)]);",
    "fn section_lod_revision(&self) -> u64;",
    "fn update_occlusion(&mut self, camera: [f64; 3], distance: f32);",
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
for marker in (
    "use horizon_render_api::TerrainStreamBackend as _;",
    "renderer.upload_section(*key, mesh);",
    "renderer.upload_section(key, &mesh);",
    "renderer.evict_columns(&unloaded_columns);",
    "renderer.update_occlusion([camera.x, camera.y, camera.z],",
    "renderer.section_lod_revision()",
):
    if marker not in m:
        raise SystemExit(f"FAIL: Game does not use terrain backend contract: {marker}")
if 'horizon-render-api = { path = "../horizon-render-api" }' not in d:
    raise SystemExit("FAIL: horizon-client does not depend on the neutral renderer API")
for stale in ("renderer.upload(", "renderer.remove_columns(", "renderer.start_occlusion(", "renderer.lod_detail_revision()"):
    if stale in m:
        raise SystemExit(f"FAIL: concrete Game terrain call remains: {stale}")
print("PASS: terrain residency operations have a GPU-independent contract.")
print("PASS: wgpu Renderer adapts upload, eviction, LOD revision and occlusion.")
print("PASS: Game streaming uses the contract instead of concrete terrain methods.")
print("LIMITATION: Game still owns concrete Renderer; GLES2 has not implemented this trait.")
