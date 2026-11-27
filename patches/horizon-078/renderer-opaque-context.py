#!/usr/bin/env python3
"""Erase backend-specific replacement state before it crosses the Game worker boundary."""
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
        raise SystemExit(f"Missing renderer replacement source: {path}")

api_text = api.read_text(encoding="utf-8")
if "pub struct BackendContext" not in api_text:
    api_text = api_text.replace("use std::fmt;\n", "use std::{any::Any, fmt};\n", 1)
    anchor = "impl std::error::Error for RenderBackendError {}\n"
    context = '''
/// Opaque, transferable state used to prepare a replacement renderer.
///
/// The payload is owned by the backend that created it. Consumers must check
/// the backend kind and concrete payload type before using it. No GPU handle
/// or platform type is exposed by this API.
pub struct BackendContext {
    kind: BackendKind,
    payload: Box<dyn Any + Send>,
}

impl BackendContext {
    pub fn new<T: Any + Send>(kind: BackendKind, payload: T) -> Self {
        Self { kind, payload: Box::new(payload) }
    }

    pub const fn kind(&self) -> BackendKind {
        self.kind
    }

    pub fn into_inner<T: Any + Send>(
        self,
        expected: BackendKind,
    ) -> Result<T, RenderBackendError> {
        if self.kind != expected {
            return Err(RenderBackendError::new("renderer replacement backend mismatch"));
        }
        self.payload
            .downcast::<T>()
            .map(|payload| *payload)
            .map_err(|_| RenderBackendError::new("renderer replacement payload mismatch"))
    }
}
'''
    if api_text.count(anchor) != 1:
        raise SystemExit("Could not locate RenderBackendError insertion point")
    api_text = api_text.replace(anchor, anchor + context, 1)

trait_anchor = "    fn backend_info(&self) -> BackendInfo;\n"
if api_text.count(trait_anchor) != 1:
    raise SystemExit("Expected one RenderBackend backend_info method")
if "fn replacement_context(&self)" not in api_text:
    api_text = api_text.replace(
        trait_anchor,
        "    fn replacement_context(&self) -> Result<BackendContext, RenderBackendError>;\n" + trait_anchor,
        1,
    )

tests_anchor = "    #[test]\n    fn backend_errors_keep_a_stable_display_message() {"
if "opaque_replacement_context_rejects_wrong_backend" not in api_text:
    tests = '''    #[test]
    fn opaque_replacement_context_round_trips_owned_payload() {
        use super::{BackendContext, BackendKind};

        let context = BackendContext::new(BackendKind::Wgpu, vec![1u8, 2, 3]);
        assert_eq!(context.kind(), BackendKind::Wgpu);
        assert_eq!(context.into_inner::<Vec<u8>>(BackendKind::Wgpu).unwrap(), vec![1, 2, 3]);
    }

    #[test]
    fn opaque_replacement_context_rejects_wrong_backend() {
        use super::{BackendContext, BackendKind};

        let context = BackendContext::new(BackendKind::Wgpu, 7u32);
        assert!(context.into_inner::<u32>(BackendKind::NativeGles2).is_err());
    }

    #[test]
    fn opaque_replacement_context_rejects_wrong_payload_type() {
        use super::{BackendContext, BackendKind};

        let context = BackendContext::new(BackendKind::Wgpu, 7u32);
        assert!(context.into_inner::<String>(BackendKind::Wgpu).is_err());
    }

'''
    if api_text.count(tests_anchor) != 1:
        raise SystemExit("Could not locate backend API test insertion point")
    api_text = api_text.replace(tests_anchor, tests + tests_anchor, 1)

gfx_text = gfx.read_text(encoding="utf-8")
old_signature = "        mut previous: Option<ReplacementContext>,"
new_signature = "        previous: Option<horizon_render_api::BackendContext>,"
if gfx_text.count(old_signature) == 1:
    gfx_text = gfx_text.replace(old_signature, new_signature, 1)
elif gfx_text.count(new_signature) != 1:
    raise SystemExit("Could not uniquely locate wgpu replacement-context parameter")

match_anchor = '''    ) -> Result<Self> {
        #[cfg(target_os = "android")]
        let startup'''
downcast = '''    ) -> Result<Self> {
        let mut previous = previous
            .map(|context| context.into_inner::<ReplacementContext>(horizon_render_api::BackendKind::Wgpu))
            .transpose()?;
        #[cfg(target_os = "android")]
        let startup'''
if gfx_text.count(match_anchor) == 1:
    gfx_text = gfx_text.replace(match_anchor, downcast, 1)
elif "context.into_inner::<ReplacementContext>" not in gfx_text:
    raise SystemExit("Could not insert the checked wgpu context downcast")

old_forward = "            previous.map(Self::replacement_context),"
new_forward = """            previous.map(Self::replacement_context).map(|context| {
                horizon_render_api::BackendContext::new(
                    horizon_render_api::BackendKind::Wgpu,
                    context,
                )
            }),"""
if gfx_text.count(old_forward) == 1:
    gfx_text = gfx_text.replace(old_forward, new_forward, 1)
elif gfx_text.count("BackendContext::new(") < 1:
    raise SystemExit("Could not wrap the wgpu replacement context")

impl_anchor = "    fn backend_info(&self) -> horizon_render_api::BackendInfo {"
replacement_impl = '''    fn replacement_context(
        &self,
    ) -> std::result::Result<horizon_render_api::BackendContext, horizon_render_api::RenderBackendError> {
        Ok(horizon_render_api::BackendContext::new(
            horizon_render_api::BackendKind::Wgpu,
            self.background_context(),
        ))
    }

'''
if gfx_text.count(impl_anchor) != 1:
    raise SystemExit("Could not locate wgpu backend_info implementation")
if "fn replacement_context(" not in gfx_text:
    gfx_text = gfx_text.replace(impl_anchor, replacement_impl + impl_anchor, 1)

game_text = game.read_text(encoding="utf-8")
old_game = "        let context = previous.background_context();"
new_game = "        let context = horizon_render_api::RenderBackend::<horizon_client::gfx::Frame<'_>>::replacement_context(previous)?;"
if game_text.count(old_game) == 1:
    game_text = game_text.replace(old_game, new_game, 1)
elif game_text.count(new_game) != 1:
    raise SystemExit("Could not uniquely migrate Game's background-context handoff")

if "previous.background_context()" in game_text:
    raise SystemExit("Game still obtains a concrete wgpu replacement context")
if "mut previous: Option<ReplacementContext>" in gfx_text:
    raise SystemExit("Renderer constructor still accepts a concrete replacement context")
if "context.into_inner::<ReplacementContext>" not in gfx_text:
    raise SystemExit("Renderer constructor does not validate opaque context ownership")

api.write_text(api_text, encoding="utf-8")
gfx.write_text(gfx_text, encoding="utf-8")
game.write_text(game_text, encoding="utf-8")
print("Migrated replacement handoff to an owned, backend-tagged opaque context with checked wgpu downcast.")
