#!/usr/bin/env python3
"""Keep HUD setup and framebuffer sizing behind Renderer instead of exposing wgpu internals."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
gfx = root / "crates/horizon-client-render/src/gfx.rs"
game = root / "crates/horizon-client/src/game.rs"
graphics = root / "crates/horizon-client-render/src/graphics.rs"

gfx_source = gfx.read_text(encoding="utf-8")
game_source = game.read_text(encoding="utf-8")
graphics_source = graphics.read_text(encoding="utf-8")

method = """    /// Wait for outstanding GPU work without exposing the wgpu device to Game.
    pub fn wait_idle(&self) {
        let _ = self.device.poll(wgpu::PollType::wait_indefinitely());
    }

method = """    /// Install the GUI atlas state without exposing the backend device or HUD renderer.
    pub fn configure_hud(&mut self, atlas: &crate::hud::HudAtlas) {
        self.hud.sprite_animations = atlas
            .sprite_animations
            .iter()
            .map(|(rect, animation)| (*rect, animation.clone(), (usize::MAX, 0, 0)))
            .collect();
        self.hud
            .set_text_shader(&self.device, atlas.text_shader.as_deref());
    }

    /// Current framebuffer dimensions, without exposing the wgpu surface configuration.
    pub fn screen_size(&self) -> [u32; 2] {
        [self.config.width, self.config.height]
    }

    /// Apply backend-neutral far-terrain policy without leaking wgpu::AdapterInfo.
    pub fn lod_distance_capped(&self, distance: u32) -> u32 {
        let capabilities = crate::graphics::BackendCapabilities {
            cap_lod_distance: crate::graphics::lod_capped(&self.info),
        };
        crate::graphics::lod_distance_capped_for_backend(distance, capabilities)
    }

    /// Number of world sections drawn by the last completed frame.
    pub fn drawn_section_count(&self) -> usize {
        self.drawn_sections
    }

    /// Whether a screenshot request is waiting for a frame.
    pub fn has_pending_screenshot(&self) -> bool {
        self.screenshot.is_some()
    }

    /// Queue a screenshot without exposing the renderer's request slot.
    pub fn request_screenshot(&mut self, request: crate::gfx::ScreenshotRequest) -> bool {
        if self.screenshot.is_some() {
            false
        } else {
            self.screenshot = Some(request);
            true
        }
    }

    /// Time the previous render spent waiting on GPU or presentation.
    pub fn last_wait_duration(&self) -> std::time::Duration {
        self.last_wait
    }

    /// Return the latest GPU timing and drain the timing samples.
    pub fn take_latest_gpu_time(&mut self) -> Option<f32> {
        let latest = self.gpu_times.last().copied();
        self.gpu_times.clear();
        latest
    }

    /// Drain all GPU timing samples for frame statistics and platform telemetry.
    pub fn take_gpu_times(&mut self) -> Vec<f32> {
        std::mem::take(&mut self.gpu_times)
    }

    /// CPU-side render encoding time from the previous frame.
    pub fn last_encode_duration(&self) -> std::time::Duration {
        self.last_encode
    }

    /// Human-readable graphics adapter name for diagnostics.
    pub fn adapter_name(&self) -> &str {
        &self.info.name
    }

    /// Stable debug label for the active wgpu backend.
    pub fn backend_name(&self) -> String {
        format!("{:?}", self.info.backend)
    }

    /// Whether this renderer is using the OpenGL backend.
    pub fn is_gl_backend(&self) -> bool {
        self.info.backend == wgpu::Backend::Gl
    }

    /// Whether this renderer is using the Vulkan backend.
    pub fn is_vulkan_backend(&self) -> bool {
        self.info.backend == wgpu::Backend::Vulkan
    }

"""
anchor = "impl Renderer {\n    /// Sections with GPU buffers, for soak tests watching residency.\n"
if gfx_source.count(method) == 0:
    if gfx_source.count(anchor) != 1:
        raise SystemExit("Could not uniquely locate Renderer implementation insertion point")
    gfx_source = gfx_source.replace(anchor, anchor + method)
elif gfx_source.count(method) != 1:
    raise SystemExit("Unexpected duplicate Renderer::configure_hud method")

atlas_block = """renderer.hud.sprite_animations = atlas
        .sprite_animations
        .iter()
        .map(|(rect, animation)| (*rect, animation.clone(), (usize::MAX, 0, 0)))
        .collect();"""
