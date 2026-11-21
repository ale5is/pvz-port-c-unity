# PavoHorizon map expansion — Atlas port plan

## Chosen upstream base

Use https://github.com/MrCloudy2/horizon-atlas as the feature and implementation reference. Its MIT licence permits adaptation with the copyright and licence notice retained.

Atlas already provides:
- A minimap and explored-world map, persisted per world, server and dimension.
- Waypoint sets, named and temporary waypoints, deathpoints, labels, distance and 3D markers.
- Mob, player, dropped-item and other-entity radar; optional mob faces, player faces and item icons.
- Auto, cave and height-layer map modes.
- Coordinates, biome, time, facing, dimension, light and weather information.
- Day/night map lighting; block light from torches and lava remains visible at night.
- Optional map sharing with an Atlas-enabled Horizon server.

## Compatibility boundary

Do not copy Atlas 0.3.0's .hnmod directly into this Android build.

- PavoHorizon is pinned to Starling/Horizon 68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29, native hooks API 80.
- Atlas 0.3.0 is pinned to Horizon 9f42064c11efe7ef3e5ed2baad7ddc2a3b36eea0 and hooks API 415.
- Atlas also reads internal world, chunk, entity, biome, map-texture and server state; those assumptions must be checked against the pinned 0.7.8 source.
- Pavo's Android launcher currently does not load arbitrary native .hnmod files. The map must be ported into the same statically compiled mixin bundle path used by Pavo's Jade and Simple Voice Chat integrations.

## API-80 compatibility audit

A direct comparison of Atlas 0.3.0's 23 injection targets with the pinned Horizon 0.7.8 native hook registry found 17 exact target matches and 6 missing targets:

- `native_screen_mouse_dragged_pre`: no API-80 equivalent. The existing screen-render hook exposes the pointer position, so dragging may be reconstructed from successive frames and button state; this needs an input-state test.
- `native_screen_mouse_scrolled_pre`: no screen-wheel hook. `native_wheel` is documented for gameplay and must not be assumed to fire over Atlas's map screen. Use keyboard zoom initially; add screen-wheel support only with a verified call site.
- `native_screen_key_pressed_pre`: use the existing `native_key` hook, which receives keyboard events on any screen. Adapt key handling to its `(key, pressed)` signature and avoid consuming keys outside the Atlas screen.
- `native_screen_char_typed_pre`: no character-text hook. Do not enable editable text fields yet; use a waypoint command or coordinate/name presets until a safe text-input path is added.
- `native_camera_pose_now`: no direct API-80 equivalent. Audit the render camera/player state before implementing world-space waypoint labels; omit camera-dependent labels in the first port if needed.
- `native_level_unload`: use the existing `native_world_lifecycle` hook (API 44) and handle the final-close/failure phases explicitly; do not reuse the newer no-argument helper unchanged.

The 17 remaining Atlas injection targets exist in the API-80 registry. This count only confirms hook-name availability; it does not prove matching signatures or compatible game data structures. The bridge still needs a field/type audit and an Android compile.

## Port order

1. Keep the current built-in Horizon Minimap available as a fallback.
2. Port Atlas's map data model, per-world/per-server persistence and waypoint manager.
3. Port the entity scan and radar (mobs, players, dropped items), with configurable categories and vertical fade.
4. Port world-map screen interactions, dimension switching, cave/layer modes and waypoint markers.
5. Port day/night shading and block-light contribution.
6. Add mobile-safe defaults: conservative map cache, capped update frequency, reduced entity scan rate, and settings to disable faces/icons and world-map background updates.
7. Add a Pavo built-in toggle and test the map on Android ARM64 before enabling it by default.

## Acceptance checks

- Waypoints survive closing and reopening a world and are isolated between worlds/servers and dimensions.
- Players and mobs appear with separate symbols and can be toggled independently.
- The map visibly darkens at night and lit blocks remain visible.
- The minimap and world map agree on player position, rotation, dimension and waypoint state.
- No duplicate minimap is drawn when Atlas is enabled.
- No unbounded queues or per-frame full-world scans; measure memory, CPU time and FPS on the target Android device.
- Existing Jade and Simple Voice Chat toggles and settings continue to work.

## Jade relationship

Jade remains a separate look-at information module. Atlas supplies map navigation and entity radar; it does not need to be registered as a Jade provider. Keep the two independent in the in-game Mods list.
