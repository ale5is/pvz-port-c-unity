#!/usr/bin/env python3
"""Guard Game against direct wgpu adapter and timing internals."""
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
for marker in ("pub struct RendererMetrics", "pub integrated_gpu: bool", "pub struct BackendInfo", "pub backend_name: String"):
    if marker not in api:
        raise SystemExit(f"FAIL: neutral API missing {marker}")
for marker in ("pub fn metrics(&self)", "pub fn backend_info(&self)", "pub fn take_gpu_time_ms(&mut self)"):
    if marker not in gfx:
        raise SystemExit(f"FAIL: wgpu adapter missing {marker}")
for marker in ("lod_distance_capped_for_backend", "take_gpu_time_ms()", "backend_info().name", "backend_info().backend_name"):
    if marker not in game:
        raise SystemExit(f"FAIL: Game migration missing {marker}")
for marker in ("renderer.info", "renderer.gpu_times", "r.info.name", "r.info.backend"):
    if marker in game:
        raise SystemExit(f"FAIL: Game still depends on wgpu internals: {marker}")
if "pub fn lod_distance_capped_for_backend" not in graphics:
    raise SystemExit("FAIL: backend-neutral LOD policy helper missing")
print("PASS: Game adapter identity, integrated-GPU policy and GPU timing are backend-neutral.")
