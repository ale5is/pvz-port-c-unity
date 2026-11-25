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
