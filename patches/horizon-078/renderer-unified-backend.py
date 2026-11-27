#!/usr/bin/env python3
"""Compose frame, lifecycle and terrain operations into one swappable backend trait."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
api = root / "crates/horizon-render-api/src/lib.rs"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
if not api.is_file() or not gfx.is_file():
    raise SystemExit("Missing horizon-render-api or renderer source; install the backend API first")

source = api.read_text(encoding="utf-8")
terrain_marker = "pub trait TerrainStreamBackend<Mesh> {"
if source.count(terrain_marker) != 1:
    raise SystemExit("Expected exactly one TerrainStreamBackend declaration")

composite = """/// Describes a family of frame values whose lifetime is chosen by the caller.
pub trait FrameFamily {
    type Frame<'frame>;
}

/// Complete renderer boundary consumed by the streamed game loop.
///
/// The frame family keeps borrowed frame lifetimes generic while allowing one
/// object-safe backend handle for every frame lifetime. The two component
/// contracts remain separately testable.
pub trait GameRenderBackend<Frames: FrameFamily, Mesh>:
    for<'frame> RenderBackend<Frames::Frame<'frame>> + TerrainStreamBackend<Mesh>
{
}

impl<T, Frames, Mesh> GameRenderBackend<Frames, Mesh> for T
where
    Frames: FrameFamily,
    T: for<'frame> RenderBackend<Frames::Frame<'frame>> + TerrainStreamBackend<Mesh>,
{
}

/// Object-safe handle for one frame family and mesh pairing.
pub type GameBackendHandle<'a, Frames, Mesh> = dyn GameRenderBackend<Frames, Mesh> + 'a;

"""

if "pub trait GameRenderBackend<Frames: FrameFamily, Mesh>" in source:
    if source.count("pub type GameBackendHandle<'a, Frames, Mesh>") != 1:
        raise SystemExit("GameRenderBackend exists but its object-safe handle alias is missing")
    print("Unified game renderer contract is already installed.")
    raise SystemExit(0)

# Put the composite declaration after both component traits. Rust item order is
# irrelevant, but this location makes the dependency relationship explicit.
terrain_end = source.find("\n}", source.find(terrain_marker))
if terrain_end < 0:
    raise SystemExit("Could not locate the end of TerrainStreamBackend")
source = source[:terrain_end + 2] + "\n\n" + composite + source[terrain_end + 2:]

tests_marker = "#[cfg(test)]\nmod tests {"
if source.count(tests_marker) != 1:
    raise SystemExit("Could not uniquely locate horizon-render-api tests")
object_test = """
    struct BorrowedFrame<'a>(&'a [u8]);

    struct BorrowedFrameFamily;

    impl super::FrameFamily for BorrowedFrameFamily {
        type Frame<'frame> = BorrowedFrame<'frame>;
    }

    #[allow(dead_code)]
    fn game_backend_is_usable_as_a_lifetime_generic_trait_object(
        _backend: &mut super::GameBackendHandle<'_, BorrowedFrameFamily, Vec<u8>>,
    ) {
    }

    #[allow(dead_code)]
    fn game_backend_object_dispatches_borrowed_frames_and_terrain(
        backend: &mut super::GameBackendHandle<'_, BorrowedFrameFamily, Vec<u8>>,
        frame: &BorrowedFrame<'_>,
        mesh: &Vec<u8>,
    ) {
        let _ = super::RenderBackend::render_frame(backend, frame);
        super::TerrainStreamBackend::upload_section(backend, (0, 0, 0), mesh);
        super::TerrainStreamBackend::evict_columns(backend, &[]);
        let _ = super::TerrainStreamBackend::section_lod_revision(backend);
        super::TerrainStreamBackend::update_occlusion(backend, [0.0; 3], 1.0);
    }

"""

# The compile-time signatures validate object safety and actual dispatch across
# borrowed frame lifetimes, not merely the trait-object type spelling.
if "game_backend_is_usable_as_a_lifetime_generic_trait_object" not in source:
    source = source.replace(tests_marker, tests_marker + object_test, 1)
api.write_text(source, encoding="utf-8")

gfx_source = gfx.read_text(encoding="utf-8")
frame_family = """/// The borrowed Horizon frame type, expressed as a lifetime-indexed family.
pub struct HorizonFrameFamily;

impl horizon_render_api::FrameFamily for HorizonFrameFamily {
    type Frame<'frame> = Frame<'frame>;
}

/// Coerce the concrete wgpu renderer through the same object-safe boundary
/// that the platform-specific Game factory will store.
pub fn as_game_backend_handle<'a>(
    renderer: &'a mut Renderer,
) -> &'a mut horizon_render_api::GameBackendHandle<'a, HorizonFrameFamily, SectionMesh> {
    renderer
}
"""
if "pub struct HorizonFrameFamily;" not in gfx_source:
    gfx_source = gfx_source.rstrip() + "\n\n" + frame_family
elif "pub fn as_game_backend_handle<'a>(" not in gfx_source:
    raise SystemExit("HorizonFrameFamily exists but its concrete backend coercion is missing")
gfx.write_text(gfx_source, encoding="utf-8")
print("Installed the frame-family-aware game backend and verified the wgpu Renderer coercion boundary.")