atlas_block_deep = """renderer.hud.sprite_animations = atlas
            .sprite_animations
            .iter()
            .map(|(rect, animation)| (*rect, animation.clone(), (usize::MAX, 0, 0)))
            .collect();"""
gui_atlas_block = """renderer.hud.sprite_animations = gui
                .atlas
                .sprite_animations
                .iter()
                .map(|(rect, animation)| (*rect, animation.clone(), (usize::MAX, 0, 0)))
                .collect();"""
screen_size_calls = "[renderer.config.width, renderer.config.height]"
screen_size_closure = "[r.config.width, r.config.height]"
if game_source.count(screen_size_calls) != 4:
    raise SystemExit(f"Expected 4 loading HUD framebuffer-size reads, found {game_source.count(screen_size_calls)}")
if game_source.count(screen_size_closure) != 17:
    raise SystemExit(f"Expected 17 renderer framebuffer-size reads, found {game_source.count(screen_size_closure)}")

atlas_shader = """renderer
        .hud
        .set_text_shader(&renderer.device, atlas.text_shader.as_deref());"""
gui_shader = """renderer
                .hud
                .set_text_shader(&renderer.device, gui.atlas.text_shader.as_deref());"""

if game_source.count(atlas_block) != 1 or game_source.count(atlas_block_deep) != 1:
    raise SystemExit(f"Expected one standard and one nested atlas animation assignment, found {game_source.count(atlas_block)} and {game_source.count(atlas_block_deep)}")
if game_source.count(gui_atlas_block) != 2:
    raise SystemExit(f"Expected 2 GUI atlas animation assignments, found {game_source.count(gui_atlas_block)}")
if game_source.count(atlas_shader) != 1:
    raise SystemExit(f"Expected 1 atlas text shader setup, found {game_source.count(atlas_shader)}")
if game_source.count(gui_shader) != 2:
    raise SystemExit(f"Expected 2 GUI text shader setups, found {game_source.count(gui_shader)}")

game_source = game_source.replace(atlas_block, "renderer.configure_hud(atlas);")
game_source = game_source.replace(atlas_block_deep, "renderer.configure_hud(atlas);")
game_source = game_source.replace(gui_atlas_block, "renderer.configure_hud(&gui.atlas);")
game_source = game_source.replace(atlas_shader, "")
game_source = game_source.replace(screen_size_calls, "renderer.screen_size()")
game_source = game_source.replace(screen_size_closure, "r.screen_size()")

game_source = game_source.replace(gui_shader, "")


if game_source.count("horizon_client::graphics::lod_distance_capped(distance, &renderer.info)") != 1:
    raise SystemExit("Expected one direct adapter-info LOD cap call for the render distance")
if game_source.count("horizon_client::graphics::lod_distance_capped(far_distance, &renderer.info)") != 1:
    raise SystemExit("Expected one direct adapter-info LOD cap call for far terrain")
game_source = game_source.replace("horizon_client::graphics::lod_distance_capped(distance, &renderer.info)", "renderer.lod_distance_capped(distance)")
game_source = game_source.replace("horizon_client::graphics::lod_distance_capped(far_distance, &renderer.info)", "renderer.lod_distance_capped(far_distance)")
if "lod_distance_capped(distance, &renderer.info)" in game_source or "lod_distance_capped(far_distance, &renderer.info)" in game_source:
    raise SystemExit("Game still leaks wgpu::AdapterInfo into LOD policy")

if "renderer.hud.sprite_animations" in game_source or ".set_text_shader(&renderer.device" in game_source:
    raise SystemExit("Game still accesses HUD/backend internals after migration")

if game_source.count("renderer.drawn_sections <= 10") != 1:
    raise SystemExit("Expected one world-icon drawn-section threshold")
if game_source.count("renderer.screenshot.is_some()") != 1:
    raise SystemExit("Expected one pending-screenshot check")
if game_source.count("renderer.screenshot = Some(horizon_client::gfx::ScreenshotRequest {") != 1:
    raise SystemExit("Expected one world-icon screenshot request")
