#!/usr/bin/env python3
import re
import sys
from pathlib import Path
if len(sys.argv)!=2:
    raise SystemExit("usage: check_android5_renderer_capabilities.py <horizon-source-root>")
root=Path(sys.argv[1]).resolve()
def read(path):
    p=root/path
    if not p.is_file():
        raise SystemExit(f"FAIL: missing {path}")
    return p.read_text(encoding="utf-8")
api=read("crates/horizon-render-api/src/lib.rs")
gfx=read("crates/horizon-client-render/src/gfx.rs")
game=read("crates/horizon-client/src/game.rs")
for marker in ("pub struct RendererMetrics","pub integrated_gpu: bool","pub last_encode_ns: u64",
               "pub struct BackendInfo","pub backend_name: String","pub is_gles: bool",
               "fn sprite_layout_matches(&self, layers: u32) -> bool;"):
    if marker not in api:
        raise SystemExit(f"FAIL: neutral API missing {marker}")
for marker in ("pub fn metrics(&self)","pub fn backend_info(&self)","pub fn lod_distance_capped(&self",
               "pub fn take_latest_gpu_time(&mut self)","pub fn take_gpu_times(&mut self)",
               "pub fn last_wait_duration(&self)","pub fn last_encode_duration(&self)",
               "pub fn adapter_name(&self)","pub fn backend_name(&self)","pub fn is_gl_backend(&self)"):
    if marker not in gfx:
        raise SystemExit(f"FAIL: renderer adapter missing {marker}")
for marker in ("renderer.lod_distance_capped(distance)","renderer.lod_distance_capped(far_distance)",
               "r.adapter_name()","r.backend_name()","previous.is_gl_backend()",
               "previous.is_vulkan_backend()","RenderBackend::sprite_layout_matches(previous, loaded.pack.render.texture_layers)","r.is_gl_backend()","r.take_latest_gpu_time()",
               "r.take_gpu_times()","RenderBackend::metrics(r).last_wait_ns","RenderBackend::metrics(r).last_encode_ns"):
    if marker not in game:
        raise SystemExit(f"FAIL: Game boundary missing {marker}")
pattern=re.compile(r"\b(?:renderer|previous|r)\.(?:device|queue|surface|spare_surface|instance|adapter|config|"
                   r"screenshot_result|screenshot|drawn_sections|last_wait|last_encode|gpu_times|info|hud|suspended)\b")
if pattern.search(game):
    raise SystemExit("FAIL: Game still reads wgpu renderer internals")

for marker in (
    "RenderBackend::metrics(renderer).drawn_sections",
    "RenderBackend::metrics(renderer).screenshot_pending",
    "RenderBackend::metrics(r).last_wait_ns",
    "RenderBackend::metrics(r).last_encode_ns",
):
    if marker not in game:
        raise SystemExit(f"FAIL: Game does not use neutral renderer metrics: {marker}")
print("PASS: Game uses backend-neutral LOD, timing, screenshot, lifecycle and adapter accessors.")
