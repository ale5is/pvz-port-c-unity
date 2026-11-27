#!/usr/bin/env python3
"""Verify independent vertex and sky data types and compatibility re-exports."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_render_types_crate.py <horizon-source-root>")

root = Path(sys.argv[1]).resolve()
crate = root / "crates/horizon-render-types"
source = (crate / "src/lib.rs").read_text(encoding="utf-8")
manifest = (crate / "Cargo.toml").read_text(encoding="utf-8")
workspace = (root / "Cargo.toml").read_text(encoding="utf-8")
render_manifest = (root / "crates/horizon-client-render/Cargo.toml").read_text(encoding="utf-8")
mesher = (root / "crates/horizon-client-render/src/mesher.rs").read_text(encoding="utf-8")
environment = (root / "crates/horizon-client-render/src/environment.rs").read_text(encoding="utf-8")

for declaration in ("pub struct Vertex", "pub struct TerrainVertex", "pub const CULL_BACK", "pub const UV_STEPS", "pub const TILED_UV_STEPS", "pub struct Sky"):
    if declaration not in source:
        raise SystemExit(f"FAIL: missing neutral render-data declaration: {declaration}")
if "wgpu" in manifest or "wgpu::" in source or "winit" in manifest or "egl" in manifest.lower():
    raise SystemExit("FAIL: horizon-render-types must not depend on a GPU or window backend")
if "bytemuck" not in manifest or "horizon-render-types" not in workspace:
    raise SystemExit("FAIL: missing bytemuck dependency or workspace membership")
if 'horizon-render-types = { path = "../horizon-render-types" }' not in render_manifest:
    raise SystemExit("FAIL: renderer crate does not depend on horizon-render-types")
if "pub use horizon_render_types::{" not in mesher:
    raise SystemExit("FAIL: existing mesher type paths are not preserved")
if "pub struct Vertex" in mesher or "pub struct TerrainVertex" in mesher:
    raise SystemExit("FAIL: duplicate vertex definitions remain in mesher.rs")
if "pub use horizon_render_types::Sky;" not in environment or "pub struct Sky" in environment:
    raise SystemExit("FAIL: environment::Sky compatibility re-export is missing")
if "#![forbid(unsafe_code)]" not in source:
    raise SystemExit("FAIL: neutral render-data crate must forbid unsafe code")
print("PASS: Vertex, TerrainVertex and Sky live in a standalone bytemuck-only crate.")
print("PASS: mesher and environment re-exports preserve Horizon's existing type paths.")
