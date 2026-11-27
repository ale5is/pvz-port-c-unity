#!/usr/bin/env python3
"""Expose stable backend capabilities while reusing the existing Game-side boundary."""
import argparse
import re
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument("source",type=Path)
root=parser.parse_args().source.resolve()
api=root/"crates/horizon-render-api/src/lib.rs"
gfx=root/"crates/horizon-client-render/src/gfx.rs"
game=root/"crates/horizon-client/src/game.rs"
for p in (api,gfx,game):
    if not p.is_file():
        raise SystemExit(f"Missing source: {p}")
a=api.read_text(encoding="utf-8")
g=gfx.read_text(encoding="utf-8")
m=game.read_text(encoding="utf-8")

if "fn sprite_layout_matches(&self, layers: u32) -> bool;" not in a:
    anchor = "    fn backend_info(&self) -> BackendInfo;"
    if a.count(anchor) != 1:
        raise SystemExit("Could not uniquely locate RenderBackend backend_info declaration")
    a = a.replace(anchor, "    fn sprite_layout_matches(&self, layers: u32) -> bool;\n" + anchor, 1)

if "pub integrated_gpu: bool" not in a:
    old="""    pub pending_gpu_time_samples: usize,
}"""
    new="""    pub pending_gpu_time_samples: usize,
    pub integrated_gpu: bool,
    pub last_encode_ns: u64,
}"""
    if a.count(old)!=1:
        raise SystemExit("RendererMetrics API shape changed")
    a=a.replace(old,new,1)
elif "pub last_encode_ns: u64" not in a:
    a=a.replace("    pub integrated_gpu: bool,\n}","    pub integrated_gpu: bool,\n    pub last_encode_ns: u64,\n}",1)

if "pub backend_name: String" not in a:
    old="""pub struct BackendInfo {
    pub kind: BackendKind,
    pub name: String,
    pub suspended: bool,
}"""
    new="""pub struct BackendInfo {
    pub kind: BackendKind,
    pub name: String,
    pub backend_name: String,
    pub integrated_gpu: bool,
    pub is_gles: bool,
    pub suspended: bool,
}"""
    if a.count(old)!=1:
        raise SystemExit("BackendInfo API shape changed")
    a=a.replace(old,new,1)

test_old="""            name: "OpenGL ES 2.0".to_owned(),
            suspended: false,"""
test_new="""            name: "OpenGL ES 2.0".to_owned(),
            backend_name: "EGL/GLES2".to_owned(),
            integrated_gpu: true,
            is_gles: true,
            suspended: false,"""
if 'backend_name: "EGL/GLES2"' not in a:
    if a.count(test_old)!=1:
        raise SystemExit("BackendInfo test fixture changed")
    a=a.replace(test_old,test_new,1)

if "last_encode_ns: self.last_encode.as_nanos()" not in g:
    pattern=r"(pending_gpu_time_samples:\s*self\.gpu_times\.len\(\),\s*)}"
    def extend_metrics(match):
        return match.group(1)+"""integrated_gpu: self.info.device_type == wgpu::DeviceType::IntegratedGpu
                && !self.info.name.starts_with("Apple"),
            last_encode_ns: self.last_encode.as_nanos().min(u128::from(u64::MAX)) as u64,
        }"""
    g,count=re.subn(pattern,extend_metrics,g,count=1)
    if count!=1:
        raise SystemExit("Could not extend RendererMetrics implementation")

if "fn sprite_layout_matches(&self, layers: u32) -> bool {" not in g:
    marker = "    fn backend_info(&self) -> horizon_render_api::BackendInfo {"
    if g.count(marker) != 1:
        raise SystemExit("Could not uniquely locate wgpu RenderBackend backend_info method")
    g = g.replace(
        marker,
        "    fn sprite_layout_matches(&self, layers: u32) -> bool {\n        Renderer::matches_sprite_layout(self, layers)\n    }\n\n" + marker,
        1,
    )

impl_marker="impl<'frame> horizon_render_api::RenderBackend<Frame<'frame>> for Renderer {"
backend_method="""impl Renderer {
    /// Backend-neutral adapter identity and coarse capabilities for diagnostics.
    pub fn backend_info(&self) -> horizon_render_api::BackendInfo {
        horizon_render_api::BackendInfo {
            kind: horizon_render_api::BackendKind::Wgpu,
            name: self.adapter_name().to_owned(),
            backend_name: self.backend_name(),
            integrated_gpu: self.info.device_type == wgpu::DeviceType::IntegratedGpu
                && !self.info.name.starts_with("Apple"),
            is_gles: self.is_gl_backend(),
            suspended: self.is_suspended(),
        }
    }
}

"""
if "pub fn backend_info(&self)" not in g:
    if g.count(impl_marker)!=1:
        raise SystemExit("Could not locate renderer backend trait implementation")
    g=g.replace(impl_marker,backend_method+impl_marker,1)

old_trait="""    fn backend_info(&self) -> horizon_render_api::BackendInfo {
        horizon_render_api::BackendInfo {
            kind: horizon_render_api::BackendKind::Wgpu,
            name: self.info.name.clone(),
            suspended: self.suspended,
        }
    }"""
new_trait="""    fn backend_info(&self) -> horizon_render_api::BackendInfo {
        Renderer::backend_info(self)
    }"""
if old_trait in g:
    g=g.replace(old_trait,new_trait,1)
elif "Renderer::backend_info(self)" not in g:
    raise SystemExit("Could not delegate RenderBackend::backend_info")

old_layout_call = "previous.matches_sprite_layout(loaded.pack.render.texture_layers)"
new_layout_call = "horizon_render_api::RenderBackend::sprite_layout_matches(previous, loaded.pack.render.texture_layers)"
if old_layout_call in m:
    m = m.replace(old_layout_call, new_layout_call, 1)
elif new_layout_call not in m:
    raise SystemExit("Could not locate the loading renderer sprite-layout compatibility query")

required_game_calls=(
    "renderer.lod_distance_capped(distance)",
    "renderer.lod_distance_capped(far_distance)",
    "r.adapter_name()",
    "r.backend_name()",
    "previous.is_gl_backend()",
    "previous.is_vulkan_backend()",
    "horizon_render_api::RenderBackend::sprite_layout_matches(previous, loaded.pack.render.texture_layers)",
    "r.is_gl_backend()",
    "RenderBackend::metrics(r).last_wait_ns",
    "RenderBackend::metrics(r).last_encode_ns",
    "r.take_latest_gpu_time()",
    "r.take_gpu_times()",
    "r.take_screenshot_result()",
    "r.wait_idle()",
    "r.is_suspended()",
    "previous.restore_after_renderer_handoff()",
)
for marker in required_game_calls:
    if marker not in m:
        raise SystemExit(f"Game did not inherit the established renderer boundary: {marker}")

for pattern in (
    r"\b(?:renderer|previous|r)\.(?:device|queue|surface|spare_surface|instance|adapter|config|"
    r"screenshot_result|screenshot|drawn_sections|last_wait|last_encode|gpu_times|info|hud|suspended)\b",
):
    found=re.findall(pattern,m)
    if found:
        raise SystemExit(f"Game still reads wgpu renderer fields: {found[:8]}")

api.write_text(a,encoding="utf-8")
gfx.write_text(g,encoding="utf-8")
game.write_text(m,encoding="utf-8")
print("Added neutral adapter capability/identity fields; verified Game uses the pre-existing renderer boundary.")
