#!/usr/bin/env python3
"""Install a GPU-independent backend lifecycle/frame API and its wgpu adapter."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
workspace = root / "Cargo.toml"
render = root / "crates/horizon-client-render"
manifest = render / "Cargo.toml"
gfx = render / "src/gfx.rs"
api = root / "crates/horizon-render-api"
api_manifest = api / "Cargo.toml"
api_source = api / "src/lib.rs"

api_text = """//! Backend-neutral lifecycle and frame submission contract.
#![forbid(unsafe_code)]

use std::fmt;

/// The implementation selected by the platform renderer factory.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum BackendKind {
    Wgpu,
    NativeGles2,
}

/// Stable, backend-neutral information safe to expose to game/UI code.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct BackendInfo {
    pub kind: BackendKind,
    pub name: String,
    pub suspended: bool,
}

/// Error returned across the backend boundary; implementation-specific errors
/// are converted at the adapter edge and never expose GPU handles.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct RenderBackendError {
    message: String,
}

impl RenderBackendError {
    pub fn new(message: impl Into<String>) -> Self {
        Self { message: message.into() }
    }

    pub fn message(&self) -> &str {
        &self.message
    }
}

impl fmt::Display for RenderBackendError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.message)
    }
}

impl std::error::Error for RenderBackendError {}

/// Monotonic identity for one native-window lifetime. A detach/reattach must
/// advance this value so delayed CPU work cannot target a replacement surface.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub struct WindowGeneration(u64);

impl WindowGeneration {
    pub const INITIAL: Self = Self(1);

    pub const fn new(value: u64) -> Option<Self> {
        if value == 0 { None } else { Some(Self(value)) }
    }

    pub const fn get(self) -> u64 {
        self.0
    }

    pub const fn is_current(self, active: Self) -> bool {
        self.0 == active.0
    }
}

/// Tracks the active native-window lifetime without allowing generation rollback.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct WindowGenerationClock {
    active: WindowGeneration,
}

impl Default for WindowGenerationClock {
    fn default() -> Self {
        Self { active: WindowGeneration::INITIAL }
    }
}

impl WindowGenerationClock {
    pub const fn active(&self) -> WindowGeneration {
        self.active
    }

    /// Advance only to a strictly newer non-zero generation.
    pub fn advance(&mut self, next: WindowGeneration) -> bool {
        if next <= self.active {
            return false;
        }
        self.active = next;
        true
    }

    /// Accept work only when it was prepared for the currently attached window.
    pub const fn accepts(&self, generation: WindowGeneration) -> bool {
        generation.is_current(self.active)
    }
}

/// Object-safe frame and window lifecycle contract.
///
/// The frame type is a type parameter rather than a dependency: this crate can
/// be compiled and audited without importing Horizon's renderer or wgpu.
pub trait RenderBackend<Frame> {
    fn render_frame(&mut self, frame: &Frame) -> Result<bool, RenderBackendError>;
    fn resize(&mut self, width: u32, height: u32);
    fn suspend_surface(&mut self);
    fn recreate_surface(&mut self) -> Result<(), RenderBackendError>;
    fn release_surface(&mut self);
    fn backend_info(&self) -> BackendInfo;
}

#[cfg(test)]
mod tests {
    use super::{BackendInfo, BackendKind, RenderBackendError};

    #[test]
    fn window_generation_rejects_stale_and_future_work() {
        use super::{WindowGeneration, WindowGenerationClock};

        let mut clock = WindowGenerationClock::default();
        let first = clock.active();
        assert_eq!(first.get(), 1);
        assert!(clock.accepts(first));

        let second = WindowGeneration::new(2).unwrap();
        assert!(clock.advance(second));
        assert!(!clock.accepts(first));
        assert!(clock.accepts(second));
        assert!(!clock.accepts(WindowGeneration::new(3).unwrap()));
    }

    #[test]
    fn window_generation_cannot_roll_back_or_be_zero() {
        use super::{WindowGeneration, WindowGenerationClock};

        assert!(WindowGeneration::new(0).is_none());
        let mut clock = WindowGenerationClock::default();
        assert!(!clock.advance(clock.active()));
        assert!(!clock.advance(WindowGeneration::INITIAL));
        assert_eq!(clock.active(), WindowGeneration::INITIAL);
    }

