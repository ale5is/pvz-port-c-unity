# Port status — Horizon Port Pavo

## Confirmed from Horizon 0.7.8 source
- Native hook API is 80. The needed `frame`, `world_lifecycle`, `key`, `command`, and `net_from_server` hooks exist.
- The 0.7.8 desktop launcher's native-mod compiler builds a desktop `horizon-client` executable. Its bundled Rust toolchains target desktop hosts only; it is not the Android APK pipeline.
- Android is built separately by `tools/android_build.py`, which compiles package `horizon-android` for Android ABIs.
- The pinned horizon-mixins revision supports named hooks, bundle crates and data staging. C/H vendor files must be placed in bundle `data/`; the crate build script then reads them from `native-data/<mod-id>/`.

## Implemented
- API-80 hook adapters for lifecycle, key, server network, command and frame.
- Removed the unused player-submit callback; no unrelated hook was substituted.
- Adapted vanilla key names and prevented the command hook from claiming unrelated commands.
- Imported the client protocol, session, audio and Opus FFI Rust code.
- Imported 205 Opus C/H files and made the build script support both direct-source and staged-data layouts.
- Added a bundle packager and an Android-specific build script that applies the exact 0.7.8 mixins revision to a clean Horizon source tree before invoking the Android APK build pipeline.

## Not yet verified
- The bundle packager has not been executed.
- The mixins apply stage has not been executed against a local Horizon checkout.
- No Android cargo check or APK build has succeeded yet.
- No device installation, microphone capture, audio playback, UDP handshake or server interoperability has been tested.