game_source = game_source.replace("renderer.drawn_sections <= 10", "renderer.drawn_section_count() <= 10")
game_source = game_source.replace("renderer.screenshot.is_some()", "renderer.has_pending_screenshot()")
game_source = game_source.replace(
    "renderer.screenshot = Some(horizon_client::gfx::ScreenshotRequest {",
    "renderer.request_screenshot(horizon_client::gfx::ScreenshotRequest {",
)
game_source = game_source.replace(
    "            scaled: false,\n        });",
    "            scaled: false,\n        });",
)

if "renderer.drawn_sections" in game_source or "renderer.screenshot" in game_source:
    raise SystemExit("Game still accesses renderer screenshot or drawn-section storage directly")

if game_source.count("previous.device.poll(wgpu::PollType::wait_indefinitely())") != 1:
    raise SystemExit("Expected one renderer replacement wait")
if game_source.count("r.device.poll(wgpu::PollType::wait_indefinitely())") != 1:
    raise SystemExit("Expected one background renderer wait")
game_source = game_source.replace(
    "previous.device.poll(wgpu::PollType::wait_indefinitely())",
    "previous.wait_idle()",
)
game_source = game_source.replace(
    "r.device.poll(wgpu::PollType::wait_indefinitely())",
    "r.wait_idle()",
)
if "previous.device" in game_source or "r.device" in game_source:
    raise SystemExit("Game still accesses wgpu device ownership directly")


if game_source.count("renderer.last_wait") != 2:
    raise SystemExit(f"Expected 2 renderer wait-duration reads, found {game_source.count('renderer.last_wait')}")
if game_source.count("renderer.gpu_times.last().copied()") != 1:
    raise SystemExit("Expected one latest GPU timing read")
if game_source.count("renderer.gpu_times.clear()") != 1:
    raise SystemExit("Expected one GPU timing drain")
game_source = game_source.replace("renderer.last_wait", "renderer.last_wait_duration()")
game_source = game_source.replace(
    "renderer.gpu_times.last().copied()",
    "renderer.take_latest_gpu_time()",
)
game_source = game_source.replace("                renderer.gpu_times.clear();\n", "")
if "renderer.last_wait)" in game_source or "renderer.last_wait," in game_source or "renderer.gpu_times" in game_source:
    raise SystemExit("Game still accesses renderer timing storage directly")

if game_source.count("now.elapsed().saturating_sub(r.last_wait)") != 2:
    raise SystemExit("Expected two main-loop wait-duration calculations")
if game_source.count("r.last_wait + render") != 1:
    raise SystemExit("Expected one render-phase wait-duration calculation")
if game_source.count("r.last_encode") != 1:
    raise SystemExit("Expected one render-encoding duration read")
if game_source.count("r.gpu_times.last().copied()") != 1:
    raise SystemExit("Expected one main-loop latest GPU timing read")
if game_source.count("r.gpu_times.last()") != 2:
    raise SystemExit("Expected one main-loop GPU utilization timing read")
if game_source.count("&r.gpu_times") != 2:
    raise SystemExit("Expected two main-loop GPU sample consumers")
if game_source.count("                        r.gpu_times.clear();") != 1:
    raise SystemExit("Expected one main-loop GPU sample drain")

main_loop_anchor = """                    if let Some(r) = self.renderer.as_mut() {
                        let cpu = now.elapsed().saturating_sub(r.last_wait);"""
if game_source.count(main_loop_anchor) != 1:
    raise SystemExit("Could not uniquely locate main-loop renderer timing block")
game_source = game_source.replace(
    main_loop_anchor,
    """                    if let Some(r) = self.renderer.as_mut() {
                        let gpu_times = r.take_gpu_times();
                        let cpu = now.elapsed().saturating_sub(r.last_wait_duration());""",
)
game_source = game_source.replace(
    "now.elapsed().saturating_sub(r.last_wait)",
    "now.elapsed().saturating_sub(r.last_wait_duration())",
)
game_source = game_source.replace("r.last_wait + render", "r.last_wait_duration() + render")
game_source = game_source.replace("r.last_encode", "r.last_encode_duration()")
game_source = game_source.replace("r.gpu_times.last().copied()", "gpu_times.last().copied()")
game_source = game_source.replace("r.gpu_times.last()", "gpu_times.last()")
game_source = game_source.replace("&r.gpu_times", "&gpu_times")
game_source = game_source.replace("self.frame_stats.record_gpu(&r.gpu_times);", "self.frame_stats.record_gpu(&gpu_times);")
game_source = game_source.replace("                        r.gpu_times.clear();\n", "")
if "r.last_wait)" in game_source or "r.last_wait +" in game_source or "r.last_encode)" in game_source or "r.last_encode," in game_source or "r.gpu_times" in game_source:
    raise SystemExit("Game still accesses main-loop renderer timing fields directly")

