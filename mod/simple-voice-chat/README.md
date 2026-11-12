# Simple Voice Chat — Horizon 0.7.8 port

This is the API-80 adaptation of the Simple Voice Chat client for Horizon Android 0.7.8.

- Target Horizon revision: `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`
- Native hooks API: `80`
- horizon-mixins revision: `a2af5c0dd4117d51ecd70066ba7a00b4a2c10dab`

## Port-specific changes

- Uses the API-80 `key`, `frame`, `world_lifecycle`, `command`, and `net_from_server` hooks.
- Replaces the unavailable API-359 `player_submit` hook with a small bridge into Horizon 0.7.8's tracked remote-player table. The frame hook refreshes listener and speaker positions for proximity audio.
- Resolves an empty voice host from the connected Minecraft server address. It no longer silently falls back to loopback.
- Intercepts only `/svc` and `/vcgroup`; unrelated Minecraft commands are left untouched.
- Keeps Opus, UDP session, protocol, and audio code in the `svc_client` bundle crate.

## Validation

The Linux client-crate CI job has passed 23 unit tests covering protocol layouts, Opus encode/decode, audio resampling/mixing, UDP handshake, voice packets, server-address resolution, command filtering, and position refresh.

The crate's Android ARM64 cross-compile has passed. The repository also includes `patches/horizon-078/audio-permission.patch`, which adds `RECORD_AUDIO`, a runtime permission request, and a permission check before CPAL opens the microphone. The patched full Horizon Android build and on-device audio/UDP tests are still required before calling this port release-ready.
