# Horizon 0.7.8 GLES2 HUD adapter

## Status

The pinned-source patch installer is `patches/horizon-078/gles2-hud-adapter.py`. It adds
`horizon_client_render::gles2_hud` to the exact Horizon 0.7.8 source tree during the API-22
build. The adapter is CPU-only: it does not create an EGL context, issue GL calls, replace
`HudRenderer`, or make the game render on GLES2.

The adapter deliberately keeps Horizon's existing `HudBatch` as the source of truth. It
exports the batch, atlas pixels, glint texture, glyph patches and ordered draw runs through a
GLES2-friendly data layout. The existing wgpu renderer remains unchanged.

## Vertex data contract

`Gles2HudVertex` is `#[repr(C)]`, 36 bytes, with this layout:

| Byte offset | Attribute | GLES2 format |
|---:|---|---|
| 0 | position, 2 floats | `GL_FLOAT`, 2 |
| 8 | UV, 2 floats | `GL_FLOAT`, 2 |
| 16 | RGBA color, 4 bytes | `GL_UNSIGNED_BYTE`, normalized |
| 20 | depth, 1 float | `GL_FLOAT`, 1 |
| 24 | glint UV, 2 floats | `GL_FLOAT`, 2 |
| 32 | text marker, 1 float | `GL_FLOAT`, 1 |

The source color is packed as AABBGGRR. The adapter uses `to_le_bytes()`, yielding RGBA
bytes on the target ARM little-endian ABI. The text marker is converted to 0.0 or 1.0 because
GLES2 has no integer vertex attributes.

GUI positions are in top-left-origin GUI pixels. The vertex shader must apply
`batch.scale` and convert to clip coordinates using the actual framebuffer dimensions.
The adapter does not pre-transform positions, so the same batch remains usable for different
surface sizes.

## Frame and texture data

The frame exposes:

- Atlas RGBA pixels and dimensions from `HudAtlas`.
- The independent glint RGBA texture and dimensions.
- `HudBatch::patches`, preserving x/y/width/height and RGBA data for glyph uploads.
- GUI width, height, scale, glint alpha, game time, blur boundary and End Portal parameters.
- Ordered draw runs, each with first vertex, vertex count and the original blend mode.

A GLES2 consumer must apply glyph patches with `glTexSubImage2D` before drawing that frame.
It must also respect the texture row orientation: the existing probe's atlas UV test validates
the driver path, but the production atlas upload/UV convention still needs an explicit
comparison against Horizon's source atlas.

For a depth run, `scissor_top_left` retains Horizon's top-left pixel rectangle. GLES scissor
uses a bottom-left origin, so the GL backend must convert y to
`framebuffer_height - (y + height)` at draw time.

## Blend behavior to preserve

The exact blend states in Horizon 0.7.8 `HudRenderer::new` are:

| Horizon mode | Color factors | Alpha factors |
|---|---|---|
| Alpha | SRC_ALPHA, ONE_MINUS_SRC_ALPHA | ONE, ONE_MINUS_SRC_ALPHA |
| Invert | ONE_MINUS_DST_COLOR, ONE_MINUS_SRC_COLOR | ONE, ZERO |
| Additive | ONE, ONE | ONE, ONE |
| Vignette | ZERO, ONE_MINUS_SRC_COLOR | ZERO, ONE |
| Depth | SRC_ALPHA, ONE_MINUS_SRC_ALPHA | ONE, ONE_MINUS_SRC_ALPHA |
| EndPortal | replacement (opaque) | replacement (opaque) |

Use `glBlendFuncSeparate` where the color and alpha factors differ. Depth runs additionally
need a Depth16 buffer, depth clear and scissor. EndPortal needs its own GLSL ES 1.00 fragment
implementation; it is not equivalent to an ordinary textured quad.

## Memory and texture constraints

- The TV reports a maximum texture dimension of 4096; do not inherit Horizon's 16384 desktop
  atlas cap without a device-specific override.
- Start with a 2048 maximum atlas side and measure actual pack requirements before raising it.
- The boot atlas is 1024 × 1024 and is a useful first deterministic fixture.
- If an atlas is rescaled, every glyph patch coordinate and dimension must be scaled consistently.
- Reuse a bounded VBO and grow in capped steps; do not allocate a new buffer every frame.
- The initial target should avoid blur, post-processing, MSAA and extra render targets.

