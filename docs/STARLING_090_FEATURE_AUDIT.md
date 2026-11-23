# Starling 0.9.0 feature audit — PavoHorizon API 80

## Exact source

- Release: Starling v0.9.0
- Commit: `ff303502a4f7982551a0e0d3622d9268efe9e829`
- Base: Horizon Android v0.7.8
- Base commit: `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`
- Native hooks API: 80
- Target branch: `port/starling-090-performance`

The 0.9.0 release notes explicitly say that its terrain path is drawn as Horizon Android 0.7.8 drew it. They also list paused-menu world-render skipping, a 4096 shadow-map ceiling, a 60 FPS ceiling for menus over the world, faster mod startup, GPU calibration, background pause, and the launcher/mod-system changes below.

This is not a source-tree upgrade. The release is about 31,000 Horizon commits ahead of the earlier Starling base and changes the mod API. Replacing the pinned source would break the premise of the API-80 port and risk the existing SVC, Jade and Atlas integrations.

## Renderer and performance

| Starling 0.9.0 improvement | API-80 audit | Port decision |
|---|---|---|
| Terrain drawn as Horizon Android 0.7.8 | Explicitly confirmed by the 0.9.0 release notes. Our build already checks out the exact 0.7.8 base, so its Vulkan batching/compatibility and GLES renderer must remain the reference. | Preserve the 0.7.8 renderer; do not copy the newer renderer over it. |
| Paused menus stop drawing the world | Confirmed already present in 0.7.8: `game.rs` tracks `was_menu_blurred`, computes `menu_blur_cache` and `skip_world_for_blur`, and passes `menu_blur_cache` into `gfx::Frame`; `gfx.rs` owns the cached blur. The 0.9.0 `screen_backgrounds::menu_blur_cache` is the reorganized equivalent, explicitly adapted from Horizon Android. | Already present. Preserve it; add a regression test around pause/unpause and menu transitions if needed. |
| Shadow maps capped at 4096 on Android | The 0.7.8 `gfx/pack_shadow.rs` already clamps the requested side to `min(device.max_texture_dimension_2d, 4096)`. | Already present. Add a regression test only if the source patch can do so without changing behavior. |
| Menus over the world capped at 60 FPS | 0.9.0 adds `menu_frame_cap(limit, has_screen)` after the user's and Battery mode's limits. | Implemented in the API-80 build patch. It only lowers the display frame cap when a screen is open on Android; it does not change world ticks or the user's lower FPS limit. Regression test added; CI pending. |
| About 6 seconds faster startup with ~20 mods | The release couples this with the new mod API/package loader. The API-80 build uses a different bundled native-mod path. | Profile the existing loader first; port only cache/parallel preparation changes that do not change mod ABI or load order. |
| GPU `Calibrate...` | 0.9.0's `game/starling/perf_lab.rs` uses a generated benchmark world and times renderer variants (direct/indirect/flat terrain, late sky, occlusion and hidden faces). The 0.7.8 renderer already exposes some A/B controls, but not every 0.9.0 choice. | Backport a reduced calibration over choices actually supported by API 80. Never let it disable the 0.7.8 Vulkan batching path or select an unsupported backend. |
| GPU culling Auto and newer graphics controls | 0.9.0 adds renderer settings and an autotuner based on CPU/GPU frame timing. This is not the same thing as the old Vulkan batching switch. | Evaluate individually after the calibration and renderer capability map. Default off; retain manual 0.7.8 controls. |
| Loading screen appears in about 0.5 seconds | This is coupled to the newer launcher/activity and loading pipeline. | Trace startup stages and move only safe, immutable preparation off the UI/render thread. |
| Background pause and audio mute | 0.7.8 already handles Android `WindowEvent::Occluded`: it marks the app backgrounded, opens the pause screen for an active world, syncs the integrated server pause, and waits instead of continuing redraws. 0.9.0 adds the user-facing `Pause in the Background` preference and describes muting audio. | Preserve the existing lifecycle behavior. Port only the preference/opt-out and verify audio mute/resume; do not add a second lifecycle path. |
| Frame pacing, thermal and diagnostics | 0.9.0 adds fixed-grid `end_frame_on_grid` pacing for Efficient mode and breaks long sleeps into 1 ms steps; it also adds performance measurements and a menu FPS display. | Fixed-grid pacing and short-step sleeps are now backported into the API-80 build patch with regression tests. Thermal/performance diagnostics remain separate. CI pending. |
| Android frame hints and display refresh matching | 0.9.0 dynamically looks up Android's ADPF session API and `ANativeWindow_setFrameRate`: it reports render-thread CPU time and asks the display to match the selected FPS cap. These symbols only exist on newer Android versions. | Not yet ported. Add optional runtime symbol lookup only; older Android must fall back cleanly, and no API-level increase is allowed. |

