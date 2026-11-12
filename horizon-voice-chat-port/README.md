# Horizon 0.7.8 — Simple Voice Chat native port

This directory is an isolated research and porting workspace. The Unity project at the repository root is not modified.

## Target
- Horizon Android 0.7.8 (base revision: `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`)
- Simple Voice Chat native mod: `Sparticle62ops/horizon-simple-voice-chat`
- Current Starling native-mod loader as a reference, not as the target runtime.

## Confirmed compatibility findings
- Horizon 0.7.8 advertises Native Hooks API 80 (108 hook entries).
- Current Starling advertises API 359 (386 hook entries).
- The current Simple Voice Chat mod declares API 359 and cannot be loaded unchanged by Horizon 0.7.8.
- Existing 0.7.8 hooks cover world lifecycle, server networking, command, and frame events, but player-submit has no direct equivalent. The key hook also has a different contract from the mod's input hook.
- Android loader, metadata validation, native library loading, permissions, and packaging need a 0.7.8-specific adaptation.

## Status
Research and implementation plan only. This is not yet a completed, compiled, or device-tested port. See `PORTING_STATUS.md` and `UPSTREAM.md`.

## Layout
- `PORTING_STATUS.md`: API gaps and implementation checklist.
- `UPSTREAM.md`: upstream repositories and pinned revisions.
- `sources/`: selected upstream source files for comparison.