## Android 5 atlas memory policy

The API-22 diagnostic patch calls
`horizon_client::android_runtime::configure_atlas_limit(2048)` at the start of
`android_main`, before the launcher builds its GUI atlas. This is intentionally earlier than
`Renderer::new`: the atlas is built before the renderer is initialized, so applying a limit
only after wgpu device creation is too late for the first atlas allocation.

Horizon 0.7.8's existing `HudAtlas::fit_mobile_atlas` repeatedly halves non-UI sprites until
the packed atlas fits the configured side limit. It preserves GUI/font/UI assets and their
logical dimensions, and retains full-size runtime slots for glyphs, maps and skins. The
2048px ceiling therefore reduces static non-UI sprite resolution and atlas memory; it does
not solve the GLES2 renderer or reduce terrain texture-array memory.

The 2048px value is a conservative starting point for the Philips 1 GiB device, not a proven
final optimum. It must be checked against the actual official/resource-pack atlas and visual
quality on the TV before being treated as final.

## Renderer integration boundary found in Horizon 0.7.8

The pinned source has one concrete wgpu renderer, not a renderer trait or interchangeable
backend:

1. Android enters through `crates/horizon-client/src/android_main.rs`.
2. The launcher creates its window and calls `Renderer::new` from
   `crates/horizon-client/src/android/launcher.rs`.
3. Game/loading paths call `build_renderer` in `crates/horizon-client/src/game.rs`.
4. `Renderer::new_with_context` in `crates/horizon-client-render/src/gfx.rs` calls
   `open_device`, creates a wgpu surface, requests a wgpu adapter/device, and builds the
   world and HUD pipelines.
5. Per-frame drawing goes through `Renderer::render` -> `draw_frame`; HUD preparation,
   glyph patch uploads and draw ranges are currently wgpu commands.

The Android lifecycle is also explicit: `Game::suspended` and the launcher's
`suspended` callback call `Renderer::suspend_surface`; their `resumed` callbacks call
`Renderer::recreate_surface` and resize. A GLES2 backend must release EGL surface/context
resources on the same suspension path and recreate them only after the new native window is
available.

This means a GLES2 HUD object cannot simply be added alongside the current `HudRenderer`:
the wgpu device and surface are created before HUD rendering, and the same window cannot be
owned by two competing presentation backends. The Android GLES2 path must branch before
`open_device` and bypass wgpu surface/device creation altogether.

The boundary is broader than the HUD. `gfx.rs` directly uses wgpu for terrain, entities,
depth targets, sky, particles, post-processing, screenshots, uploads and presentation. A
playable GLES2 client therefore needs a separate renderer backend with a staged feature set,
not only a replacement HUD shader. The first safe integration milestone should be a
GLES2-owned window/context plus a real boot/loading HUD frame; terrain and gameplay passes
must remain explicitly unavailable until their own GLES2 implementations exist.

## Explicitly not implemented yet

- EGL/GLES2 context ownership inside Horizon's runtime.
- Calling this adapter from the real frame submission path.
- GLES2 VBO upload/draw calls consuming this exact frame.
- Atlas animation frame updates and full glyph upload validation.
- Resource-pack text shaders, menu blur, EndPortal shader and full depth-run parity.
- A GLES2 terrain/world renderer or a complete playable client.

The existing NativeActivity test app validates EGL/GLES2 on the Philips independently. Its four
colored squares are not rendered from Horizon's `HudBatch`.

## CI validation

The API-22 diagnostic workflow installs this adapter into the pinned source and runs
`cargo test -p horizon-client-render --lib gles2_hud::tests` before the Android build.
That validates source compatibility and CPU conversion only; it does not validate GL drawing
or game integration.


## Launcher and renderer integration inventory

A second source pass pinned the first integration boundary to the exact Android call sites:

- `android_main.rs` creates `Launcher` and runs the single winit `EventLoop`.
- `android/launcher.rs` stores `renderer: Option<Renderer>`. Its `open` method creates the native window, calls `game::loading_renderer`, builds the GUI atlas and calls `set_loading_ui`.
- The launcher's `RedrawRequested` event enters `draw_pages`; the launcher therefore needs a renderer capable of drawing the actual launcher HUD, not merely clearing the screen.
- The launcher's Android `suspended` callback calls `Renderer::suspend_surface`; `resumed` reaches `open`, which calls `recreate_surface` for an existing renderer.
- `game.rs` also creates the same concrete `Renderer` for loading and gameplay. Its loading frames call `renderer.render(&loading_frame(&hud))`.
- `horizon-client-render` forbids unsafe code, while the Android host crate is the appropriate place to isolate any EGL/GLES2 FFI.