## Other 0.9.0 features

| Feature | Dependency / risk | Port decision |
|---|---|---|
| New launcher with profiles, per-profile worlds/settings/mods | New `ha_launcher`/launcher architecture, persistence and import/export paths. Not a small UI patch. | Separate staged port; keep current Pavo launcher usable until profile migration is tested. |
| Horizon + Modrinth catalog for mods, resource packs and shaders | New catalog clients, download/cache/indexing logic and UI. | Port independently after profile/storage paths are settled. |
| Skin/cape wardrobe synced with Microsoft | New account/cosmetics code and authenticated network/storage flows. | Separate feature; do not disturb existing account/session handling. |
| Custom GPU drivers (experimental, arm64) | `libadrenotools`, driver packages, native loading and recovery/fallback logic. | Optional isolated feature only after signature, path, ABI and crash-recovery review. Never install/select a driver automatically. |
| Experimental ray tracing | New wgpu ray-query/device/pipeline paths. | Do not backport until the API-80 renderer and device support can be proven. |
| Display controls and safe zones | New Android display settings, Java/JNI bridge and coordinate mapping for UI/touch. | Port only with tests for cutouts, rotation, scaling and touch alignment. |
| Touch labels, preset deletion and corrected hub click handling | New Starling touch editor and event ownership code. | Backport the individual editor/input fixes to the existing touch system; preserve saved layouts. |
| Mouse title-card interaction and menu FPS counter | New title-screen and overlay code. | Low priority after the performance/lifecycle changes. |
| Diagnostics from app icon; choose next graphics API even if game will not start | New DiagnosticsActivity, native crash report and driver recovery interfaces. | Port diagnostics as a separate Android feature; do not couple it to renderer settings migration. |
| Hide game-folder pictures from gallery; more Malay translations | Android media-scanner handling and localization assets. | Small independent changes after checking existing storage behavior. |
| Mod switches apply after closing Options; invalid recipe no longer blocks world load | New mod manager/API and recipe registration. | Inspect for isolated API-80-compatible fixes; do not replace the mod loader wholesale. |
| PowerVR Vulkan crash/limits; Mali background-load crash; 32-bit startup; shader compute bounds; touch and world-upgrade fixes | Each has a separate device/renderer/storage cause. | Reproduce and port each fix separately; do not treat the release note as proof the API-80 code has the same bug. |

## Native mods: hard compatibility boundary

Starling 0.9.0 no longer runs its old `.hnmod` format; it uses Horizon's newer mod API and a server-built native library matched to the exact Starling build. PavoHorizon currently compiles its API-80 SVC, Jade and Atlas integrations into the APK using its pinned `horizon-mixins` revision.

Therefore:
- Keep Horizon 0.7.8 and `horizon-mixins` pins unchanged.
- Keep the Pavo package identity, signing key, ABI and APK build path.
- Do not copy 0.9.0's mod loader or bump the native hooks API as part of a renderer patch.
- Any newer mod API work must be a separate compatibility project with explicit SVC/Jade/Atlas ports and tests.

## Implementation order

1. Verify the 0.7.8 renderer parity points (terrain draw path, menu blur cache, 4096 shadow cap and existing frame limiter).
2. Add a dedicated 60 FPS cap for menus over the world, with tests for world FPS, menus, unfocused/background and user-selected limits.
3. Add background pause/audio lifecycle handling with save/resume tests.
4. Port a reduced GPU calibration using only renderer choices actually supported by API 80.
5. Profile and optimize mod startup without changing API 80 or load order.
6. Port the touch editor and display safe-zone fixes.
7. Treat launcher/profiles/catalog, cosmetics, custom GPU drivers, ray tracing and diagnostics as separate milestones.

## Required validation

- Rust native compile for `aarch64-linux-android`.
- Android APK build, package/version check and signature verification.
- Verify SVC, Jade and Atlas remain bundled and togglable.
- No change to Horizon, `horizon-mixins`, package ID, signing key or ABI without a separate reviewed decision.
- Tests for menu/world FPS separation, pause/resume, app background/foreground, audio, world save, touch, display rotation/cutouts and all existing renderer backends.
- Same-device A/B: average FPS, 1%/0.1% lows, frame-time p50/p95/p99, startup/world-load time, RSS and thermals.
- Do not claim a performance win from desktop benchmarks or from a build that has not been tested on the target phone.

## Current status

This file records the source audit and port order. The first API-80-compatible code backport now includes the 60 FPS Android menu cap and fixed-grid Efficient frame pacing. The workflow runs regression tests for the frame limiter and builds the signed APK; device A/B testing is still required before claiming a performance improvement.
