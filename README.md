# Horizon Port Pavo

Native Simple Voice Chat port for Horizon Android 0.7.8.

The repository's root project is dedicated to this port. The mod workspace is in `mod/`; its compatibility metadata targets Horizon hook API 80.

## Build pipeline
Horizon 0.7.8 already contains its own native-mod compiler and Rust toolchain. Use that launcher pipeline; do not copy Starling's newer launcher or change the target runtime.

The source bundle can be packaged with:

```sh
python3 scripts/package_hnmod.py
```

This creates `dist/simple-voice-chat-horizon-0.7.8.hnmod`. The packager moves the vendored Opus C/H files into the bundle's `data/` area because Horizon's native-bundle validator only permits Rust/TOML source plus data payloads. Horizon stages that payload under `native-data/simple-voice-chat/vendor`, where the crate build script reads it.

See [port status](PORTING_STATUS.md), [upstream pins](UPSTREAM.md), and [mod source](mod/README.md).

No successful compilation or runtime test is claimed yet.
