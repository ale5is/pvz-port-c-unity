# Upstream provenance

| Component | Repository / revision | Purpose |
|---|---|---|
| Horizon Android 0.7.8 | https://github.com/starling-client/starling — tag `v0.7.8`, commit `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29` | Target runtime |
| Starling current | https://github.com/starling-client/starling — commit `c4e44c1e0df785373572a479a9adb4fb99eff7fb` | Native mod loader reference |
| Simple Voice Chat native mod | https://github.com/Sparticle62ops/horizon-simple-voice-chat — main tree `b3bf3b792a04f8053dfcf22ed1e5f879227e6c89` | Feature implementation to adapt |
| Horizon mixins | https://github.com/horizon-rust/horizon-mixins | Mixin/runtime dependency |

## Revision mismatches
- Mod metadata expects Horizon revision `3fb4c3138e0c5d8fdd2fdb555cf31368e8897497`.
- Mod metadata expects horizon-mixins revision `d9c83d7096b7ea5b83678da2efdfd6161f74997e`.
- Current Starling native build script references horizon-mixins revision `ddd9700b21997c95d95ac6ee5ecafe3830cf622a`.
- Existing .hnmod artifact hash: `daab67ade4a5d22f922a681a792d449cea9b91c1d7da0bd710d52c9e7b07f855` (823 KB). This artifact is not proof of compatibility with 0.7.8.
