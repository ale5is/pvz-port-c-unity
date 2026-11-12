# Simple Voice Chat — Horizon 0.7.8 port

This API-80 adaptation speaks the Simple Voice Chat 2.6.x client protocol (compatibility version 20).

- Direct Java-server path: sends and receives raw `voicechat:*` custom payloads.
- Horizon dedicated-server path: also mirrors control messages through `horizon:mod_net` when the server announces that channel, for the Horizon `voicechat-server` relay.
- Audio: CPAL 0.16 Android backend plus vendored libopus 1.4.
- Microphone permission: the Android build adapter adds `RECORD_AUDIO` to the manifest and requests it at runtime.

Horizon 0.7.8 hook API: 80. The remote client connection is detected from `Remote.logged_in` in the frame hook; the 0.7.8 `world_lifecycle` hook is server-side and does not fire for remote clients.

No APK build or runtime test has been completed yet.
