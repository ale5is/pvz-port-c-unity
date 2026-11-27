#!/usr/bin/env python3
"""Add a backend-neutral terrain residency contract and adapt the wgpu renderer."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
api = root / "crates/horizon-render-api/src/lib.rs"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
for path in (api, gfx):
    if not path.is_file():
        raise SystemExit(f"Missing expected source: {path}")
api_text = api.read_text(encoding="utf-8")
gfx_text = gfx.read_text(encoding="utf-8")

trait = """/// Stable section identity shared by the game streaming queue and render backends.
pub type SectionKey = (i32, i32, i32);

/// Terrain residency operations used by the streamed-world game loop.
///
/// The mesh is a backend-neutral CPU value supplied by the caller. Implementors
/// may upload it immediately or retain/convert it, but must preserve replacement
/// semantics for an already resident section. Column removals are slices so the
/// contract remains object-safe and does not expose a backend iterator type.
pub trait TerrainStreamBackend<Mesh> {
    fn upload_section(&mut self, key: SectionKey, mesh: &Mesh);
    fn remove_columns(&mut self, columns: &[(i32, i32)]);
    fn lod_detail_revision(&self) -> u64;
    fn start_occlusion(&mut self, camera: [f64; 3], distance: f32);
}

"""
marker = "#[cfg(test)]\nmod tests {"
if "pub trait TerrainStreamBackend<Mesh>" not in api_text:
    if api_text.count(marker) != 1:
        raise SystemExit("Could not uniquely locate backend API test module")
    api_text = api_text.replace(marker, trait + marker, 1)
else:
    for required in ("pub type SectionKey", "fn upload_section(", "fn remove_columns(", "fn lod_detail_revision(", "fn start_occlusion("):
        if required not in api_text:
            raise SystemExit(f"Existing terrain stream API is incomplete: {required}")

impl_marker = "impl horizon_render_api::TerrainStreamBackend<SectionMesh> for Renderer {"
if impl_marker not in gfx_text:
    implementation = """
impl horizon_render_api::TerrainStreamBackend<SectionMesh> for Renderer {
    fn upload_section(&mut self, key: horizon_render_api::SectionKey, mesh: &SectionMesh) {
        Renderer::upload(self, key, mesh);
    }

    fn remove_columns(&mut self, columns: &[(i32, i32)]) {
        Renderer::remove_columns(self, columns.iter().copied());
    }

    fn lod_detail_revision(&self) -> u64 {
        Renderer::lod_detail_revision(self)
    }

    fn start_occlusion(&mut self, camera: [f64; 3], distance: f32) {
        Renderer::start_occlusion(self, DVec3::new(camera[0], camera[1], camera[2]), distance);
    }
}
"""
    gfx_text = gfx_text.rstrip() + "\n\n" + implementation
elif gfx_text.count(impl_marker) != 1:
    raise SystemExit("Duplicate TerrainStreamBackend implementation detected")

if any(token in api_text for token in ("wgpu::", "winit::", "EGLDisplay", "EGLContext", "GLuint", "DVec3")):
    raise SystemExit("Terrain stream API leaks a GPU, window, EGL or math-library type")
for required in ("Renderer::upload(self, key, mesh)", "columns.iter().copied()", "Renderer::lod_detail_revision(self)", "DVec3::new(camera[0], camera[1], camera[2])"):
    if required not in gfx_text:
        raise SystemExit(f"wgpu terrain adapter is incomplete: {required}")
api.write_text(api_text, encoding="utf-8")
gfx.write_text(gfx_text, encoding="utf-8")
print("Installed the GPU-independent terrain residency contract and wgpu adapter.")
