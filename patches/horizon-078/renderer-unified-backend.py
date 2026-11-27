#!/usr/bin/env python3
"""Compose frame, lifecycle and terrain operations into one swappable backend trait."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
api = root / "crates/horizon-render-api/src/lib.rs"
if not api.is_file():
    raise SystemExit("Missing horizon-render-api; install the frame backend API first")

source = api.read_text(encoding="utf-8")
terrain_marker = "pub trait TerrainStreamBackend<Mesh> {"
if source.count(terrain_marker) != 1:
    raise SystemExit("Expected exactly one TerrainStreamBackend declaration")

composite = """/// Complete renderer boundary consumed by the streamed game loop.
///
/// The two component traits remain separately testable, while this marker trait
/// gives Game and Launcher one backend type that owns both frame submission and
/// streamed terrain residency. The blanket implementation keeps backend
/// implementations focused on the actual operations instead of boilerplate.
pub trait GameRenderBackend<Frame, Mesh>:
    RenderBackend<Frame> + TerrainStreamBackend<Mesh>
{
}

impl<T, Frame, Mesh> GameRenderBackend<Frame, Mesh> for T
where
    T: RenderBackend<Frame> + TerrainStreamBackend<Mesh>,
{
}

/// Object-safe handle for one concrete frame/mesh pairing.
pub type GameBackendHandle<'a, Frame, Mesh> = dyn GameRenderBackend<Frame, Mesh> + 'a;

"""
if "pub trait GameRenderBackend<Frame, Mesh>" in source:
    if source.count("pub type GameBackendHandle<'a, Frame, Mesh>") != 1:
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
    #[allow(dead_code)]
    fn game_backend_is_usable_as_a_lifetime_generic_trait_object(
        _backend: &dyn for<'frame> super::GameRenderBackend<BorrowedFrame<'frame>, Vec<u8>>,
    ) {
    }

    struct BorrowedFrame<'a>(&'a [u8]);

"""
# The compile-time signature validates the exact higher-ranked trait-object
# shape needed for Horizon's borrowed Frame<'frame>.
if "game_backend_is_usable_as_a_lifetime_generic_trait_object" not in source:
    source = source.replace(tests_marker, tests_marker + object_test, 1)
api.write_text(source, encoding="utf-8")
print("Installed the combined GameRenderBackend trait and HRTB trait-object handle.")
