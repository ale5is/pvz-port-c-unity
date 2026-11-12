# Port status — Horizon Port Pavo

## Implemented
- API-80 adapters for key, command, frame and Horizon native mod network hooks.
- Added a raw `Clientbound::CustomPayload` hook in `Game::handle_packet` for standard `voicechat:*` packets from Java servers.
- Added raw `Serverbound::CustomPayload` transmission through the 0.7.8 remote connection.
- When the server advertises Horizon mod networking, control messages are also mirrored through `horizon:mod_net` for the Horizon `voicechat-server` relay.
- Replaced server-only world lifecycle detection with remote-client login detection in the frame hook.
- Added server-host resolution from the active remote session so an empty `voice_host` uses the actual game server address instead of localhost.
- Reconnect requests are queued when the UDP session reports a dropped connection.
- Imported client protocol/session/audio Rust sources and 205 Opus C/H files.
- Android build adapter requests runtime microphone permission and uses the pinned Horizon 0.7.8 Android build pipeline.

## Compatibility checks
- Horizon 0.7.8 native hooks API: 80.
- All named hook names and versions in the manifest are present in `native-hooks.json` at API 80.
- CPAL 0.16.0, NDK 0.9.0, JNI 0.21.1 and cc 1.6.0 are present in the Horizon 0.7.8 Cargo.lock.
- `horizon-mixins` is pinned to the 0.7.8 launcher revision.

## Not yet verified
- `horizon-mixins apply` has not been run against a local Horizon checkout.
- No Android `cargo check`, ABI build or APK build has succeeded yet.
- No install or device test has verified microphone capture, playback, UDP, or server interoperability.