    #[test]
    fn backend_identity_is_explicit_and_gpu_handle_free() {
        let info = BackendInfo {
            kind: BackendKind::NativeGles2,
            name: "OpenGL ES 2.0".to_owned(),
            suspended: false,
        };
        assert_eq!(info.kind, BackendKind::NativeGles2);
        assert_eq!(info.name, "OpenGL ES 2.0");
    }

    #[test]
    fn backend_errors_keep_a_stable_display_message() {
        let error = RenderBackendError::new("surface unavailable");
        assert_eq!(error.message(), "surface unavailable");
        assert_eq!(error.to_string(), "surface unavailable");
    }
}
"""

if api_source.exists():
    existing = api_source.read_text(encoding="utf-8")
    if "pub trait RenderBackend<Frame>" not in existing:
        raise SystemExit("Existing horizon-render-api crate has an unexpected API; refusing overwrite")
else:
    api.mkdir(parents=True, exist_ok=True)
    (api / "src").mkdir(parents=True, exist_ok=True)
    api_manifest.write_text(
        """[package]
name = "horizon-render-api"
version = "0.1.0"
edition.workspace = true
license.workspace = true
rust-version.workspace = true
description = "GPU-independent rendering backend contract for Horizon"

[dependencies]

[lints]
workspace = true
""",
        encoding="utf-8",
    )
    api_source.write_text(api_text, encoding="utf-8")

workspace_text = workspace.read_text(encoding="utf-8")
workspace_anchor = '    "crates/horizon-client-render",\n'
if '"crates/horizon-render-api"' not in workspace_text:
    if workspace_text.count(workspace_anchor) != 1:
        raise SystemExit("Could not uniquely add horizon-render-api to workspace members")
    workspace_text = workspace_text.replace(
        workspace_anchor,
        '    "crates/horizon-render-api",\n' + workspace_anchor,
    )

manifest_text = manifest.read_text(encoding="utf-8")
dependency = 'horizon-render-api = { path = "../horizon-render-api" }\n'
if dependency not in manifest_text:
    anchor = "[dependencies]\n"
    if manifest_text.count(anchor) != 1:
        raise SystemExit("Could not uniquely add horizon-render-api dependency")
    manifest_text = manifest_text.replace(anchor, anchor + dependency)

gfx_text = gfx.read_text(encoding="utf-8")
impl_marker = "impl<'frame> horizon_render_api::RenderBackend<Frame<'frame>> for Renderer {"
if impl_marker not in gfx_text:
    implementation = """
impl<'frame> horizon_render_api::RenderBackend<Frame<'frame>> for Renderer {
    fn render_frame(
        &mut self,
        frame: &Frame<'frame>,
    ) -> std::result::Result<bool, horizon_render_api::RenderBackendError> {
        self.render(frame)
            .map_err(|error| horizon_render_api::RenderBackendError::new(error.to_string()))
    }

    fn resize(&mut self, width: u32, height: u32) {
        Renderer::resize(self, width, height);
    }

    fn suspend_surface(&mut self) {
        #[cfg(target_os = "android")]
        Renderer::suspend_surface(self);
    }

    fn recreate_surface(
        &mut self,
    ) -> std::result::Result<(), horizon_render_api::RenderBackendError> {
        Renderer::recreate_surface(self)
            .map_err(|error| horizon_render_api::RenderBackendError::new(error.to_string()))
    }

    fn release_surface(&mut self) {
        Renderer::release_surface(self);
    }

    fn backend_info(&self) -> horizon_render_api::BackendInfo {
        horizon_render_api::BackendInfo {
            kind: horizon_render_api::BackendKind::Wgpu,
            name: self.info.name.clone(),
            suspended: self.suspended,
        }
    }
}
"""
    gfx_text = gfx_text.rstrip() + "\n\n" + implementation
elif gfx_text.count(impl_marker) != 1:
    raise SystemExit("Duplicate wgpu RenderBackend implementation detected")

if "wgpu::" in api_text or "winit::" in api_text or "EGLDisplay" in api_text or "EGLContext" in api_text or "GLuint" in api_text:
    raise SystemExit("Backend API unexpectedly depends on a GPU or window implementation")

workspace.write_text(workspace_text, encoding="utf-8")
manifest.write_text(manifest_text, encoding="utf-8")
gfx.write_text(gfx_text, encoding="utf-8")
print("Installed horizon-render-api and implemented its frame/lifecycle contract for wgpu Renderer.")
