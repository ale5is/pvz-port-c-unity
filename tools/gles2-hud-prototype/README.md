# Pavo GLES2 HUD surface prototype

This is a standalone Android API-22 app used to validate the first real-window GLES2 HUD path on the Philips QM152E before touching Horizon's renderer lifecycle.

It deliberately does not load Horizon or claim to render a Horizon HudBatch. It owns one Android GLSurfaceView, requests an OpenGL ES 2.0 context, recreates GL resources from onSurfaceCreated after context loss, handles Activity pause/resume through GLSurfaceView, and renders an atlas-textured synthetic HUD using one VBO and one draw call.

## What the screen demonstrates

- GLES2 context and Android window surface creation.
- GLSL ES 1.00 vertex and fragment shader compile/link.
- 2×2 RGBA atlas upload and sampling.
- Interleaved position/UV/color vertex attributes.
- 18 vertices in one VBO and one glDrawArrays call.
- Alpha blending and scissor state.
- Repeated frames and GL error reporting.
- Context/resource recreation when Android recreates the EGL context.

## Build

From this directory:

```sh
gradle --no-daemon --stacktrace assembleDebug
```

APK: `app/build/outputs/apk/debug/app-debug.apk`.

The next stage, after the user confirms the app renders and survives leaving/re-entering it, is to replace the synthetic vertex fixture with data converted from Horizon's actual HudBatch and HudAtlas. That integration is not part of this prototype.
