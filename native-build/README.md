# Horizon 0.7.8 native-mod build service

This is the first backend layer for bringing Starlight's remote native-mod compiler to Horizon.
It targets the pinned Horizon 0.7.8 source commit, the horizon-mixins revision used by this port,
and Android ARM64.

## Implemented

- A pinned source map in native-build/versions.json.
- Repeatable source and toolchain preparation.
- A build script that applies the microphone/network patches, includes the Simple Voice Chat
  bundle in every build, applies player bundles with horizon-mixins, and compiles
  libhorizon_android.so using Horizon's own tools/android_build.py --rust-only.
- A networkless build sandbox. Source, patches, built-in mods and the base cache are mounted
  read-only or through a disposable overlay; untrusted mod compilation receives no runner
  environment secrets and no network access.
- Strict .hnmod ZIP validation and a one-shot GitHub Actions worker.
- Separate workflows to prepare the trusted base cache and build an individual request.

## Prepare the trusted cache

Run Actions → Prepare Horizon 0.7.8 native-mod build cache → Run workflow once.
It builds the Horizon 0.7.8 + Simple Voice Chat base and saves the Rust, Android NDK, Cargo,
horizon-mixins and incremental target cache.

Then a build request can be submitted to Horizon native mod build with:
- version=0.7.8
- request_id: a random one-time identifier
- request_url: an HTTPS URL returning the request body

The request body is a sequence of big-endian 32-bit lengths followed by .hnmod ZIP files.
The output artifact contains libhorizon_android.so and build.json.

## Not yet the in-game feature

The build worker and GitHub workflow are foundations, not a finished in-game compiler.
The Android file picker and next-start native-library loader are already patched into the APK.
The remaining in-game work is the Starlight-style build card and its Rust client: collect enabled
.hnmod files, upload them to the configured gateway, poll progress, verify the downloaded SHA-256,
store the library under files/native, and mark it current for the next launch. The gateway code is
provided, but it still must be deployed behind HTTPS with a server-side GitHub token. That token
must never be embedded in the APK.

Native mods execute with the game's process permissions. Only compile code from authors you trust.
The worker validates the bundle format and runs compilation without network access or secrets, but
that is not a guarantee that the resulting native library is safe to load.