if game_source.count("self.renderer.as_ref().map(|r| &r.info.name)") != 4:
    raise SystemExit("Expected four adapter-name diagnostic reads")
if game_source.count("format!(\"{:?}\",r.info.backend)") != 1:
    raise SystemExit("Expected one backend diagnostic label")
if game_source.count("previous.info.backend == wgpu::Backend::Gl") != 1:
    raise SystemExit("Expected one GL backend selection")
if game_source.count("previous.info.backend != wgpu::Backend::Vulkan") != 1:
    raise SystemExit("Expected one Vulkan backend selection")
if game_source.count("r.info.backend == wgpu::Backend::Gl") != 1:
    raise SystemExit("Expected one deferred GL frame check")

game_source = game_source.replace(
    "self.renderer.as_ref().map(|r| &r.info.name)",
    "self.renderer.as_ref().map(|r| r.adapter_name())",
)
game_source = game_source.replace(
    "format!(\"{:?}\",r.info.backend)",
    "r.backend_name()",
)
game_source = game_source.replace(
    "previous.info.backend == wgpu::Backend::Gl",
    "previous.is_gl_backend()",
)
game_source = game_source.replace(
    "previous.info.backend != wgpu::Backend::Vulkan",
    "!previous.is_vulkan_backend()",
)
game_source = game_source.replace(
    "r.info.backend == wgpu::Backend::Gl",
    "r.is_gl_backend()",
)
if "renderer.info" in game_source or "previous.info" in game_source or "r.info" in game_source:
    raise SystemExit("Game still accesses wgpu adapter metadata directly")

capability_anchor = "pub fn lod_capped(info: &wgpu::AdapterInfo) -> bool {"
capability_code = """/// Backend-neutral capabilities that affect shared render-distance policy.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct BackendCapabilities {
    /// Apply the integrated/low-bandwidth GPU far-terrain limit.
    pub cap_lod_distance: bool,
}
/// A LOD distance capped using backend-neutral capabilities.
pub fn lod_distance_capped_for_backend(distance: u32, capabilities: BackendCapabilities) -> u32 {
    let distance = distance.min(crate::lod::MAX_CHUNKS);
    if capabilities.cap_lod_distance {
        distance.min(INTEGRATED_LOD_DISTANCE)
    } else {
        distance
    }
}
"""
if graphics_source.count(capability_anchor) != 1:
    raise SystemExit("Could not uniquely locate backend capability insertion point")
if "pub struct BackendCapabilities" not in graphics_source:
    graphics_source = graphics_source.replace(capability_anchor, capability_code + capability_anchor)
elif graphics_source.count("pub struct BackendCapabilities") != 1:
    raise SystemExit("Unexpected duplicate BackendCapabilities definition")

test_anchor = """    #[test]
    fn integrated_gpus_other_than_apple_cap_the_lod_distance() {"""
test_code = """    #[test]
    fn backend_neutral_lod_cap_supports_low_bandwidth_gles2_devices() {
        let low_end_gles2 = BackendCapabilities { cap_lod_distance: true };
        let unrestricted = BackendCapabilities { cap_lod_distance: false };
        for (distance, capped) in [(0, 0), (64, 64), (128, 128), (256, 128), (1024, 128)] {
            assert_eq!(lod_distance_capped_for_backend(distance, low_end_gles2), capped);
        }
        assert_eq!(lod_distance_capped_for_backend(256, unrestricted), 256);
    }

"""
if graphics_source.count(test_anchor) != 1:
    raise SystemExit("Could not uniquely locate backend capability test insertion point")
if "fn backend_neutral_lod_cap_supports_low_bandwidth_gles2_devices" not in graphics_source:
    graphics_source = graphics_source.replace(test_anchor, test_code + test_anchor)

gfx.write_text(gfx_source, encoding="utf-8")
game.write_text(game_source, encoding="utf-8")
graphics.write_text(graphics_source, encoding="utf-8")
print("Installed renderer accessors, backend-neutral LOD capabilities, and migrated game-side adapter metadata.")
