# Android 5 / GLES2 CI strategy

## Automatic checks on branch pushes

Only `PavoHorizon Android 5 API-22 diagnostic` runs automatically for Android 5 renderer work. Its path filter covers the Horizon patch scripts, renderer boundary and lifecycle checks, GLES2 shaders, native renderer sources, and their checkers.

That workflow already performs the native GLES2 ARMv7/API-22 CMake configure and build. Running the separate HUD prototype workflow on every push duplicated that native compilation.

## Manual builds

- `Pavo GLES2 HUD surface prototype`: run manually when the isolated native surface prototype itself needs an APK. Leave `build_prototype_apk=false` for contract and native compilation checks; set it to true only when an installable prototype APK is specifically needed.
- `PavoHorizon Android 0.7.8 ARMv7 baseline`: run manually when validating the untouched upstream API-27 ARMv7 baseline. It is not an Android 5 build and does not need rebuilding for every GLES2 adapter change.
- The diagnostic workflow's `build_diagnostic_apk` remains an explicit manual option. It produces a diagnostic APK, not a playable game.

## Why this split

The API-22 diagnostic is the continuous-integration gate for changes in this port. The prototype APK and original API-27 baseline are expensive, independent artifacts and are opt-in. This avoids three overlapping workflows after each renderer commit while retaining all three validations when explicitly requested.

A successful CPU/static contract check is not proof of playable rendering or physical-device compatibility.
