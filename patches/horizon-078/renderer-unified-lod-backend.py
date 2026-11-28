#!/usr/bin/env python3
"""Add far-terrain LOD operations to the single swappable game backend."""
import argparse
from pathlib import Path
parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
api = root / "crates/horizon-render-api/src/lib.rs"
if not api.is_file():
    raise SystemExit("Missing horizon-render-api; install renderer and LOD APIs first")
source = api.read_text(encoding="utf-8")
old = "for<'frame> RenderBackend<Frames::Frame<'frame>> + TerrainStreamBackend<Mesh>"
new = old + " + LodStreamBackend<Mesh>"
if source.count(old) == 2:
    source = source.replace(old, new)
elif source.count(new) != 2:
    raise SystemExit("Expected both GameRenderBackend bounds to compose frame, terrain and LOD APIs")
test_anchor = "        super::TerrainStreamBackend::update_occlusion(backend, [0.0; 3], 1.0);\n"
test_calls = """        super::LodStreamBackend::clear_lods(backend);
        super::LodStreamBackend::queue_lods(backend, Vec::new(), Vec::new(), None);
        super::LodStreamBackend::upload_lods(backend, std::time::Duration::from_millis(1));
        let _ = super::LodStreamBackend::lod_detail_columns(backend);
"""
if source.count(test_anchor) == 1:
    source = source.replace(test_anchor, test_anchor + test_calls, 1)
elif source.count("super::LodStreamBackend::queue_lods(backend") != 1:
    raise SystemExit("Could not extend compile-time object-dispatch test with LOD operations")
api.write_text(source, encoding="utf-8")
print("Unified game backend now requires frame, terrain and far-terrain LOD operations.")
print("Compile-time object dispatch exercises every LOD method.")
