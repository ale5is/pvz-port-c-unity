# Pavo GLES capability probe

Standalone Android diagnostic for the Android 5 / ARMv7 television target.

- Minimum Android API: 22
- Requests EGL contexts for OpenGL ES 3.x and 2.x independently.
- Prints EGL version/vendor/extensions, GL version/vendor/renderer/GLSL version/extensions, common hardware limits, and an RGBA8 framebuffer completeness test.
- Does not include Horizon, wgpu, game assets, network access, or audio.

Build with JDK 17, Android SDK platform 35, build-tools 35.0.0, and Gradle 8.9:

    gradle --no-daemon assembleDebug

Install the resulting app-debug.apk on the target TV and read the report on screen. The full report is also written to logcat under the tag PavoGlesProbe.

The result is a capability probe, not a renderer compatibility test. A successful GLES3 context does not prove that all Horizon shaders or formats work; a successful GLES2 context only confirms that the driver can create that context.

The API-22 APK is built by `.github/workflows/pavo-gles-probe.yml` and published as the `pavo-gles-capability-probe-api22` artifact.
