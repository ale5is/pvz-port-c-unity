#!/usr/bin/env python3
"""Move common renderer status reads behind a backend-neutral metrics snapshot."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()

api = root / "crates/horizon-render-api/src/lib.rs"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
game = root / "crates/horizon-client/src/game.rs"

for path in (api, gfx, game):
    if not path.is_file():
        raise SystemExit(f"Missing expected Horizon source: {path}")

api_text = api.read_text(encoding="utf-8")
gfx_text = gfx.read_text(encoding="utf-8")
game_text = game.read_text(encoding="utf-8")

metrics_type = """/// Backend-neutral status used by game logic and diagnostics. Durations are
/// represented in nanoseconds so this API does not expose GPU or window types.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct RendererMetrics {
    pub width: u32,
    pub height: u32,
    pub suspended: bool,
    pub drawn_sections: usize,
    pub screenshot_pending: bool,
    pub last_wait_ns: u64,
    pub pending_gpu_time_samples: usize,
}

"""
if "pub struct RendererMetrics" not in api_text:
    marker = "/// Object-safe frame and window lifecycle contract."
    if api_text.count(marker) != 1:
        raise SystemExit("Could not uniquely locate RenderBackend trait marker")
    api_text = api_text.replace(marker, metrics_type + marker, 1)

trait_marker = "    fn backend_info(&self) -> BackendInfo;\n"
if "fn metrics(&self) -> RendererMetrics;" not in api_text:
    if api_text.count(trait_marker) != 1:
        raise SystemExit("Could not uniquely locate RenderBackend backend_info method")
    api_text = api_text.replace(
        trait_marker,
        trait_marker + "    fn metrics(&self) -> RendererMetrics;\n",
        1,
    )

impl_marker = "    fn backend_info(&self) -> horizon_render_api::BackendInfo {"
if "fn metrics(&self) -> horizon_render_api::RendererMetrics" not in gfx_text:
    if gfx_text.count(impl_marker) != 1:
        raise SystemExit("Could not uniquely locate wgpu backend_info implementation")
    # Add a concrete accessor first; Game can use it without importing the trait.
    accessor = """
impl Renderer {
    /// A backend-neutral view of status commonly needed by the game loop.
    pub fn metrics(&self) -> horizon_render_api::RendererMetrics {
        horizon_render_api::RendererMetrics {
            width: self.config.width,
            height: self.config.height,
            suspended: self.suspended,
            drawn_sections: self.drawn_sections,
            screenshot_pending: self.screenshot.is_some(),
            last_wait_ns: self.last_wait.as_nanos().min(u128::from(u64::MAX)) as u64,
            pending_gpu_time_samples: self.gpu_times.len(),
        }
    }

    /// Request a screenshot without exposing the renderer's internal slot.
    pub fn set_screenshot_request(&mut self, request: ScreenshotRequest) {
        self.screenshot = Some(request);
    }
}

"""
    gfx_text = gfx_text.replace(impl_marker, accessor + impl_marker, 1)
    # Add trait implementation method immediately before the impl closes.
    close_marker = """        }
    }
}
"""
    # Locate the end of the one RenderBackend impl, not an arbitrary impl.
    start = gfx_text.find(impl_marker)
    end = gfx_text.find(close_marker, start)
    if start < 0 or end < 0:
        raise SystemExit("Could not locate end of wgpu RenderBackend implementation")
    end += len(close_marker)
    trait_method = """
    fn metrics(&self) -> horizon_render_api::RendererMetrics {
        Renderer::metrics(self)
    }
"""
    gfx_text = gfx_text[:end-len("}\n")] + trait_method + "}\n" + gfx_text[end:]

replacements = [
    ("r.config.width", "r.metrics().width"),
    ("r.config.height", "r.metrics().height"),
    ("renderer.config.width", "renderer.metrics().width"),
    ("renderer.config.height", "renderer.metrics().height"),
    ("renderer.drawn_sections", "renderer.metrics().drawn_sections"),
    ("renderer.screenshot.is_some()", "renderer.metrics().screenshot_pending"),
    ("renderer.last_wait,", "std::time::Duration::from_nanos(renderer.metrics().last_wait_ns),"),
    ("|r| r.suspended", "|r| r.metrics().suspended"),
]
for old, new in replacements:
    game_text = game_text.replace(old, new)

screenshot_old = "renderer.screenshot = Some(horizon_client::gfx::ScreenshotRequest {"
screenshot_new = "renderer.set_screenshot_request(horizon_client::gfx::ScreenshotRequest {"
game_text = game_text.replace(screenshot_old, screenshot_new)

# Require all formerly direct status reads to be gone. Keep the GPU info and
# GPU timing vectors for a later, separately audited extraction.
for marker in (
    "r.config.width",
    "r.config.height",
    "renderer.config.width",
    "renderer.config.height",
    "renderer.drawn_sections",
    "renderer.screenshot.is_some()",
    "renderer.screenshot = Some(",
    "renderer.last_wait,",
    "|r| r.suspended",
):
    if marker in game_text:
        raise SystemExit(f"Renderer metrics migration incomplete: {marker}")

if "pub struct RendererMetrics" not in api_text:
    raise SystemExit("RendererMetrics type was not installed")
if "pub fn metrics(&self)" not in gfx_text:
    raise SystemExit("Concrete wgpu Renderer metrics accessor was not installed")
if "fn metrics(&self) -> horizon_render_api::RendererMetrics" not in gfx_text:
    raise SystemExit("wgpu RenderBackend metrics adapter was not installed")

api.write_text(api_text, encoding="utf-8")
gfx.write_text(gfx_text, encoding="utf-8")
game.write_text(game_text, encoding="utf-8")
print("Moved renderer dimensions, lifecycle state, section count, screenshot state and wait timing behind RendererMetrics.")
