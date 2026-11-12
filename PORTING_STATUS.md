# Port status — Horizon Port Pavo

## Confirmed from Horizon 0.7.8 source
- The 0.7.8 launcher already includes native-mod compilation: `launcher/src/native_mods.rs`, `launcher/src/native_mods/crates.rs`, and `launcher/src/rust_toolchain.rs`.
- Hook API is 80. The needed `frame`, `world_lifecycle`, `key`, `command`, and `net_from_server` hooks exist.
- Native bundles are ZIP-based `.hnmod` files. Their manifest requires schema, id, version, game, exact Horizon and mixins commits, and optional hook API.
- Bundle source files are restricted to Rust/TOML plus text and `data/` payloads. Opus C/H files therefore must be packaged as data, then read from Horizon's `native-data/<mod-id>/` staging directory during the crate build.

## Implemented in this workspace
- API-80 hook manifest and adapters for lifecycle, key, server network, command and frame.
- Removed the unused player-submit callback without substituting an unrelated hook.
- Adapted key names from `key.keyboard.v` to the mod's `KeyV` representation.
- Prevented the command hook from claiming unrelated commands.
- Imported the client protocol, session, audio and Opus FFI Rust code.
- Imported 205 Opus C/H files; the build script supports both direct-source builds and the `native-data` layout used by packaged `.hnmod` bundles.
- Added a packager that converts the workspace to the 0.7.8 bundle ZIP layout.

## Not yet verified
- The `.hnmod` packager has not been run in this environment.
- The 0.7.8 `horizon-mixins` apply step has not been executed against the mod.
- No native build, Android build, installation, microphone capture, UDP handshake or playback has been tested.
- The Android audio backend and platform permissions remain a specific validation risk.
