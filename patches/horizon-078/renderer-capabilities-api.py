#!/usr/bin/env python3
"""Move adapter identity, timing and renderer-only operations behind neutral APIs."""
import argparse
import re
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
api = root / "crates/horizon-render-api/src/lib.rs"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
game = root / "crates/horizon-client/src/game.rs"
graphics = root / "crates/horizon-client-render/src/graphics.rs"
for path in (api, gfx, game, graphics):
    if not path.is_file():
        raise SystemExit(f"Missing expected source: {path}")
a, g, m, gr = [p.read_text(encoding="utf-8") for p in (api, gfx, game, graphics)]

if "pub integrated_gpu: bool" not in a:
    old = """    pub pending_gpu_time_samples: usize,
}"""
    new = """    pub pending_gpu_time_samples: usize,
    pub integrated_gpu: bool,
    pub last_encode_ns: u64,
}"""
    if a.count(old) != 1:
        raise SystemExit("RendererMetrics shape changed")
    a = a.replace(old, new, 1)
elif "pub last_encode_ns: u64" not in a:
    a = a.replace("    pub integrated_gpu: bool,\n}", "    pub integrated_gpu: bool,\n    pub last_encode_ns: u64,\n}", 1)

if "pub backend_name: String" not in a:
    old = """pub struct BackendInfo {
    pub kind: BackendKind,
    pub name: String,
    pub suspended: bool,
}"""
    new = """pub struct BackendInfo {
    pub kind: BackendKind,
    pub name: String,
    pub backend_name: String,
    pub integrated_gpu: bool,
    pub is_gles: bool,
    pub suspended: bool,
}"""
    if a.count(old) != 1:
        raise SystemExit("BackendInfo shape changed")
    a = a.replace(old, new, 1)
elif "pub is_gles: bool" not in a:
    a = a.replace("    pub integrated_gpu: bool,\n    pub suspended: bool,", "    pub integrated_gpu: bool,\n    pub is_gles: bool,\n    pub suspended: bool,", 1)

test_old = """            name: "OpenGL ES 2.0".to_owned(),
            suspended: false,"""
test_new = """            name: "OpenGL ES 2.0".to_owned(),
            backend_name: "EGL/GLES2".to_owned(),
            integrated_gpu: true,
            is_gles: true,
            suspended: false,"""
if 'backend_name: "EGL/GLES2"' not in a:
    if a.count(test_old) != 1:
        raise SystemExit("BackendInfo test fixture changed")
    a = a.replace(test_old, test_new, 1)

if 'is_gles: false' not in a:
    a = a.replace(
        '            integrated_gpu: true,\n            suspended: false,',
        '            integrated_gpu: true,\n            is_gles: true,\n            suspended: false,',
        1,
    )

if "last_encode_ns: self.last_encode.as_nanos()" not in g:
    pattern = r"(pending_gpu_time_samples:\s*self\.gpu_times\.len\(\),\s*)}"
    def add_metrics(match):
        return match.group(1) + """integrated_gpu: self.info.device_type == wgpu::DeviceType::IntegratedGpu
                && !self.info.name.starts_with("Apple"),
            last_encode_ns: self.last_encode.as_nanos().min(u128::from(u64::MAX)) as u64,
        }"""
    g, count = re.subn(pattern, add_metrics, g, count=1)
    if count != 1:
        raise SystemExit("Could not update RendererMetrics adapter")

if "pub fn backend_info(&self)" not in g:
    marker = "    /// Request a screenshot without exposing the renderer's internal slot."
    backend_method = """    /// Return stable adapter identity without exposing wgpu's AdapterInfo type.
    pub fn backend_info(&self) -> horizon_render_api::BackendInfo {
        horizon_render_api::BackendInfo {
            kind: horizon_render_api::BackendKind::Wgpu,
            name: self.info.name.clone(),
            backend_name: format!("{:?}", self.info.backend),
            integrated_gpu: self.info.device_type == wgpu::DeviceType::IntegratedGpu
                && !self.info.name.starts_with("Apple"),
            is_gles: self.info.backend == wgpu::Backend::Gl,
            suspended: self.suspended,
        }
    }

"""
    if g.count(marker) != 1:
        raise SystemExit("Could not locate backend info insertion point")
    g = g.replace(marker, backend_method + marker, 1)

if "pub fn gpu_time_samples(&self)" not in g:
    marker = "    /// Request a screenshot without exposing the renderer's internal slot."
    methods = """    /// Read timing samples through a backend-neutral slice.
    pub fn gpu_time_samples(&self) -> &[f32] {
        &self.gpu_times
    }

    /// Clear timing samples after all frame-stat consumers have read them.
    pub fn clear_gpu_time_samples(&mut self) {
        self.gpu_times.clear();
    }

    /// Take the completed screenshot result without exposing its storage slot.
    pub fn take_screenshot_result(&mut self) -> Option<std::result::Result<[u32; 2], String>> {
        self.screenshot_result.take()
    }

    /// Wait for background uploads before the renderer is handed to the frame thread.
    pub fn wait_for_background_work(&self) {
        let _ = self.device.poll(wgpu::PollType::wait_indefinitely());
    }

"""
    if g.count(marker) != 1:
        raise SystemExit("Could not locate renderer accessor insertion point")
    g = g.replace(marker, methods + marker, 1)

