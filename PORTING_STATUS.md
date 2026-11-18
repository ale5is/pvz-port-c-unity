# Port status — PavoHorizon Android

## Simple Voice Chat
- API-80 adapters for key, command, frame and native mod networking hooks.
- Receives standard `voicechat:*` custom payloads from Java servers and sends raw serverbound payloads through the pinned Horizon 0.7.8 connection.
- Mirrors control messages through `horizon:mod_net` when the server advertises Horizon mod networking.
- Remote-client lifecycle detection, server-host resolution, queued reconnect handshakes and player-position updates are implemented.
- Bundled client protocol/session/audio Rust sources and vendored libopus 1.4 sources.
- Android microphone permission is declared and requested at runtime.
- Physical-device microphone, playback, UDP and interoperability tests remain outstanding.

## Jade API-80 adaptation
- Jade is registered as a native mod and has a block look-at tooltip using Horizon 0.7.8's existing raycast, block facts and HUD atlas.
- The tooltip can be enabled/disabled from Options > Mods > Jade.
- Registry identifier, block coordinates, block-state properties, and block hardness/tool requirement have independent persistent toggles in Jade's settings.
- Block-state properties are read from the pinned Horizon block registry (`game.pack.assets.registry.state`), not from `StateFacts`; hardness and tool requirements are read from the matching `StateFacts` entry.
- The settings are stored through Horizon's native-mod settings directory and default to enabled.
- This remains a limited block-provider adaptation. Entity providers, block-entity/plugin data, server extensions, themes, profiles, full configuration screens, keybinds and commands are not implemented yet.
- Upstream reference: https://github.com/Joeypc6/Jade-Horizon, revision `9f63f5c78c5e4938d4544c9567e06feb45649656`.
- Jade is licensed CC BY-NC-SA 4.0; this adaptation is non-commercial and must retain that licence.

## Build
- Pinned Horizon source: `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29` (0.7.8, native hooks API 80).
- Android target: `arm64-v8a`.
- Application label: `PavoHorizon`; application ID: `com.ale5is.pavohorizon`.
- APK output name: `PavoHorizon-1.0.0.apk`.
- Build artifacts are valid only when the corresponding GitHub Actions run and artifact upload both succeed.
