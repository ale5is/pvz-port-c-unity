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
- Jade is registered as a native mod and has a configurable look-at HUD for blocks, mobs, dropped items, armor stands, item frames, paintings, boats, minecarts, falling blocks, and block/item display entities.
- Target selection compares ray distances and keeps block occlusion; each newly added target family has an independent persistent toggle.
- Block information includes translated names, registry IDs, mod namespace, coordinates, relative coordinates, block face, distance, state properties, hardness, harvest tool, crop progress, redstone, note blocks, TNT, beehive honey, block-state amounts and optional access-state labels (open/closed, waterlogged, lit, locked, powered, active, cracked, etc.).
- Optional block-entity data providers read Horizon's loaded client data for chests, furnaces, brewing stands, jukeboxes, lecterns, spawners, trial spawners, vaults, beehives, campfires, crafters, beacons, sculk, command blocks, shelves, signs, brushable blocks, end gateways and mod block entities.
- Entity information includes health, armor, equipment, effects, villager profession, tame state, horse stats, optional variants for axolotls/rabbits/parrots/foxes and registry-backed wolves/cats/frogs/pigs/cows/mooshrooms/chickens/zombie nautiluses, and access states for charged creepers, powered withers, curing zombie villagers, hornless goats, bees, slimes/magma cubes and copper golems, plus configurable baby-growth / breeding information. Growth and breeding details are disabled by default.
- The next-drop provider now reads Horizon's ticking mob timers: chicken next-egg cooldown, armadillo next-scute cooldown and sniffer dig cooldown (the sniffer brain memory TTL). Allay duplication cooldown is shown under breeding details. No client-side timer is fabricated.
- Item and block mod namespaces use independent persistent settings. Item names are taken from Horizon's translated stack tooltip; leather armour icons use the native item renderer and preserve dye components.
- Four selectable overlay themes (dark, light, purple and high contrast) are persisted. Extra entity reach is configurable from 0 to 8 blocks and applies to mob, item, armor-stand, boat, minecart, falling-block and display-entity ray tests. Theme colours use Horizon's RGBA packing, and the incorrect-tool X and health heart explicitly use pure red (`rgba(255, 0, 0, 255)`).
- Background opacity is configurable from 20% to 100% in five steps and by `/jade opacity <20..100>`; it is applied to the overlay panel background. Nine overlay anchors (top/middle/bottom × left/center/right) can be selected in settings or with `/jade position <anchor>`. Block access-state labels, entity access states, fluid display, next-drop cooldowns and the supported entity-variant provider have independent toggles and are off by default. Jade has configurable overlay and details keybinds, plus `/jade show|hide`, `/jade id on|off`, `/jade coords on|off`, `/jade states on|off`, `/jade theme <name>`, `/jade reach <0..8>`, `/jade set <setting> on|off`, `/jade profile save|load <name>`, `/jade ignore|unignore block|entity|item <id>` and `/jade clear-ignores`.
- Settings are stored through Horizon's native-mod settings directory and are verified after writes. UI labels include eight languages (English, Spanish, Portuguese, French, Italian, German, Japanese and Chinese).
- Still not full parity with Jade-Horizon: no remote server provider protocol / cross-mod provider registration API, no full-screen draggable config editor or profile browser (profiles are command-managed), no theme sprite/nine-slice assets or opacity/scale sliders, and no boss-bar overlap integration, TTS, perspective-camera targeting, recipes/uses or full accessibility provider screens. Ignore lists and profiles are command-managed rather than edited in a dedicated screen. Some block-entity details are generic snapshots rather than every upstream Jade provider.
- Upstream reference: https://github.com/Joeypc6/Jade-Horizon, revision 9f63f5c78c5e4938d4544c9567e06feb45649656.
- Jade is licensed CC BY-NC-SA 4.0; this adaptation is non-commercial and must retain that licence.

## Build
- Pinned Horizon source: `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29` (0.7.8, native hooks API 80).
- Android target: `arm64-v8a`.
- Application label: `PavoHorizon`; application ID: `com.ale5is.pavohorizon`.
- APK output name: `PavoHorizon-1.0.0.apk`.
- Build artifacts are valid only when the corresponding GitHub Actions run and artifact upload both succeed.
