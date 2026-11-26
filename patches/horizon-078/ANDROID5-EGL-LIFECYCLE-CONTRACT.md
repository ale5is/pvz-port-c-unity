# Android 5 EGL lifecycle and native-window ownership contract

Status: native EGL owner and Rust runtime now expose suspend/resume plus context-generation detection. Horizon Launcher/Game ownership transfer and call-site integration remain unimplemented.

- Upstream: `starling-client/starling`
- Revision: `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`
- Target: Android 5.1 / API 22 / ARMv7 / GLES 2.0
- Existing owner: Android `Launcher` before game start, then `Game`; both currently own the concrete wgpu `Renderer`.

## Source behavior that must be preserved

The Android launcher creates its native `Window`, prepares display scaling, and calls `game::loading_renderer(...)` before assigning the renderer to `self.renderer`. The loading renderer is therefore a real rendering owner, not just a static bootstrap screen.

When the launcher resumes with an existing window/renderer, it prepares the display, calls `recreate_surface()`, then resizes to the current window dimensions. When the game is active, `Game::suspended` marks the surface lost, releases touch/input state, pauses the game as appropriate, and calls `suspend_surface()`. `Game::resumed` clears the lost flag, synchronizes pause state, prepares display scaling, resizes, recreates the surface, resizes again using the current window size, and resets frame timing. `Game::exiting` calls shutdown.

A GLES2 backend must serve both launcher and game paths. Replacing only the Game renderer leaves the installer/loading UI on the old backend. Creating an EGL context independently while wgpu still owns/presents the same native window is not a valid coexistence strategy.

## Required ownership model

One Android render owner controls the complete EGL tuple:

- `EGLDisplay`
- `EGLConfig` selected with an ES 2 renderable bit
- `EGLContext` created with client version 2
- `EGLSurface` created from the current `ANativeWindow`
- GLES objects created in that context (programs, textures, buffers, framebuffers)

Only the owner thread may create, make-current, resize/recreate, present, or destroy these objects. The native window must remain acquired for as long as an EGL window surface references it. The owner must release its acquired native-window reference after destroying the EGL surface that uses it. The owner must not call GL deletion functions after the context has been destroyed or while another context is current.

EGL/GL FFI and unsafe calls belong in the isolated Android native/FFI layer. Do not weaken `#![forbid(unsafe_code)]` in `horizon-client-render` to put EGL calls there.

## Lifecycle state machine

| State | Meaning | Allowed operations |
|---|---|---|
| `Detached` | No native window or EGL surface is owned | Initialize display/config/context; attach a valid window |
| `Attached(g)` | Window and EGL surface belong to generation `g`; context is current on owner thread | Resize viewport, draw, swap |
| `Suspended(g)` | Generation `g` is no longer presentable; context/resources may be retained only if EGL permits | Stop frame submission; detach current surface if required |
| `Recreating(g+1)` | Old surface is invalid or detached; new native window/surface is being installed | Recreate surface, make current, restore viewport |
| `ShuttingDown` | No new frame or attach requests accepted | Destroy surface, GL resources, context, display in dependency order |
| `Destroyed` | All EGL and native-window references released | No operations |

The generation is a monotonically increasing unsigned integer. Increment it whenever a new native window is attached or an old window is replaced. Every queued resize/draw/present request carries the generation it was created against. Reject stale requests whose generation differs from the active generation. This prevents delayed work from presenting to a destroyed or replaced Android window.

A surface-only recreation may retain the context and GL resources only if the EGL implementation and context lifecycle permit it. If context loss is reported, rebuild every GL object from CPU-owned source data; never assume texture or buffer names survive.

The native owner now exposes a context-generation counter through `pavo_egl_context_generation`. It increments when `pavo_egl_resume` has to recreate an `EGL_CONTEXT_LOST` context. The Rust `Runtime::resume` compares this value: unchanged means surface-only resume and existing resources remain valid; changed means the old GLES2 backend is replaced and `is_initialized()` becomes false. The runtime now retains the initial sprite-atlas pixels, lightmap pixels, and uploaded section vertex/range payloads in CPU memory. On a changed context generation it creates a fresh GLES2 backend, reinitializes textures, replays every cached section, and only then marks itself initialized. `remove_column` and `clear_sections` keep the CPU cache in sync. This closes the prototype runtime's mesh/texture-loss gap; animated atlas updates and live Game call-site wiring are still not connected. This counter tracks GL context validity, not native-window generations.

## Event mapping

1. **Initial launcher start:** create window -> acquire native-window reference -> initialize EGL display/config/context -> create window surface -> make current -> set viewport -> create loading/UI resources -> first present.
2. **Launcher pause:** stop frame submissions -> detach current surface as required -> mark generation non-presentable. Keep the context only if it is valid and the backend's policy explicitly allows retention.
3. **Launcher resume:** verify current native window -> if replaced, increment generation and acquire new reference -> destroy old surface -> create new surface -> make current -> query actual drawable size -> set viewport -> resume frames.
4. **Game launch:** transfer the same backend owner or explicitly shut down the launcher renderer before constructing the game renderer. Do not leave two independent EGL owners presenting the same window.
5. **Game suspend/resume:** preserve Horizon's pause/input semantics, but route surface transitions through the same owner and generation checks. Resize only after the new surface is current and its drawable dimensions are known.
6. **Return to launcher:** stop game submissions and release game-only GL resources before launcher resources are restored. Reuse the owner only through an explicit ownership transfer; otherwise fully tear down and recreate.
7. **Exit:** stop submissions -> destroy EGL surface -> release native-window reference -> delete GL resources while context is current (or before context teardown) -> destroy context -> terminate display.

## Failure handling

- Treat `EGL_BAD_NATIVE_WINDOW`, `EGL_CONTEXT_LOST`, failed `eglMakeCurrent`, and failed `eglSwapBuffers` as lifecycle transitions, not as ignorable frame errors.
- On `EGL_CONTEXT_LOST`, invalidate all GPU handles and rebuild from CPU-side atlas, lightmap, mesh and shader data.
- On a failed attach/recreate, do not continue drawing against the previous generation. Keep the app paused or surface-less and report the failure to the existing Android diagnostics path.
- Do not busy-loop while detached or suspended. Frame scheduling must stop until a valid surface is attached.
- Shutdown must be idempotent: repeated pause/exit callbacks must not double-release the native window or destroy an EGL handle twice.

## Integration gates

Before wiring this owner into Horizon:

- [ ] Launcher loading renderer and world renderer both select the same Android backend before GPU resource construction.
- [ ] A single owner controls EGL and the native-window reference across launcher/game transitions.
- [ ] All lifecycle callbacks run on the documented owner thread.
- [ ] Generation checks reject stale resize, draw and present work.
- [x] Native EGL owner detects context loss and reports a monotonically increasing GL context generation.
- [x] Rust runtime recreates the backend after a generation change and blocks terrain uploads/draws until reinitialized.
- [ ] Launcher/Game callbacks invoke the runtime suspend/resume methods and restore atlas, lightmap, animations and section meshes.
- [x] Runtime context-loss recovery recreates its cached sprite atlas, lightmap and uploaded terrain sections from CPU-owned data.
- [ ] Runtime cache receives the live Game atlas/lightmap and all streaming updates; animated atlas state is still not wired.
- [ ] Desktop wgpu construction and lifecycle remain unchanged.
- [ ] An Android integration test exercises initial attach, suspend/resume, window replacement, context loss and shutdown.

Until these gates are met, the GLES2 NativeActivity prototype remains an isolated renderer experiment. It is not integrated into Horizon's launcher or game loop, and no APK should be described as playable.