This rules out a safe one-file swap in `android_main.rs`. The first integration refactor must introduce a backend boundary shared by launcher and game, then provide a GLES2 implementation for the launcher's HUD frame. It must preserve the single winit-owned Android window and mirror the existing suspend/resume lifecycle. Until that seam exists, do not create a second EGL surface alongside wgpu.

### CI guard

`tools/check_android5_renderer_boundary.py` checks these exact upstream integration markers before applying the diagnostic patches. It is a source-drift guard, not a renderer implementation or a device test. If Horizon changes its Android lifecycle or renderer ownership, CI stops so the GLES2 integration plan can be reviewed before producing another diagnostic APK.


## Native window handoff for the GLES2 host

The pinned Horizon Android host uses winit 0.30.13 with the `rwh_06` feature enabled by its default feature set. The Android raw-window-handle 0.6 contract exposes the existing native window as:

- `window.window_handle()?.as_raw()`
- Match `RawWindowHandle::AndroidNdk(handle)`.
- Read `handle.a_native_window`, a non-null pointer to the `ANativeWindow`.

This is the appropriate handoff for an EGL implementation hosted in `horizon-android`: winit remains the owner of the Activity/window lifecycle, while the selected renderer creates its EGL display/config/context/window surface from that same native window. The raw pointer must only be used while the corresponding winit window is valid and resumed; do not cache it across Android surface destruction.

This does **not** permit EGL to coexist with the current wgpu surface on that window. The Android renderer selection must happen before `game::loading_renderer` calls `Renderer::new`. On suspend, the GLES2 backend must unbind and destroy its EGL window surface (and context/display resources according to its ownership policy); on resume it must obtain the current native window again and recreate the surface before drawing.

The first backend seam should be deliberately small:

1. A host-side Android renderer enum/facade selected before launcher renderer creation.
2. A wgpu variant preserving the current behavior unchanged.
3. A GLES2 variant owning EGL/GLES2 resources in the Android host crate, not in the `forbid(unsafe_code)` render crate.
4. A first GLES2 draw path consuming the existing `Gles2HudFrame` for a deterministic boot/loading frame.
5. Explicitly return an unsupported-renderer result for world/gameplay until terrain, entities, sky, particles, depth and presentation passes are implemented.

The raw handle provides the native window; it does not solve the larger `Renderer` API coupling. In particular, existing launcher/game code reads renderer configuration and GPU/HUD fields directly. Those call sites need to move behind backend-neutral methods or a narrower shared frame interface before the GLES2 variant can be substituted safely.


### Concrete API surface that must be separated

The pinned Android launcher page renderer is not limited to `render`: `draw_pages_with` reads `Renderer.config`, checks `Renderer.suspended`, and calls `Renderer.render`. The game runtime has a much wider direct dependency on the concrete type. The source inventory includes:

- Frame submission and lifecycle: `render`, `resize`, `suspend_surface`, `recreate_surface`, `release_surface`.
- GPU-owned state: `device`, `queue`, `hud`, `info`, `config`.
- World streaming and GPU uploads: `upload`, `upload_lods`, `queue_lods`, `clear_lods`, `remove_columns`, `set_world_assets`, `start_occlusion`.
- Timing and telemetry: `gpu_times`, `last_wait`, `set_pass_timing`, `set_detailed_gpu_timing`, `drawn_sections`, `lod_detail_revision`.
- Screenshots and menu effects: `screenshot`, `has_cached_menu_blur`, `invalidate_menu_blur`.

These are not all methods a GLES2 renderer should implement. They demonstrate why a backend trait must be split by responsibility instead of mechanically mirroring every wgpu field. The first launcher-only interface can be narrow (framebuffer size, suspended state, atlas/UI upload, HUD frame submission and lifecycle). The gameplay renderer will need separate world-streaming, resource-upload, depth and pass interfaces later. Do not expose `wgpu::Device` or `wgpu::Queue` through the backend-neutral interface.
