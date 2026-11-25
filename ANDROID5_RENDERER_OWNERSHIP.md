# Android 5 renderer ownership and surface lifecycle

This note records the ownership boundary in the pinned Horizon 0.7.8 source. It is an architectural constraint for the GLES2 port, not a claim that GLES2 is already integrated.

## Current wgpu ownership

In `crates/horizon-client-render/src/gfx.rs`, `Renderer` owns the complete wgpu graph:

- `Instance`, `Adapter`, `Device`, and `Queue`;
- the live `Surface`, an optional spare surface, and surface configuration;
- pipelines, buffers, textures, frame arenas, upload queues, and presentation state;
- device-loss state and frame-in-flight bookkeeping.

The renderer is not merely a draw function. It creates GPU resources, submits uploads even when a frame is skipped, acquires/presents a surface texture, recovers lost/outdated surfaces, and can temporarily render screenshots offscreen.

## Existing replacement lifecycle

The current Android renderer replacement path has two distinct execution domains:

1. The event/window thread owns the live native window surface. It polls the old device, releases/suspends the surface, and later activates the replacement.
2. A background worker prepares a replacement renderer using a `ReplacementContext`. `background_context()` clones the instance/adapter/device/queue and configuration but deliberately carries no live surface.
3. On successful handoff, `activate_replacement()` transfers/recreates the surface and resizes it for the current window.
4. On Android resume, `recreate_surface()` creates a fresh surface. The Android implementation intentionally drops a spare surface rather than reusing a surface tied to a destroyed native window.

This is deliberate lifecycle behavior. A port must not move EGL window-surface creation, destruction, or presentation onto a worker merely because GPU resources are prepared there.

## Remaining game-side wgpu ownership leaks

After the current `renderer-hud-boundary.py` patch is applied, the game still has exactly three direct references that cross the renderer boundary:

- `previous.device.poll(wgpu::PollType::wait_indefinitely())`: waits for the previous renderer before background preparation.
- `previous.background_context()`: obtains the resource-sharing context for the worker.
- `r.device.poll(wgpu::PollType::wait_indefinitely())`: waits for the prepared renderer before publishing it.

These are intentionally inventoried by `tools/check_android5_renderer_ownership.py`. They are not made into superficial accessors. Their replacement requires an explicit lifecycle API and a decision about whether resource preparation can share a device/context.

## GLES2 backend contract

The GLES2 implementation must own its own graphics objects and lifecycle:

- EGL display/config/context and GLES2 capabilities belong to the GLES2 backend.
- An EGL window surface is tied to the current Android native window and is only created, resized, presented, and destroyed on the thread/domain allowed by the Android window lifecycle.
- Background preparation may build CPU-side meshes, packed atlas data, shader sources, and upload descriptions. It must not touch a window surface or issue GLES calls without a current context on the owning thread.
- Resume creates a new EGL window surface for the new native window; it must not reuse an EGLSurface associated with a destroyed ANativeWindow.
- Context loss is distinct from surface loss. If Android destroys only the window surface, retain/reuse the context only when the platform confirms it remains valid; otherwise rebuild GPU resources from retained CPU-side data.
- Frame submission, resource upload, and presentation need explicit ownership and ordering. A generic `upload/render` trait is insufficient while it hides context affinity, pending uploads, surface loss, and in-flight work.

The existing wgpu renderer remains the PC renderer. The Android GLES2 backend should be a separate implementation behind a narrow, backend-neutral game-facing contract; do not replace wgpu types with fake GLES2 equivalents.

## Required next design sequence

1. Define a CPU-side prepared-renderer payload that can cross to a worker without carrying GPU handles.
2. Define event-thread lifecycle operations: attach native window, suspend/detach surface, resume/recreate surface, resize, and present.
3. Define context-thread operations: create context, upload prepared resources, execute frame, drain/delete resources, and recover context loss.
4. Decide whether Android uses one persistent GL thread or a strictly event-thread-owned context. Enforce that choice in code and tests.
5. Only then migrate game call sites and connect the existing GLES2 terrain/HUD prototypes to real Horizon mesh and atlas data.

## Verification boundary

The ownership audit checks the pinned source after patch application. Passing that static audit only proves that the known wgpu leaks have not silently multiplied; it does not prove compilation, EGL correctness, GLES2 rendering, APK installability, or operation on the Philips QM152E.
