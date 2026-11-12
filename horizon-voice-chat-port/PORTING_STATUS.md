# Porting status

## Confirmed incompatibilities

1. Native hooks API: Horizon 0.7.8 = 80; current Starling = 359; mod requires 359.
2. Hook table size: Horizon 0.7.8 = 108 entries; current Starling = 386.
3. `world_lifecycle`, `net_from_server`, `command`, and `frame` have counterparts in Horizon 0.7.8; signatures still require direct comparison.
4. `player_submit` has no direct equivalent in the 0.7.8 hook table. Do not substitute an unrelated block-entity render hook.
5. Horizon 0.7.8 exposes `native_key(&mut self, key: &str, pressed: bool)`; the mod input hook expects a richer key-event/modifier/GLFW-style contract. An adapter is required.
6. Android integration differs: Horizon 0.7.8 uses package `dev.horizon.android` and `HorizonActivity.java`; current Starling uses `io.github.juanluci.starling` and `StarlingActivity.java`. Preserve 0.7.8 package/app identity.

## Required implementation

- [ ] Import the complete mod source and required assets/dependencies, including Opus native/vendor sources.
- [ ] Implement a 0.7.8 hook compatibility layer; map only semantically equivalent hooks.
- [ ] Implement or redesign player-submit integration against the actual 0.7.8 player/render lifecycle.
- [ ] Adapt key input while preserving key-down/up semantics and documenting modifier/repeat limitations.
- [ ] Backport the native-mod loader into the 0.7.8 Android activity/JNI lifecycle.
- [ ] Adapt metadata/API-version checks for API 80.
- [ ] Backport packaging and permission handling; verify ABI and native library paths.
- [ ] Resolve the horizon-mixins revision mismatch and confirm ABI/API requirements.
- [ ] Build with a pinned Android NDK/toolchain and run static checks.
- [ ] Install on device and test connection, capture, playback, lifecycle, permissions, and server interoperability.

## Build reference

Current Starling build instructions indicate Android NDK 28.2.13676358, Build Tools 35, Android platform 36, Rust stable, target `aarch64-linux-android`, and cargo-ndk 3.5.4. These are reference values, not yet validated for this backport.

## Validation boundary

No successful build, installation, runtime test, or end-to-end voice session has been established. Do not describe this port as working until those checks pass.
