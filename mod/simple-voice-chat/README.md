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
- Opens playback without microphone access; microphone capture starts only after Android grants `RECORD_AUDIO` and stops when leaving the world.
- Keeps Opus, UDP session, protocol, and audio code in the `svc_client` bundle crate.

## Controls and configuration

- Hold `V` (default) to transmit with push-to-talk; release it to stop.
- Press `M` to mute or unmute the microphone.
- `/svc mute`, `/svc unmute`, `/svc toggle`, `/svc status` control and report voice state.
- `/svc ptt KeyV` changes the push-to-talk key. Key names use Horizon's `KeyA` / `KeyV` format.
- `/svc volume 0-100` sets received voice volume; `/svc micvolume 0-100` sets microphone gain.
- Settings are loaded when a world opens and saved under `config/simple_voice_chat/config.json` using Horizon's shared native config directory.
- The Options > Mods Yes/No switch is the source of truth for enabling the native mod. Turning it off closes UDP and releases microphone/speaker streams; turning it back on starts a fresh voice-secret handshake in the current world.
- The mod's settings page contains a manual **Reconnect to voice server** action. It closes the old session and requests a fresh secret from the connected Minecraft server.
- The Android microphone prompt is deferred until after the first world frame, rather than being opened from the world-open lifecycle callback. Permission status is then polled asynchronously; microphone capture remains closed until permission is granted.
- Android's `RECORD_AUDIO` permission remains separate from the mod switch. Muting does not revoke the Android permission; denying it prevents microphone capture.

Command replies are shown in the in-game system chat. Key bindings and reconnection are available from the native mod settings page; enable/disable is controlled by the Mods list's Yes/No switch.

## Validation

The Linux client-crate CI job has passed 24 unit tests covering protocol layouts, Opus encode/decode, audio resampling/mixing, UDP handshake, voice packets, server-address resolution, command filtering, and position refresh.

The crate's Android ARM64 cross-compile has passed. The repository also includes `patches/horizon-078/audio-permission.patch`, which adds `RECORD_AUDIO`, a runtime permission request, and a permission check before CPAL opens the microphone. The patched full Horizon Android build and on-device audio/UDP tests are still required before calling this port release-ready.
