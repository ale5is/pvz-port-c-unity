# Port status — Horizon Port Pavo

## Implemented
- API-80 adapters for key, command, frame and Horizon native mod network hooks.
- Added a raw `Clientbound::CustomPayload` hook in `Game::handle_packet` for standard `voicechat:*` packets from Java servers.
- Added raw `Serverbound::CustomPayload` transmission through the 0.7.8 remote connection.
- When the server advertises Horizon mod networking, control messages are also mirrored through `horizon:mod_net` for the Horizon `voicechat-server` relay.
- Replaced server-only world lifecycle detection with remote-client login detection in the frame hook.
- Added server-host resolution from the active remote session so an empty `voice_host` uses the actual game server address instead of localhost.
- Reconnect requests are queued when the UDP session reports a dropped connection.
- Imported the client protocol/session/audio Rust sources and 205 Opus C/H files under bundle `data/vendor/`; Horizon stages them under `native-data/simple-voice-chat/vendor/`.
- Android build adapter requests runtime microphone permission and uses the pinned Horizon 0.7.8 Android build pipeline.
- Linked the `svc_client` bundle crate into the Android game target and exposed the key-event bridge used by the API-80 hook.

## Compatibility checks
- Horizon 0.7.8 native hooks API: 80.
- All named hook names and versions in the manifest are present in `native-hooks.json` at API 80.
- CPAL 0.16.0, NDK 0.9.0, JNI 0.21.1 and cc 1.6.0 are present in the Horizon 0.7.8 Cargo.lock.
- `horizon-mixins` is pinned to the 0.7.8 launcher revision.
- GitHub Actions workflow `Horizon Android port check`, run [#16](https://github.com/ale5is/pvz-port-c-unity/actions/runs/43964323307), completed successfully on commit `cf056b374a5f91224703d57d2c30287f93809212`. This ran `horizon-mixins apply` and Android ARM64 `cargo check` against the pinned Horizon 0.7.8 source.

## Not yet verified
- A release-profile Android APK has not yet been built from this commit.
- No install/device test has verified microphone capture, playback, UDP, or server interoperability on a physical Android device.
- Jade has not yet been integrated; its original port targets a different Horizon/hooks API and needs a separate API-80 adaptation.