old_trait = """    fn backend_info(&self) -> horizon_render_api::BackendInfo {
        horizon_render_api::BackendInfo {
            kind: horizon_render_api::BackendKind::Wgpu,
            name: self.info.name.clone(),
            suspended: self.suspended,
        }
    }"""
new_trait = """    fn backend_info(&self) -> horizon_render_api::BackendInfo {
        Renderer::backend_info(self)
    }"""
if old_trait in g:
    g = g.replace(old_trait, new_trait, 1)

old_helper = """pub fn lod_distance_capped(distance: u32, info: &wgpu::AdapterInfo) -> u32 {
    let distance = distance.min(crate::lod::MAX_CHUNKS);
    if lod_capped(info) {
        distance.min(INTEGRATED_LOD_DISTANCE)
    } else {
        distance
    }
}"""
if "pub fn lod_distance_capped_for_backend" not in gr:
    if gr.count(old_helper) != 1:
        raise SystemExit("Could not locate LOD cap helper")
    gr = gr.replace(old_helper, old_helper + """

/// Backend-neutral LOD policy for renderers that do not expose wgpu adapters.
pub fn lod_distance_capped_for_backend(distance: u32, integrated_gpu: bool) -> u32 {
    let distance = distance.min(crate::lod::MAX_CHUNKS);
    if integrated_gpu {
        distance.min(INTEGRATED_LOD_DISTANCE)
    } else {
        distance
    }
}
""", 1)

m = re.sub(
    r"horizon_client::graphics::lod_distance_capped\((distance|far_distance),\s*&renderer\.info\)",
    lambda match: f"horizon_client::graphics::lod_distance_capped_for_backend({match.group(1)}, renderer.metrics().integrated_gpu)",
    m,
)
m = re.sub(r"&r\.info\.name", "r.backend_info().name", m)
m = re.sub(r"r\.info\.backend\s*==\s*wgpu::Backend::Gl", "r.backend_info().is_gles", m)
m = re.sub(
    r"\b(r|renderer)\.gpu_times\.last\(\)\.copied\(\)",
    r"\1.gpu_time_samples().last().copied()",
    m,
)
m = re.sub(
    r"\b(r|renderer)\.gpu_times\.last\(\)",
    r"\1.gpu_time_samples().last()",
    m,
)
m = re.sub(
    r"&\b(r|renderer)\.gpu_times\b",
    r"\1.gpu_time_samples()",
    m,
)
m = re.sub(
    r"\b(r|renderer)\.gpu_times\.clear\(\)",
    r"\1.clear_gpu_time_samples()",
    m,
)
m = re.sub(
    r"\b(r|renderer)\.last_wait\b",
    r"std::time::Duration::from_nanos(\1.metrics().last_wait_ns)",
    m,
)
m = re.sub(
    r"\br\.last_encode\b",
    "std::time::Duration::from_nanos(r.metrics().last_encode_ns)",
    m,
)
m = m.replace(
    ".and_then(|r| r.screenshot_result.take())",
    ".and_then(|r| r.take_screenshot_result())",
)
m = m.replace(
    "r.device.poll(wgpu::PollType::wait_indefinitely())",
    "r.wait_for_background_work()",
)
m = m.replace(
    "self.renderer.as_ref().map(|r| &r.info.name)",
    "self.renderer.as_ref().map(|r| r.backend_info().name)",
)

for pattern in (
    r"\b(?:renderer|r)\.info\b",
    r"\b(?:renderer|r)\.gpu_times\b",
    r"\b(?:renderer|r)\.last_wait\b",
    r"\b(?:renderer|r)\.last_encode\b",
    r"\b(?:renderer|r)\.screenshot_result\b",
):
    if re.search(pattern, m):
        raise SystemExit(f"Game still accesses wgpu renderer internals: {pattern}")
if "lod_distance_capped_for_backend" not in m:
    raise SystemExit("Game LOD policy migration incomplete")
if "gpu_time_samples()" not in m or "clear_gpu_time_samples()" not in m:
    raise SystemExit("Game GPU timing migration incomplete")
if "take_screenshot_result()" not in m or "wait_for_background_work()" not in m:
    raise SystemExit("Game renderer operation migration incomplete")

for path, value in ((api, a), (gfx, g), (game, m), (graphics, gr)):
    path.write_text(value, encoding="utf-8")
print("Moved adapter identity, GPU capabilities, frame timings, screenshot results and background waits behind renderer APIs.")
