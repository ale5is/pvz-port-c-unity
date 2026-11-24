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

- `native_screen_mouse_dragged_pre`: no API-80 equivalent. The Pavo adapter reconstructs drag deltas from successive frame cursor positions while Atlas has a captured mouse button; this still needs an Android touch/pointer test.
- `native_screen_mouse_scrolled_pre`: no published API-80 screen-wheel hook. The map uses its registered `[` / `]` zoom keybinds instead; do not assume the gameplay `native_wheel` hook fires over a screen.
- `native_screen_key_pressed_pre`: no published API-80 screen-key hook. Pavo routes the existing `native_key` callback while its Atlas modal screen is open and does not handle map controls outside Atlas.
- `native_screen_char_typed_pre`: no published API-80 character hook. Pavo converts physical key names received by `native_key` into basic characters for the waypoint editor; Android IME committed text is not covered yet.
- `native_camera_pose_now`: no direct API-80 equivalent. Audit the render camera/player state before implementing world-space waypoint labels; omit camera-dependent labels in the first port if needed.
- `native_level_unload`: use the existing `native_world_lifecycle` hook (API 44) and handle the final-close/failure phases explicitly; do not reuse the newer no-argument helper unchanged.

The 17 remaining Atlas injection targets exist in the API-80 registry. This count only confirms hook-name availability; it does not prove matching signatures or compatible game data structures. The bridge still needs a field/type audit and an Android compile.

## Pavo API-80 implementation status

The active Pavo mixin now wires the portable Atlas core into the built-in Android bundle:

- The existing Horizon minimap/radar renderer remains in place; Atlas continues to supply its terrain texture, radar dots and waypoint markers.
- `M` opens the full-screen world map using the published `screen_render_post` hook. It uses a modal paused-screen container with the pause menu hidden, rather than depending on the unavailable API-415 mod-screen hooks.
- The published API-80 mouse press/release hooks handle map buttons and waypoint markers. Dragging is reconstructed from cursor movement each frame while a captured button is held.
- Drag pans the map; `[` / `]` zoom; `Space` recentres; `B` creates a waypoint at the player; right-click creates one at the map pointer; `U` opens the waypoint list; `D` toggles drawing and `Z` undoes a stroke.
- The root mixin manifest is audited against the pinned API-80 hook registry by `scripts/check_atlas_api80.py`. Unsupported screen-wheel, screen-key and character hooks are not registered.

This is implementation status, not a device acceptance result. The Android build and a real in-game test of opening the map, panning, zooming, saving a waypoint, and seeing it on both map views are still required before calling the feature complete.

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
