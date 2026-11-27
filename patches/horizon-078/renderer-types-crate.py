#!/usr/bin/env python3
"""Extract shared CPU render data into a standalone, wgpu-free crate."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
workspace = root / "Cargo.toml"
render = root / "crates/horizon-client-render"
mesher_path = render / "src/mesher.rs"
environment_path = render / "src/environment.rs"
render_manifest = render / "Cargo.toml"
crate = root / "crates/horizon-render-types"
crate_manifest = crate / "Cargo.toml"
crate_source = crate / "src/lib.rs"

if crate_source.exists():
    crate_text = crate_source.read_text(encoding="utf-8")
    mesher = mesher_path.read_text(encoding="utf-8")
    if "pub struct Vertex" not in crate_text or "pub struct TerrainVertex" not in crate_text:
        raise SystemExit("Existing horizon-render-types crate is missing vertex formats")
    if "pub use horizon_render_types::{" not in mesher:
        raise SystemExit("Existing vertex formats are not re-exported by mesher")
else:
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

    crate_text = (
        "//! GPU-independent render data shared by Horizon rendering backends.\n"
        "//! This crate owns no GPU, window, EGL or platform resources.\n"
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
    crate.mkdir(parents=True, exist_ok=True)
    (crate / "src").mkdir(parents=True, exist_ok=True)
    crate_manifest.write_text(
        """[package]
name = "horizon-render-types"
version = "0.1.0"
edition.workspace = true
license.workspace = true
rust-version.workspace = true
description = "GPU-independent render data shared by Horizon rendering backends"

[dependencies]
bytemuck = { version = "1.24", features = ["derive"] }

