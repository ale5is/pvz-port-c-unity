# Simple Voice Chat — Horizon 0.7.8 port

This API-80 adaptation speaks the Simple Voice Chat 2.6.x client protocol (compatibility version 20).

- Direct Java-server path: sends and receives raw `voicechat:*` custom payloads.
- Horizon dedicated-server path: also mirrors control messages through `horizon:mod_net` when the server announces that channel, for the Horizon `voicechat-server` relay.
- Audio: CPAL 0.16 Android backend plus vendored libopus 1.4.
- Microphone permission: the Android build adapter adds `RECORD_AUDIO` to the manifest and requests it at runtime.
- Opus C/H files are stored in `data/vendor/`; Horizon stages these as `native-data/simple-voice-chat/vendor/` for the crate build script.

Horizon 0.7.8 hook API: 80. The remote client connection is detected from `Remote.logged_in` in the frame hook; the 0.7.8 `world_lifecycle` hook is server-side and does not fire for remote clients.

The Android release workflow builds and audits the APK; only a successful workflow run with its artifact checks passing should be treated as a valid build. Runtime behavior still requires installation and testing on a physical Android device.

## Jade API-80 adaptation

The Android bundle includes an initial native Jade look-at overlay adapted to the pinned Horizon 0.7.8 / hooks API 80. It uses Horizon's own block raycast, block-state facts and HUD atlas; it does not rewrite Jade's upstream compatibility metadata or introduce newer Horizon APIs. The current adapted scope is the block tooltip (display name, registry identifier and coordinates). Jade's upstream entity providers, plugin registry, themes, configuration screens and server extensions are not yet ported and are not represented as implemented.

Upstream reference: https://github.com/Joeypc6/Jade-Horizon, revision `9f63f5c78c5e4938d4544c9567e06feb45649656`. Jade is licensed CC BY-NC-SA 4.0; see `LICENSE` and `THIRD_PARTY_NOTICES.md`. This adaptation is non-commercial and must retain the same licence.
