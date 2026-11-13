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

## In-game workflow

1. Import a compatible `.hnmod` package through the Android file picker. It is stored in
   `files/horizon-native-mods/`.
2. Put the HTTPS gateway base URL in `files/horizon-native-mods/build-server.txt`.
3. Open Options → Mods → Native Mod Builder and choose Compile installed native mods.
4. The app uploads the packages, displays build progress, verifies the returned SHA-256 and
   records the compiled library under `files/native/`.
5. Restart Horizon. The Android bootstrap loads the verified library for that APK installation;
   if the custom build fails to reach the game window twice, it falls back to the APK's original
   library.

The gateway must be deployed behind HTTPS with a server-side GitHub token. That token must never
be embedded in the APK.

Native mods execute with the game's process permissions. Only compile code from authors you trust.
The worker validates the bundle format and runs compilation without network access or secrets, but
that is not a guarantee that the resulting native library is safe to load.
