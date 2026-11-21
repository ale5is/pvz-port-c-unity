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
The API-80 adaptation provides a configurable in-world look-at overlay for blocks, mobs, dropped items, armor stands, item frames, paintings, boats, minecarts, falling blocks and block/item display entities. It also includes:
- Block names, registry IDs, mod namespaces, coordinates, block-state properties, hardness, harvest tools and supported crop/redstone/note-block/TNT/beehive details.
- Optional block-entity details for supported vanilla containers and special blocks.
- Entity health, armor, equipment, effects, villager profession, tame state, horse stats, supported variants and entity access states.
- Chicken egg, armadillo scute, sniffer digging and allay duplication cooldowns.
- Optional water/lava targeting, block access states, entity reach, ignore lists, profiles, overlay anchors, opacity and four themes.
- Persistent settings, keybinds and /jade commands.
- Native mods compiled into Horizon can register local block/entity detail callbacks with crate::game::jade::register_client_block_provider, crate::game::jade::register_client_entity_provider and crate::game::jade::register_client_item_provider.

The provider callbacks are a compile-time client extension point only. They do not yet implement Jade's server request/data protocol, server-side provider registration, or dynamic third-party mod loading. Other parity gaps include the full draggable configuration editor/profile browser, original theme sprites/nine-slice rendering, boss-bar overlap, TTS, perspective-camera targeting, recipe/uses screens and complete accessibility providers. Some block-entity and entity details remain partial. This is not full Jade-Horizon feature parity.

Upstream reference: https://github.com/Joeypc6/Jade-Horizon, revision `9f63f5c78c5e4938d4544c9567e06feb45649656`. Jade is licensed CC BY-NC-SA 4.0; this adaptation is non-commercial and must retain the same licence. See `LICENSE` and `THIRD_PARTY_NOTICES.md`.

The Android workflow builds a public ARM64 APK. Only a successful workflow run with its artifact checks passing should be treated as a valid build. Runtime behavior still requires installation and testing on a physical Android device.
