#!/usr/bin/env python3
import re
import sys
from pathlib import Path
if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_renderer_capabilities.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
def read(path):
    p = root / path
    if not p.is_file():
        raise SystemExit(f"FAIL: missing {path}")
    return p.read_text(encoding="utf-8")
api = read("crates/horizon-render-api/src/lib.rs")
gfx = read("crates/horizon-client-render/src/gfx.rs")
game = read("crates/horizon-client/src/game.rs")
graphics = read("crates/horizon-client-render/src/graphics.rs")
for marker in ("pub struct RendererMetrics", "pub integrated_gpu: bool", "pub last_encode_ns: u64",
               "pub struct BackendInfo", "pub backend_name: String", "pub is_gles: bool"):
    if marker not in api:
        raise SystemExit(f"FAIL: neutral API missing {marker}")
for marker in ("pub fn metrics(&self)", "pub fn backend_info(&self)", "pub fn take_gpu_time_ms(&mut self)",
               "pub fn gpu_time_samples(&self)", "pub fn clear_gpu_time_samples(&mut self)",
               "pub fn take_screenshot_result(&mut self)", "pub fn wait_for_background_work(&self)"):
    if marker not in gfx:
        raise SystemExit(f"FAIL: wgpu adapter missing {marker}")
for marker in ("lod_distance_capped_for_backend", "gpu_time_samples()", "clear_gpu_time_samples()",
               "take_screenshot_result()", "wait_for_background_work()", "backend_info().name",
               "backend_info().is_gles", "last_encode_ns"):
    if marker not in game:
        raise SystemExit(f"FAIL: Game migration missing {marker}")
for pattern in (r"\b(?:renderer|r)\.info\b", r"\b(?:renderer|r)\.gpu_times\b",
                r"\b(?:renderer|r)\.last_wait\b", r"\b(?:renderer|r)\.last_encode\b",
                r"\b(?:renderer|r)\.screenshot_result\b"):
    if re.search(pattern, game):
        raise SystemExit(f"FAIL: Game still accesses wgpu internals: {pattern}")
if "pub fn lod_distance_capped_for_backend" not in graphics:
    raise SystemExit("FAIL: backend-neutral LOD policy helper missing")
print("PASS: Game adapter identity, LOD capabilities, frame timings, screenshot results and background waits are backend-neutral.")
