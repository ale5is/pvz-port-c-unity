# Android 5 GLES2 HUD adapter — implementation boundary

Target: Horizon 0.7.8 at `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`, Philips QM152E (API 22, ARMv7, Mali-450 MP, GLES2).

Status: source-level adapter design. The hardware probes pass, but no Horizon GLES2 renderer or game integration is claimed by this document.

## Reusable CPU interface

The pinned source already exposes a useful renderer-independent boundary in `horizon-client-render`:

- `HudBatch.vertices: Vec<HudVertex>` contains screen-space triangles (six vertices per quad).
- `HudVertex` contains `pos: [f32; 2]`, `uv: [f32; 2]`, packed RGBA `color: u32`, `depth: f32`, `glint_uv`, and `text`.
- `HudBatch::runs()` returns ordered vertex ranges and their `Blend` state.
- `HudBatch.patches` contains dynamic glyph-atlas updates to apply before drawing.
- `HudBatch.scale`, `width`, and `height` define the GUI coordinate system.
- `HudAtlas` owns the RGBA pixels, dimensions, and normalized sprite rectangles.
- `screen_draw::Draw` is the higher-level menu command model; `screen_draw::paint` already lowers it into a `HudBatch`.

The GLES2 path should consume these CPU values. It must not call, wrap, or attempt to reuse the wgpu-specific `HudRenderer`.

## First GLES2 vertex interface

Use a compact dedicated vertex structure rather than passing `HudVertex` as an opaque GPU layout:

| Attribute | GLES2 representation | Source |
|---|---|---|
| Position | 2 × GL_FLOAT | `HudVertex.pos` |
| UV | 2 × GL_FLOAT | `HudVertex.uv` |
| Color | 4 × GL_UNSIGNED_BYTE, normalized | four in-memory bytes of `HudVertex.color` |
| Depth | 1 × GL_FLOAT | `HudVertex.depth` (initially reserved; enable only in the depth-run stage) |

Ignore `glint_uv` and `text` in the first pass. They belong to later feature stages. Keep the CPU vertex order unchanged: the batch is already triangulated.

The vertex shader maps GUI pixels to clip coordinates using the batch's physical target size and GUI scale. The fragment shader samples one `sampler2D` atlas and multiplies by vertex color. Verify the V coordinate convention against the actual atlas upload before accepting the first screenshot; the standalone quadrant probe confirms UV interpolation, not Horizon's PNG row convention.

## Initial supported run states

- `Blend::Alpha`: enable `GL_BLEND`, use `GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA`.
- Default alpha runs: same state.
- Ordered adjacent ranges: preserve their order and issue one draw call per contiguous run; do not reorder transparent UI.
- Atlas glyph patches: upload with `glTexSubImage2D` before drawing, after validating bounds.

The device probe now confirms alpha blending, scissor, batched quads and atlas UV quadrants on the actual TV. Those results validate the GL operations, not this adapter.

## Explicitly deferred

- `Blend::Invert`, `Additive`, `Vignette`, and `EndPortal`: each needs its own shader/blend semantics and deterministic pixel test.
- `Blend::Depth([x,y,w,h])`: requires a depth-isolated pass and coordinate-correct scissor. Depth16 is validated on device, but the Horizon run semantics still need to be reproduced.
- Resource-pack text shaders, glint UVs, inline player heads/skins, map/trim/pack icon slots, animation uploads and all non-HUD world rendering.
- Stencil: no Stencil8 functional test has been run.

## Atlas and memory guardrails

- The hardware maximum texture dimension is 4096; do not inherit `HudAtlas::MAX_ATLAS_SIZE = 16384`.
- Start with a 2048 × 2048 maximum atlas allocation, then measure real resource-pack dimensions and memory before choosing a final policy.
- The boot atlas is 1024 × 1024 and is a useful first end-to-end HUD fixture.
- If an atlas is rescaled, keep normalized sprite UV rectangles unchanged, but scale the pixel buffer and every later glyph-patch coordinate/dimension consistently. Do not scale only the initial texture and leave patch coordinates untouched.
- Reuse a bounded VBO. Grow in capped steps; do not allocate a new VBO every frame.
- Keep the first target free of blur, post-processing, MSAA and additional render targets unless a later feature explicitly requires them.

## Integration constraints

The Android 5 workflow currently builds a diagnostic APK by cloning the pinned source and applying `tools/android5_patch.py`. A future GLES2 implementation must therefore be applied reproducibly by that patch/build path or by a pinned source patch. Do not edit only a local copy that CI will not build.

The current game renderer owns a wgpu instance, surface, device, queue, render targets and many world passes. A GLES2 HUD module alone cannot be selected by changing the manifest: the Android render lifecycle must provide a real GLES2 context and surface, and the frame path must not still require the wgpu GLES3 device. Before wiring the HUD into the game, establish that context/surface ownership and the fallback boundary. Do not create two unrelated contexts competing for the same Android window surface.

## Next implementation gate

1. Add an isolated Android-only GLES2 context/surface owner with pause/resume and surface-recreation handling.
2. Render the existing `HudAtlas::boot()` and a synthetic `HudBatch` to the actual window surface using the dedicated vertex/shader interface.
3. Add pixel/visual checks for top-left origin, color packing, alpha, scissor and repeated frames.
4. Only then adapt the real `HudBatch` and dynamic glyph patches.
5. Keep wgpu paths unchanged on other platforms. Do not claim a playable Horizon port until the world renderer is also implemented.

