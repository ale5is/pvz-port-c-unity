# Horizon Atlas — pinned porting source

This directory vendors the upstream Horizon Atlas 0.3.0 Rust implementation and its mixin adapters as a reference base for PavoHorizon.

- Upstream: https://github.com/MrCloudy2/horizon-atlas
- Pinned upstream commit: 66737a3e887dc0fb17eb6e6868efed1c11a76228
- Upstream Horizon revision: 9f42064c11efe7ef3e5ed2baad7ddc2a3b36eea0
- Upstream native hooks API: 415
- PavoHorizon target: Horizon 0.7.8, revision 68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29, native hooks API 80

## Important

This is a vendored source and test baseline, not yet an Android integration. Do not register its hooks or enable it in the APK until the bridge and every injection have been ported to API 80 and verified against the pinned 0.7.8 game internals. Atlas reads chunk palettes, render assets, entities, world storage and networking internals that may have changed between the two revisions.

The core crate is deliberately standalone and uses only Rust's standard library. Run its logic tests with:

    cargo test --manifest-path mod/atlas-upstream/Cargo.toml

The next integration stage is to adapt `bridge.rs` and its hook adapters to Pavo's API-80 source, then run the Android ARM64 build. Keep Horizon's built-in minimap as a fallback until the Atlas implementation passes in-game tests.

Retain the upstream MIT licence and copyright notices in `LICENSE`.
