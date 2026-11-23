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
| Dynamic Resolution | 0.9.0 adds a GPU-time-driven resolution scale controller and target FPS, separate from the fixed resolution-scale setting. | Not yet ported. Requires measuring GPU time reliably on API 80 and proving resize does not rebuild expensive resources every frame; off by default until tested. |
| FSR 3.1 Frame Generation | 0.9.0 adds WGSL frame interpolation and new renderer pipelines. | Experimental/high risk. Do not port until API-80 Vulkan/GLES feature support, frame pacing, latency and memory costs are measured on target devices. |
| Mobile far-detail renderer | 0.9.0 adds a third LOD mode (Mobile) that reduces far-terrain geometry to about one-sixteenth of Full, plus mobile-specific leaf culling and device-tier defaults. | API-80 now adds Mobile mode, three-level quadtree coarseness, the 256-chunk integrated-GPU cap, and a first-run Android default that preserves any saved Full/Reduced choice. It keeps the 0.7.8 mesh pipeline; 0.9.0's dedicated leaf-culling modes and tier-based render-distance defaults remain outstanding. Quadtree, serialization and GPU-cap tests are in CI; visual/device A/B is still required. |
| Loading screen appears in about 0.5 seconds | This is coupled to the newer launcher/activity and loading pipeline. | Trace startup stages and move only safe, immutable preparation off the UI/render thread. |
| Loading-menu redraw cap | 0.9.0's Android loading loop schedules and rejects early redraws so the loading menu does not spin uncapped while resources prepare. The 0.7.8 Loading loop uses Poll and requests redraw continuously. | Backported a 60 FPS Android loading-only guard using the loading menu's last-frame timestamp; it keeps resource polling and native events running between redraws. A host regression test covers first-frame, overdue-frame and Android timing behavior; CI pending. |
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
| Phone defaults: 100% resolution scale and Mobile far detail | 0.7.8 already defaults resolution scale to 100%; it has only Full/Reduced far detail. 0.9.0 adds `FarDetail::Mobile`, device tiers and a separate mobile LOD distance/culling path. | Keep the existing 100% default. Mobile far detail is a renderer feature, not a settings-only change; backport its LOD/culling path only after comparing shader and mesh semantics. Never substitute Reduced and call it Mobile. |
| Catalog and mod fixes | 0.9.0 restores Horizon catalog listings, avoids duplicate downloads, and fixes invalid recipes that previously blocked world startup. | Port only isolated catalog/storage/recipe fixes that do not depend on the new mod API. |
| Touch labels, preset deletion and corrected hub click handling | New Starling touch editor and event ownership code. | Backport the individual editor/input fixes to the existing touch system; preserve saved layouts. |
| Touch and UI regressions | 0.9.0 fixes camera/chat buttons, GoyControl sneak, F3+F4 switching, control editor input, inventory player preview cursor tracking, sliders grabbing fingers while scrolling, and vibration remaining off. | Treat each as a separate reproduction and regression test; do not replace the whole touch system. |
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

1. Preserve the verified 0.7.8 renderer parity: terrain path, cached menu blur, 4096 shadow cap and existing backends.
2. Finish CI for the menu/loading caps, fixed-grid Efficient pacing and Mobile LOD; then run same-device A/B.
3. Add optional Android frame hints/display refresh matching, with runtime fallbacks on older Android.
4. Verify the existing background pause path; add only the missing preference/audio behavior.
5. Port a reduced GPU calibration using only renderer choices actually supported by API 80.
6. Finish Mobile far detail with compatible leaf culling and tier-based first-run defaults; then port Dynamic Resolution only with reliable GPU timing and resize-cost tests.
7. Profile mod startup and port touch/display/catalog fixes without changing API 80 or load order.
8. Treat launcher/profiles, cosmetics, custom GPU drivers, FSR frame generation, ray tracing and diagnostics as separate milestones.

## Required validation

- Rust native compile for `aarch64-linux-android`.
- Android APK build, package/version check and signature verification.
- Verify SVC, Jade and Atlas remain bundled and togglable.
- No change to Horizon, `horizon-mixins`, package ID, signing key or ABI without a separate reviewed decision.
- Tests for menu/world FPS separation, pause/resume, app background/foreground, audio, world save, touch, display rotation/cutouts and all existing renderer backends.
- Same-device A/B: average FPS, 1%/0.1% lows, frame-time p50/p95/p99, startup/world-load time, RSS and thermals.
- Do not claim a performance win from desktop benchmarks or from a build that has not been tested on the target phone.

## Current status

This file records the source audit and port order. The API-80 build patch now includes the 60 FPS Android menu cap, fixed-grid Efficient frame pacing, Mobile far-detail (settings, quadtree coarseness, integrated-GPU distance cap and first-run Android default), and a 60 FPS loading-menu redraw guard. Regression tests are included for each. CI has not yet validated the latest commit, and no device A/B result is claimed.
