#!/usr/bin/env python3
"""Move far-terrain LOD queue operations behind the backend-neutral contract."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
types = root / "crates/horizon-render-types/src/lib.rs"
api = root / "crates/horizon-render-api/src/lib.rs"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
game = root / "crates/horizon-client/src/game.rs"
for path in (types, api, gfx, game):
    if not path.is_file():
        raise SystemExit("Missing LOD backend migration source: " + str(path))

t = types.read_text(encoding="utf-8")
a = api.read_text(encoding="utf-8")
g = gfx.read_text(encoding="utf-8")
m = game.read_text(encoding="utf-8")

key_type = """/// Stable identity for a far-terrain tile, independent of the renderer.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct LodTileKey {
    pub x: i32,
    pub z: i32,
    pub level: u8,
}

"""
if "pub struct LodTileKey" not in t:
    anchor = "#[cfg(test)]\nmod tests {"
    if t.count(anchor) != 1:
        raise SystemExit("Could not locate neutral render-types tests")
    t = t.replace(anchor, key_type + anchor, 1)
elif "pub level: u8" not in t:
    raise SystemExit("Existing LodTileKey has an unexpected shape")

trait = """/// Far-terrain residency and incremental upload operations.
pub trait LodStreamBackend<Mesh> {
    fn clear_lods(&mut self);
    fn queue_lods(
        &mut self,
        meshes: Vec<(horizon_render_types::LodTileKey, Mesh)>,
        removed: Vec<horizon_render_types::LodTileKey>,
        cutout: Option<Vec<(i32, i32)>>,
    );
    fn upload_lods(&mut self, budget: std::time::Duration);
    fn lod_detail_columns(&self) -> Vec<(i32, i32)>;
}

"""
if "pub trait LodStreamBackend<Mesh>" not in a:
    anchor = "#[cfg(test)]\nmod tests {"
    if a.count(anchor) != 1:
        raise SystemExit("Could not locate neutral backend API tests")
    a = a.replace(anchor, trait + anchor, 1)

impl_marker = "impl horizon_render_api::LodStreamBackend<SectionMesh> for Renderer {"
if impl_marker not in g:
    implementation = """
impl horizon_render_api::LodStreamBackend<SectionMesh> for Renderer {
    fn clear_lods(&mut self) {
        Renderer::clear_lods(self);
    }

    fn queue_lods(
        &mut self,
        meshes: Vec<(horizon_render_types::LodTileKey, SectionMesh)>,
        removed: Vec<horizon_render_types::LodTileKey>,
        cutout: Option<Vec<(i32, i32)>>,
    ) {
        let meshes = meshes
            .into_iter()
            .map(|(key, mesh)| (
                crate::lod::TileKey { x: key.x, z: key.z, level: key.level },
                mesh,
            ))
            .collect();
        let removed = removed
            .into_iter()
            .map(|key| crate::lod::TileKey { x: key.x, z: key.z, level: key.level })
            .collect();
        Renderer::queue_lods(self, meshes, removed, cutout.map(|columns| columns.into_iter().collect()));
    }

    fn upload_lods(&mut self, budget: std::time::Duration) {
        Renderer::upload_lods(self, budget);
    }

    fn lod_detail_columns(&self) -> Vec<(i32, i32)> {
        Renderer::lod_detail_columns(self).collect()
    }
}
"""
    g = g.rstrip() + "\n\n" + implementation

import_line = "use horizon_render_api::TerrainStreamBackend as _;\n"
lod_import = "use horizon_render_api::LodStreamBackend as _;\n"
if lod_import not in m:
    if m.count(import_line) != 1:
        raise SystemExit("Could not locate Game terrain-backend trait import")
    m = m.replace(import_line, import_line + lod_import, 1)

replacements = [
    ("renderer.clear_lods();", "renderer.clear_lods();"),
    ("renderer\n                            .lod_detail_columns()", "renderer\n                            .lod_detail_columns()\n                            .into_iter()"),
]
# The first call is now resolved through LodStreamBackend by the imported trait.
old_queue = "renderer.queue_lods(meshes, lod.removed(), lod.take_cutout());"
new_queue = """let meshes = meshes
                    .into_iter()
                    .map(|(key, mesh)| (
                        horizon_render_types::LodTileKey { x: key.x, z: key.z, level: key.level },
                        mesh,
                    ))
                    .collect();
                let removed = lod.removed()
                    .into_iter()
                    .map(|key| horizon_render_types::LodTileKey { x: key.x, z: key.z, level: key.level })
                    .collect();
                let cutout = lod.take_cutout().map(|columns| columns.into_iter().collect());
                renderer.queue_lods(meshes, removed, cutout);"""
if old_queue in m:
    m = m.replace(old_queue, new_queue, 1)
elif "renderer.queue_lods(meshes, removed, cutout);" not in m:
    raise SystemExit("Could not locate Game LOD queue call")
old_columns = "renderer\n                            .lod_detail_columns()"
if old_columns in m:
    m = m.replace(old_columns, old_columns + "\n                            .into_iter()", 1)
elif ".lod_detail_columns()\n                            .into_iter()" not in m:
    raise SystemExit("Could not adapt LOD detail-column collection")
if m.count("use horizon_render_api::LodStreamBackend as _;") != 1:
    raise SystemExit("Game LOD backend trait import is missing or duplicated")
if g.count(impl_marker) != 1:
    raise SystemExit("Expected exactly one wgpu LodStreamBackend adapter")

for stale in ("renderer.queue_lods(meshes, lod.removed(), lod.take_cutout());",
              "renderer.lod_detail_columns().filter"):
    if stale in m:
        raise SystemExit("Concrete LOD API call remains: " + stale)

types.write_text(t, encoding="utf-8")
api.write_text(a, encoding="utf-8")
gfx.write_text(g, encoding="utf-8")
game.write_text(m, encoding="utf-8")
print("Installed neutral LOD tile identity, queue/upload contract and wgpu adapter.")
print("Game now submits LOD work and reads detail columns through LodStreamBackend.")
