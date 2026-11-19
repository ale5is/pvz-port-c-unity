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
The current Jade API-80 adaptation provides a partial in-world look-at overlay:
- Block display name, optional registry ID, block mod namespace, hardness and tool requirement.
- Optional block coordinates and block-state properties; both options default to disabled in a fresh configuration.
- Mob name, optional health and mod namespace.
- Dropped-item name, optional registry ID, mod namespace and stack count.
- Armor-stand targeting with optional equipment listing.
- Independent persistent toggles for supported providers and details in Options > Mods > Jade.
- Entity overlays respect the first block hit and compare supported targets by ray distance.

Not yet ported: item frames, paintings, boats, minecarts, falling blocks, block-entity/plugin data, server extensions, themes, profiles, full Jade configuration screens, keybinds and commands. This is not full Jade-Horizon feature parity.

Upstream reference: https://github.com/Joeypc6/Jade-Horizon, revision `9f63f5c78c5e4938d4544c9567e06feb45649656`. Jade is licensed CC BY-NC-SA 4.0; this adaptation is non-commercial and must retain the same licence. See `LICENSE` and `THIRD_PARTY_NOTICES.md`.

The Android workflow builds a public ARM64 APK. Only a successful workflow run with its artifact checks passing should be treated as a valid build. Runtime behavior still requires installation and testing on a physical Android device.
