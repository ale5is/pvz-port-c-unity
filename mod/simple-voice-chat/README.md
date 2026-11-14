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

- `V` (default) is the **Push to Talk** key: hold it to transmit and release it to stop. In the mod settings page, switch **Microphone mode** to **Always On** to transmit whenever the microphone is unmuted and the voice session is connected; in that mode the V key is ignored.
- `M` (default) toggles **Mute My Microphone**: you stop transmitting but continue hearing other players.
- `B` (default) toggles **Mute Other Players**: you stop hearing incoming voices but continue transmitting.
- `N` (default) toggles **Mute Microphone + Players**: both directions are silenced.
- All four keys are independent native key bindings and can be reassigned from Horizon's Controls / key bindings screen. The settings page also has separate buttons for the transmission mode and each mute state. None of these controls disconnects UDP; only disabling the mod in Mods closes the voice session.
- `/svc mute`, `/svc unmute`, `/svc toggle`, `/svc status` control and report voice state.
- `/svc ptt KeyV` changes the push-to-talk key. Key names use Horizon's `KeyA` / `KeyV` format.
- `/svc volume 0-100` sets received voice volume; `/svc micvolume 0-100` sets microphone gain.
- Settings are loaded when a world opens and saved under `config/simple_voice_chat/config.json` using Horizon's shared native config directory.
- The Options > Mods Yes/No switch is the source of truth for enabling the native mod. Turning it off closes UDP and releases microphone/speaker streams; turning it back on starts a fresh voice-secret handshake in the current world.
- The mod's settings page has a **Microphone mode** toggle (Push to Talk / Always On), independent **Mute my microphone**, **Mute other players**, and **Mute microphone + players** controls, plus a manual **Reconnect to voice server** action. Muting the microphone releases capture; muting incoming voices releases speaker output. Both keep the UDP session active. Unmuting reopens the required audio devices when the session is connected.
- Android requests `RECORD_AUDIO` once from `HorizonActivity.onCreate`, after the native activity has initialized and while the player is still at app startup, before entering a world. The permission result is asynchronous; denying it never blocks world loading, and microphone capture remains closed until permission is granted. The game checks permission state while a world is active, so granting it later in Android Settings also takes effect.
- Android's `RECORD_AUDIO` permission remains separate from the mod switch. Muting closes the app's active capture stream but does not revoke Android permission; denying it prevents microphone capture.

Command replies are shown in the in-game system chat. Key bindings and reconnection are available from the native mod settings page; enable/disable is controlled by the Mods list's Yes/No switch.

## Validation

The Linux client-crate CI job has passed 24 unit tests covering protocol layouts, Opus encode/decode, audio resampling/mixing, UDP handshake, voice packets, server-address resolution, command filtering, and position refresh.

The crate's Android ARM64 cross-compile has passed. The repository also includes `patches/horizon-078/audio-permission.patch`, which adds `RECORD_AUDIO`, a runtime permission request, and a permission check before CPAL opens the microphone. The patched full Horizon Android build and on-device audio/UDP tests are still required before calling this port release-ready.
