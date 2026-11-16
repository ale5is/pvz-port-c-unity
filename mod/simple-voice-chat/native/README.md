# Simple Voice Chat — Horizon Nightly native adapter

This is the native Horizon API adapter for the existing Pavo Simple Voice Chat
client core. It keeps the original Android launcher separate and does not alter
the stable branch.

## Connected components

- Current native package layout: `horizon-mod.toml`, `Cargo.toml`, `src/lib.rs`.
- Exact Horizon Nightly and `horizon-mixins` revision pins.
- Original `voicechat:` raw plugin channels, with no extra packet envelope.
- Server custom-payload receive path through `Hooks::client_packet`.
- Client custom-payload sends through `ClientHost::send_payload_to_server`.
- World/session lifecycle through `ClientHost::client_session`.
- UDP session, Opus, capture and playback through the existing `svc_client` crate.
- PTT, microphone mute, player mute, all-mute and HUD Controls bindings.
- `/svc` and `/vcgroup` local commands.
- Local/remote player position refresh for positional audio.
- Mod-scoped configuration storage.

## Android permission and packaging

The adapter requests no Android permission itself. The Android launcher must
declare and request `RECORD_AUDIO` before microphone capture; the audio backend
will report a device-open failure when permission or a device is unavailable.
The launcher, installer, touch controls and Minecraft asset download flow remain
a separate integration stage.

## Validation

The native API boundary passed `cargo check` and its two queue/catalogue tests
against the pinned Horizon Nightly revision. The full SVC runtime connection,
Android ARM64 build, server interoperability and on-device audio still require
their own CI and runtime validation before release.
