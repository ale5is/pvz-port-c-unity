#!/usr/bin/env python3
"""Extract shared CPU vertex layouts into a standalone, wgpu-free crate."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
workspace = root / "Cargo.toml"
render = root / "crates/horizon-client-render"
mesher_path = render / "src/mesher.rs"
render_manifest = render / "Cargo.toml"
crate = root / "crates/horizon-render-types"
crate_manifest = crate / "Cargo.toml"
crate_source = crate / "src/lib.rs"

if crate_source.exists():
    source = crate_source.read_text(encoding="utf-8")
    mesher = mesher_path.read_text(encoding="utf-8")
    if "pub struct Vertex" not in source or "pub struct TerrainVertex" not in source:
        raise SystemExit("Existing horizon-render-types crate is incomplete")
    if "pub use horizon_render_types::{" not in mesher:
        raise SystemExit("Existing render types crate is not re-exported by mesher")
    print("GPU-independent vertex format crate is already installed.")
    raise SystemExit(0)

mesher = mesher_path.read_text(encoding="utf-8")
start_marker = "/// An entity, item and effect vertex (the entity pipelines), with a face"
end_marker = "\n#[cfg(test)]\nmod vertex_tests"
if mesher.count(start_marker) != 1 or mesher.count(end_marker) != 1:
    raise SystemExit("Could not uniquely locate the pinned vertex-format definitions")
start = mesher.index(start_marker)
end = mesher.index(end_marker, start)
if end <= start:
    raise SystemExit("Invalid vertex-format extraction range")
definitions = mesher[start:end]
if "pub struct Vertex" not in definitions or "pub struct TerrainVertex" not in definitions:
    raise SystemExit("Expected both entity and terrain vertex formats in extraction range")
definitions = definitions.replace("pub(crate) uv: [u16; 2]", "pub uv: [u16; 2]")
if "pub(crate) uv:" in definitions:
    raise SystemExit("Terrain UV field did not become visible to the neutral crate")

workspace_source = workspace.read_text(encoding="utf-8")
workspace_anchor = '    "crates/horizon-client-render",\n'
if workspace_source.count(workspace_anchor) != 1:
    raise SystemExit("Could not uniquely locate horizon-client-render in workspace members")
workspace_source = workspace_source.replace(
    workspace_anchor,
    '    "crates/horizon-render-types",\n' + workspace_anchor,
)

manifest = render_manifest.read_text(encoding="utf-8")
dependency_anchor = "[dependencies]\n"
if manifest.count(dependency_anchor) != 1:
    raise SystemExit("Could not uniquely locate renderer dependencies")
manifest = manifest.replace(
    dependency_anchor,
    dependency_anchor + 'horizon-render-types = { path = "../horizon-render-types" }\n',
)

reexport = (
    "pub use horizon_render_types::{"
    "CULL_BACK, TILED_UV_STEPS, TerrainVertex, UV_STEPS, Vertex"
    "};\n"
)
mesher = mesher[:start] + mesher[end:]
import_anchor = "use std::sync::Arc;\n"
if mesher.count(import_anchor) != 1:
    raise SystemExit("Could not uniquely locate mesher imports for type re-export")
mesher = mesher.replace(import_anchor, import_anchor + reexport)

crate_source_text = (
    "//! GPU-upload vertex formats shared by independent rendering backends.\n"
    "//! This crate contains data layouts only; it owns no GPU, window or platform resources.\n"
    "#![forbid(unsafe_code)]\n\n"
    + definitions.strip()
    + "\n\n"
    + """#[cfg(test)]
mod tests {
    use super::{TerrainVertex, Vertex, UV_STEPS};

    #[test]
    fn vertex_layouts_match_the_upload_contract() {
        assert_eq!(std::mem::size_of::<Vertex>(), 44);
        assert_eq!(std::mem::size_of::<TerrainVertex>(), 24);
    }

    #[test]
    fn terrain_uv_round_trip_stays_within_half_a_fixed_point_step() {
        let uv = [0.37f32, 0.83f32];
        let vertex = TerrainVertex::new([0.0; 3], uv, 7, 0xAABB_CCDD, 0x1234);
        let decoded = vertex.uv();
        assert!((decoded[0] - uv[0]).abs() <= 0.5 / UV_STEPS);
        assert!((decoded[1] - uv[1]).abs() <= 0.5 / UV_STEPS);
        assert_eq!(vertex.layer, 7);
        assert_eq!(vertex.light, 0x1234);
        assert_eq!(vertex.color, 0xAABB_CCDD);
    }
}
"""
)
crate_manifest_text = """[package]
name = "horizon-render-types"
version = "0.1.0"
edition.workspace = true
license.workspace = true
rust-version.workspace = true
description = "GPU-independent vertex formats shared by Horizon rendering backends"

[dependencies]
bytemuck = { version = "1.24", features = ["derive"] }

[lints]
workspace = true
"""

crate.mkdir(parents=True, exist_ok=True)
(crate / "src").mkdir(parents=True, exist_ok=True)
crate_manifest.write_text(crate_manifest_text, encoding="utf-8")
crate_source.write_text(crate_source_text, encoding="utf-8")
workspace.write_text(workspace_source, encoding="utf-8")
render_manifest.write_text(manifest, encoding="utf-8")
mesher_path.write_text(mesher, encoding="utf-8")
print("Extracted Vertex and TerrainVertex into horizon-render-types.")
