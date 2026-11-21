# PavoHorizon 1.0.0

PavoHorizon is an Android ARM64 build based on Horizon 0.7.8, with integrated Jade and Simple Voice Chat modules. The Pavo application version is independent of the underlying Horizon engine version.

The target is the Android APK, not a dynamically loaded desktop `.hnmod`. Horizon 0.7.8's `launcher/src/native_mods.rs` compiler builds a desktop `horizon-client` executable; its toolchain does not target Android. The Android app has its own `tools/android_build.py` pipeline. This port therefore uses the same pinned `horizon-mixins` source transformation directly on the Android source tree, then compiles `horizon-android`.

## Build

1. Obtain a clean Horizon source checkout at tag `v0.7.8` (commit `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`).
2. Install Rust/Cargo, Python, Android SDK, NDK `28.2.13676358`, build-tools `35.0.0`, and a JDK.
3. Prepare the Android engine data in that checkout once with `python tools/android_prepare.py`.
4. From this repository, run:

```sh
python scripts/build_android_port.py --horizon-source /path/to/starling --abi arm64-v8a --sdk /path/to/Android/Sdk --java-home /path/to/jdk --trust-native-code
```

The script installs the exact `horizon-mixins` revision used by Horizon 0.7.8 if it is not already on PATH, applies the API-80 bundle to a separate staged source tree, copies the prepared engine data, and invokes Horizon's own Android build script. Use `--check-only --trust-native-code` first to type-check without packaging an APK. The trust flag is required because Cargo executes the mod's reviewed build script.

The Opus C/H source is kept under `mod/data/vendor/` because Horizon mixin bundles only permit C/H files inside the `data/` payload area. The mixins staging process places it under `native-data/simple-voice-chat/vendor/`, where the crate build script reads it.

The ARM64 Android build pipeline has completed successfully for the integrated Jade and Simple Voice Chat modules. Real-device microphone permission, audio capture/playback, UDP connection and server interoperability still require runtime testing.

The launcher version is stored in `PAVO_VERSION`. Android in-place updates require the persistent signing key described in [Android updates](docs/ANDROID_UPDATES.md). The current Horizon engine pin remains 0.7.8.

Horizon Atlas 0.3.0 is vendored as the minimap expansion source reference under `mod/atlas-upstream/`. It is not yet integrated: its upstream hooks API 415 and Horizon revision differ from Pavo's API 80 / Horizon 0.7.8. See [the Atlas port plan](docs/ATLAS_PORT_PLAN.md).

## Source layout
- `mod/`: adapted hooks, Rust client, protocol and audio implementation.
- `mod/data/vendor/`: vendored libopus 1.4 C/H sources.
- `scripts/build_android_port.py`: Android source-transform/build pipeline.
- `scripts/package_hnmod.py`: packages a desktop native-mod bundle; it is not the Android APK builder.

See [port status](PORTING_STATUS.md) and [upstream pins](UPSTREAM.md).
