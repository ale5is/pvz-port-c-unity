#!/usr/bin/env python3
"""Audit whether the CPU frame contract lives in a wgpu-free crate."""
import re
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_render_crate_boundary.py <horizon-source-root>")

root = Path(sys.argv[1]).resolve()
render = root / "crates/horizon-client-render"
frame = (render / "src/frame.rs").read_text(encoding="utf-8")
manifest = (render / "Cargo.toml").read_text(encoding="utf-8")
lib = (render / "src/lib.rs").read_text(encoding="utf-8")

if "pub struct Frame<'a>" not in frame or "pub struct BorderDraw" not in frame:
    raise SystemExit("FAIL: expected extracted Frame and BorderDraw CPU contracts")
if "wgpu::" in frame or "crate::gfx::" in frame:
    raise SystemExit("FAIL: frame.rs directly references the wgpu implementation")
if "#![forbid(unsafe_code)]" not in lib:
    raise SystemExit("FAIL: renderer crate must keep unsafe EGL/GL FFI outside this crate")

wgpu_dependencies = [
    line.strip() for line in manifest.splitlines()
    if re.match(r"^wgpu\s*=", line.strip())
]
if not wgpu_dependencies:
    raise SystemExit("FAIL: audit expected the current horizon-client-render wgpu dependency")

fields = re.findall(r"(?m)^\s*pub [a-zA-Z_][a-zA-Z0-9_]*:", frame)
print(f"PASS: Frame source is CPU-only ({len(fields)} public data fields counted)")
print("PASS: unsafe EGL/GL FFI remains forbidden in horizon-client-render")
print("BLOCKER: horizon-client-render itself still depends on wgpu:")
for dependency in wgpu_dependencies:
    print(f"  {dependency}")
print("BLOCKER: frame.rs cannot yet be consumed as a standalone backend-neutral crate.")
print("NEXT: extract Vertex, HudBatch, Sky, ScreenQuad and other referenced CPU types")
print("      into a wgpu-free contract crate before introducing a production backend trait.")
