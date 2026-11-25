# Pavo GLES2 HUD surface prototype

This is a standalone Android API-22 NativeActivity test for the Philips QM152E. It is deliberately separate from Horizon: it does not claim to render a Horizon HudBatch or to be a playable game.

The native C++ entry point receives the Android NativeActivity callbacks and renders directly to the Activity's ANativeWindow through EGL. It does not use GLSurfaceView or create a second Java Surface. The render thread is stopped and joined when the native window is destroyed or the Activity pauses; a new EGL display surface, context, shader program, atlas and VBO are created when the window becomes available again.

## What the screen demonstrates

- Direct ANativeWindow -> EGL window surface ownership.
- EGL OpenGL ES 2 context creation (no GLES3 request).
- GLSL ES 1.00 vertex and fragment shader compile/link.
- 2×2 RGBA atlas upload and all four atlas regions.
- Interleaved position/UV/color vertex attributes.
- 24 vertices in one VBO and one glDrawArrays call per frame.
- Alpha blending and scissor state.
- Continuous frame presentation and GL/EGL error logging.
- Stop/join and resource recreation on NativeActivity pause/window destruction.

## Build

From this directory:

```sh
gradle --no-daemon --stacktrace assembleDebug
```

APK: `app/build/outputs/apk/debug/app-debug.apk`.

This NativeActivity path is intentionally closer to Horizon's NativeActivity/winit surface than the first GLSurfaceView experiment. It still does not share or replace Horizon's wgpu surface. The next integration stage is to create the GLES2 renderer inside Horizon's Android render lifecycle, use the same ANativeWindow only when the wgpu surface is not active, and convert the actual HudBatch/HudAtlas data.


## Lifecycle and mesh validation hardening

The NativeActivity harness now checks the native-window buffer geometry result and valid EGL surface dimensions before entering the render loop, requests a synchronized swap interval, and ignores stale window-destroy callbacks. The render-thread state distinguishes a running thread from a finished-but-joinable thread, so a later resume/window callback can restart after an EGL or presentation failure. Terrain VBO uploads reject vertex counts that are not complete triangle lists, and the renderer restores its texture/program bindings after the terrain passes. These changes harden the isolated prototype only; they do not connect it to Horizon's winit/wgpu launcher or real world sections.