[lints]
workspace = true
""",
        encoding="utf-8",
    )
    workspace.write_text(workspace_source, encoding="utf-8")
    render_manifest.write_text(manifest, encoding="utf-8")
    mesher_path.write_text(mesher, encoding="utf-8")
    crate_source.write_text(crate_text, encoding="utf-8")

environment = environment_path.read_text(encoding="utf-8")
if "pub struct Sky" in crate_text:
    if "pub use horizon_render_types::Sky;" not in environment:
        raise SystemExit("Sky exists in neutral crate but environment.rs does not re-export it")
    print("GPU-independent Sky frame data is already installed.")
else:
    start_marker = "#[derive(Clone, Copy, Debug, Default)]\npub struct Sky {"
    end_marker = "\nimpl Environment {"
    if environment.count(start_marker) != 1 or environment.count(end_marker) != 1:
        raise SystemExit("Could not uniquely locate the pinned Sky data structure")
    start = environment.index(start_marker)
    end = environment.index(end_marker, start)
    if end <= start:
        raise SystemExit("Invalid Sky extraction range")
    sky = environment[start:end].strip()
    if "pub struct Sky" not in sky:
        raise SystemExit("Sky extraction did not capture the expected struct")

    environment_anchor = "use std::{collections::HashMap, fs, path::Path};\n"
    if environment.count(environment_anchor) != 1:
        raise SystemExit("Could not uniquely locate environment.rs imports")
    environment = environment[:start] + environment[end:]
    environment = environment.replace(
        environment_anchor,
        environment_anchor + "pub use horizon_render_types::Sky;\n",
    )
    test_anchor = "\n#[cfg(test)]\nmod tests {"
    if crate_text.count(test_anchor) != 1:
        raise SystemExit("Could not locate neutral crate tests to insert Sky")
    crate_text = crate_text.replace(
        test_anchor,
        "\n\n" + sky + "\n\n#[cfg(test)]\nmod tests {",
    )
    crate_text = crate_text.replace(
        "use super::{TerrainVertex, Vertex, UV_STEPS};",
        "use super::{Sky, TerrainVertex, Vertex, UV_STEPS};",
    )
    crate_text = crate_text.replace(
        "    #[test]\n    fn vertex_layouts_match_the_upload_contract() {",
        "    #[test]\n    fn sky_frame_data_is_a_compact_plain_value() {\n"
        "        assert_eq!(std::mem::size_of::<Sky>(), 128);\n"
        "        assert_eq!(Sky::default().sky_light_factor, 0.0);\n"
        "    }\n\n"
        "    #[test]\n    fn vertex_layouts_match_the_upload_contract() {",
    )
    environment_path.write_text(environment, encoding="utf-8")
    crate_source.write_text(crate_text, encoding="utf-8")
    print("Extracted Sky frame data into horizon-render-types.")


screen_path = render / "src/screen_effects.rs"
screen = screen_path.read_text(encoding="utf-8")
screen_reexport = "pub use horizon_render_types::{ScreenBlend as Blend, ScreenQuad, ScreenVertex};\n"
if screen_reexport in screen:
    if any(name not in crate_text for name in ("pub enum ScreenBlend", "pub struct ScreenVertex", "pub struct ScreenQuad")):
        raise SystemExit("Screen-effect compatibility re-export exists but neutral data types are incomplete")
    print("Screen-effect frame data is already in horizon-render-types.")
else:
    def extract_screen_block(source: str, start: str, end: str, label: str) -> tuple[str, str]:
        if source.count(start) != 1 or source.count(end) != 1:
            raise SystemExit(f"Could not uniquely locate {label} boundaries")
        first = source.index(start)
        last = source.index(end, first)
        if last <= first:
            raise SystemExit(f"Invalid {label} extraction range")
        return source[:first] + source[last:], source[first:last]

    screen, screen_blend = extract_screen_block(
        screen,
        "#[derive(Clone, Copy, Debug, PartialEq, Eq)]\npub enum Blend {",
        "\n/// One clip-space vertex of a screen quad.",
        "screen blend",
    )
    screen, screen_vertex = extract_screen_block(
        screen,
        "/// One clip-space vertex of a screen quad.\n#[repr(C)]\n#[derive(Clone, Copy, Debug, bytemuck::Pod, bytemuck::Zeroable)]\npub struct ScreenVertex {",
        "\n/// Linear filtering (`TextureMetadataSection.blur`), else nearest.",
        "screen vertex",
    )
    screen, screen_quad = extract_screen_block(
        screen,
        "#[derive(Clone, Debug)]\npub struct ScreenQuad {",
        "\n/// What the camera sees this frame.",
        "screen quad",
    )
    screen_blend = screen_blend.replace("pub enum Blend {", "pub enum ScreenBlend {")
    screen_quad = screen_quad.replace("pub blend: Blend,", "pub blend: ScreenBlend,")
    screen_quad = screen_quad.replace("/// How the sprite is sampled: [`SAMPLE_BLUR`] and [`SAMPLE_CLAMP`].", "/// How the sprite is sampled: the sample blur and clamp flags.")
    if "pub enum ScreenBlend" not in screen_blend or "pub struct ScreenVertex" not in screen_vertex:
        raise SystemExit("Failed to rename/extract screen-effect CPU types")
    if "pub blend: ScreenBlend" not in screen_quad:
        raise SystemExit("ScreenQuad still refers to the renderer-local Blend enum")

    screen_types = "\n\n".join((screen_blend.strip(), screen_vertex.strip(), screen_quad.strip()))
    test_anchor = "#[cfg(test)]\nmod tests {"
    if crate_text.count(test_anchor) != 1:
        raise SystemExit("Could not locate neutral render-data tests")
    crate_text = crate_text.replace(test_anchor, screen_types + "\n\n" + test_anchor)
    test_import = "use super::{Sky, TerrainVertex, Vertex, UV_STEPS};"
    if crate_text.count(test_import) != 1:
        raise SystemExit("Could not uniquely locate neutral render-data test imports")
    crate_text = crate_text.replace(
        test_import,
        "use super::{ScreenVertex, Sky, TerrainVertex, Vertex, UV_STEPS};",
    )
    layout_test = "    #[test]\n    fn vertex_layouts_match_the_upload_contract() {"
    if crate_text.count(layout_test) != 1:
        raise SystemExit("Could not locate neutral vertex layout test")
    crate_text = crate_text.replace(
        layout_test,
        "    #[test]\n    fn screen_vertex_layout_matches_the_upload_contract() {\n"
        "        assert_eq!(std::mem::size_of::<ScreenVertex>(), 48);\n"
        "    }\n\n" + layout_test,
    )

    screen_import_anchor = "use std::collections::HashMap;\n"
    if screen.count(screen_import_anchor) != 1:
        raise SystemExit("Could not locate screen_effects.rs imports")
    screen = screen.replace(
        screen_import_anchor,
        screen_import_anchor + screen_reexport,
    )
    screen_path.write_text(screen, encoding="utf-8")
    crate_source.write_text(crate_text, encoding="utf-8")
    print("Extracted ScreenVertex, ScreenQuad and screen blending data into horizon-render-types.")

print("GPU-independent render data extraction completed.")
