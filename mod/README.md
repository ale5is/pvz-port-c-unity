# PavoHorizon native mod bundle

This bundle targets the pinned Horizon 0.7.8 source and native hooks API 80.

## Simple Voice Chat
The bundle contains the API-80 adaptation of Simple Voice Chat 2.6.x client protocol compatibility version 20, including:
- Standard `voicechat:*` custom payload receive/send for Java servers.
- Horizon dedicated-server relay mirroring through `horizon:mod_net` when advertised.
- CPAL Android audio backend and vendored libopus 1.4 sources.
- Runtime Android microphone permission request.
- Remote-client lifecycle, server host resolution and reconnect handling.

## Jade
The current Jade API-80 adaptation is deliberately scoped to the in-world block tooltip:
- Shows the display name of the block currently targeted.
- Optional registry identifier, X/Y/Z coordinates and the block state's properties (for example, facing, half, type or waterlogged).
- Options > Mods > Jade exposes persistent toggles for the tooltip, registry identifier, coordinates and block-state properties.
- All three options default to enabled. Settings are stored using Horizon's native-mod settings directory.
- The implementation uses the pinned game's existing raycast, block facts and HUD atlas. It does not rewrite upstream compatibility metadata or force newer Horizon APIs.

Not yet ported: entity providers, block-entity/plugin data, server extensions, themes, profiles, full Jade configuration screens, keybinds and commands. The current tooltip should not be described as full Jade feature parity.

Upstream reference: https://github.com/Joeypc6/Jade-Horizon, revision `9f63f5c78c5e4938d4544c9567e06feb45649656`. Jade is licensed CC BY-NC-SA 4.0; this adaptation is non-commercial and must retain the same licence. See `LICENSE` and `THIRD_PARTY_NOTICES.md`.

The Android workflow builds a public ARM64 APK. Only a successful workflow run with its artifact checks passing should be treated as a valid build. Runtime behavior still requires installation and testing on a physical Android device.
