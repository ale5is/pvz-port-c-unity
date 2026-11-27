#!/usr/bin/env python3
"""Remove Game's remaining adapter-info and GPU-timing dependencies from wgpu."""
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

old_metrics = """    pub pending_gpu_time_samples: usize,
}"""
new_metrics = """    pub pending_gpu_time_samples: usize,
    pub integrated_gpu: bool,
}"""
if "pub integrated_gpu: bool" not in a:
    if a.count(old_metrics) != 1:
        raise SystemExit("RendererMetrics shape changed")
    a = a.replace(old_metrics, new_metrics, 1)

old_info = """pub struct BackendInfo {
    pub kind: BackendKind,
    pub name: String,
    pub suspended: bool,
}"""
new_info = """pub struct BackendInfo {
    pub kind: BackendKind,
    pub name: String,
    pub backend_name: String,
    pub integrated_gpu: bool,
    pub suspended: bool,
}"""
if "pub backend_name: String" not in a:
    if a.count(old_info) != 1:
        raise SystemExit("BackendInfo shape changed")
    a = a.replace(old_info, new_info, 1)

test_old = """            name: "OpenGL ES 2.0".to_owned(),
            suspended: false,"""
test_new = """            name: "OpenGL ES 2.0".to_owned(),
            backend_name: "EGL/GLES2".to_owned(),
            integrated_gpu: true,
            suspended: false,"""
if 'backend_name: "EGL/GLES2"' not in a:
    if a.count(test_old) != 1:
        raise SystemExit("BackendInfo test fixture changed")
    a = a.replace(test_old, test_new, 1)

old_accessor = """            pending_gpu_time_samples: self.gpu_times.len(),
        }
    }

    /// Request a screenshot without exposing the renderer's internal slot."""
new_accessor = """            pending_gpu_time_samples: self.gpu_times.len(),
            integrated_gpu: self.info.device_type == wgpu::DeviceType::IntegratedGpu
                && !self.info.name.starts_with("Apple"),
        }
    }

    /// Return stable adapter identity without exposing wgpu's AdapterInfo type.
    pub fn backend_info(&self) -> horizon_render_api::BackendInfo {
        horizon_render_api::BackendInfo {
            kind: horizon_render_api::BackendKind::Wgpu,
            name: self.info.name.clone(),
            backend_name: format!("{:?}", self.info.backend),
            integrated_gpu: self.info.device_type == wgpu::DeviceType::IntegratedGpu
                && !self.info.name.starts_with("Apple"),
            suspended: self.suspended,
        }
    }

    /// Consume the latest GPU timing sample and clear older samples.
    pub fn take_gpu_time_ms(&mut self) -> Option<f32> {
        let latest = self.gpu_times.last().copied();
        self.gpu_times.clear();
        latest
    }

    /// Request a screenshot without exposing the renderer's internal slot."""
if "pub fn take_gpu_time_ms(&mut self)" not in g:
    if g.count(old_accessor) != 1:
        raise SystemExit("RendererMetrics accessor changed")
    g = g.replace(old_accessor, new_accessor, 1)

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
elif "Renderer::backend_info(self)" not in g:
    raise SystemExit("Could not locate wgpu backend_info implementation")

old_helper = """pub fn lod_distance_capped(distance: u32, info: &wgpu::AdapterInfo) -> u32 {
    let distance = distance.min(crate::lod::MAX_CHUNKS);
    if lod_capped(info) {
        distance.min(INTEGRATED_LOD_DISTANCE)
    } else {
        distance
    }
}"""
new_helper = old_helper + """

/// Backend-neutral equivalent used by Game after adapter details have been
/// reduced to a capability bit. GLES2 backends can report this without wgpu.
pub fn lod_distance_capped_for_backend(distance: u32, integrated_gpu: bool) -> u32 {
    let distance = distance.min(crate::lod::MAX_CHUNKS);
    if integrated_gpu {
        distance.min(INTEGRATED_LOD_DISTANCE)
    } else {
        distance
    }
}"""
if "pub fn lod_distance_capped_for_backend" not in gr:
    if gr.count(old_helper) != 1:
        raise SystemExit("Could not locate LOD cap helper")
    gr = gr.replace(old_helper, new_helper, 1)

m = re.sub(
    r"horizon_client::graphics::lod_distance_capped\\(distance,\\s*&renderer\\.info\\)",
    "horizon_client::graphics::lod_distance_capped_for_backend(distance, renderer.metrics().integrated_gpu)",
    m,
)
m = re.sub(
    r"horizon_client::graphics::lod_distance_capped\\(far_distance,\\s*&renderer\\.info\\)",
    "horizon_client::graphics::lod_distance_capped_for_backend(far_distance, renderer.metrics().integrated_gpu)",
    m,
)
m = re.sub(
    r"\\.record\\(frame_ms, cpu, renderer\\.gpu_times\\.last\\(\\)\\.copied\\(\\)\\);\\s*renderer\\.gpu_times\\.clear\\(\\);",
    ".record(frame_ms, cpu, renderer.take_gpu_time_ms());",
    m,
)
m = m.replace(
    "renderer.last_wait",
    "std::time::Duration::from_nanos(renderer.metrics().last_wait_ns)",
)
m = m.replace(
    '"gpu":self.renderer.as_ref().map(|r| &r.info.name),',
    '"gpu":self.renderer.as_ref().map(|r| r.backend_info().name),',
)
m = m.replace(
    '"backend":self.renderer.as_ref().map(|r| format!("{:?}",r.info.backend))',
    '"backend":self.renderer.as_ref().map(|r| r.backend_info().backend_name)',
)
for marker in ("renderer.info", "renderer.gpu_times", "renderer.last_wait", "r.info.name", "r.info.backend"):
    if marker in m:
        raise SystemExit(f"Game still accesses wgpu internals: {marker}")
if "lod_distance_capped_for_backend" not in m or "take_gpu_time_ms()" not in m:
    raise SystemExit("Game capability/timing migration incomplete")

for p, value in ((api, a), (gfx, g), (game, m), (graphics, gr)):
    p.write_text(value, encoding="utf-8")
print("Moved adapter identity, integrated-GPU policy and GPU timing behind backend-neutral APIs.")
