# Android 5 GLES2 renderer audit — 2026-11-23

## Scope and fixed source

- Target branch: `port/android-5-low-end`.
- Horizon source: 0.7.8, commit `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`.
- Device: Philips QM152E, Android 5.1/API 22, ARMv7, Mali-450 MP, GLES 2.0, 1280x720.
- This document is an implementation audit. It does not claim that a Horizon GLES2 renderer exists yet.

## Device evidence supplied from the TV

The expanded Pavo GLES2 probe report supplied by the user records:

| Test / property | Result |
|---|---|
| GL vendor / renderer | ARM / Mali-450 MP |
| GL / GLSL | OpenGL ES 2.0 / GLSL ES 1.00 |
| Max texture size | 4096 |
| Fragment texture units | 8 |
| Vertex attributes | 16 |
| Varying vectors | 12 |
| Default framebuffer depth | 24 bits |
| GLSL ES 1.00 compile + link | PASS |
| RGBA 2x2 texture upload | PASS |
| VBO + glBufferData | PASS |
| RGBA8 + Depth16 FBO | PASS |
| Actual depth ordering + glReadPixels | PASS |
| Readback pixel | 0,255,0,255 (green) |
| Android app memory class / Java heap max | 192 MB / 192 MB |

Interpretation:
- The TV can execute the basic GLES2 operations required for a first independent renderer.
- The green readback verifies the depth comparison in the tested RGBA8 + Depth16 FBO, not merely attachment completeness.
- The report does not establish Horizon shader compatibility, terrain rendering, stencil attachment completeness, RGB upload behavior in this particular run, performance, or stability under sustained load.
- The 192 MB app heap limit is a practical constraint. The device's physical 1 GB RAM must not be treated as 1 GB available to the process.

## Source audit findings

### 1. The current GPU renderer cannot be reused as a GLES2 renderer

The pinned `horizon-client-render` crate depends on wgpu 30.0.1. Its central `gfx.rs` uses wgpu devices, queues, bind groups, render pipelines, shader modules, command encoders and render passes. The existing `terrain_source.rs` is a WGSL lowering path for GLES3/wgpu, not a GLSL ES 1.00 compiler or backend.

The renderer's terrain vertex declaration is packed for wgpu:
- position: Float32x3
- UV: Uint16x2
- layer: Uint16
- light: Uint8x2
- color: Unorm8x4

The terrain WGSL entry points use integer inputs and flat integer varyings. GLES2 has no integer vertex attribute type or flat interpolation qualifier in core GLSL ES 1.00. A GLES2 path must define a separate vertex format and shader interface; simply translating the current WGSL or changing the EGL version is not sufficient.

### 2. Terrain texture arrays need a different representation

The terrain shader declares several `texture_2d_array` bindings and selects among them. Texture arrays are not part of GLES2. The GLES2 path must use ordinary `GL_TEXTURE_2D` textures.

Recommended first implementation:
- Use one bounded 2D atlas for the initial terrain test.
- Convert the discrete sprite/layer identifier on the CPU into atlas UV rectangles, or into a small texture-page + UV representation.
- Keep atlas page selection out of unsupported integer/flat shader inputs.
- Do not attempt to reproduce the full multi-array texture paging system in the first milestone.
- Respect the device's 4096 maximum texture dimension. The existing HUD atlas constant allows 16384 and must not be reused as-is on this target.

The existing atlas packing and sprite metadata can be reused as CPU-side source data only after checking that atlas dimensions, mip levels, padding and animation updates fit the GLES2 limits.

### 3. HUD is the best first Horizon subsystem

The CPU-side `HudBatch` is already a useful boundary:
- A frame is collected as `Vec<HudVertex>`.
- It carries blend runs, glyph patches, screen dimensions and GUI scale.
- `screen_draw::Draw` represents sprite, blit, fill, highlight, text, scissor and tooltip commands.

However, the current `HudRenderer` is still wgpu-specific: it owns wgpu pipelines, bind groups, buffers, textures and samplers. Therefore, reuse the CPU batch/data model, not the current GPU renderer.

The HUD path is a better first integration target than terrain because it already uses a 2D atlas and screen-space geometry. The first GLES2 HUD prototype should support only:
1. solid-color and atlas-textured quads;
2. alpha blending;
3. scissor rectangles;
4. a bounded dynamic vertex buffer;
5. a small glyph atlas or the existing atlas glyph patches.

Defer invert/additive/vignette/end-portal blend modes, resource-pack text shaders, depth-isolated overlays and advanced effects until the basic HUD is stable.

### 4. Depth is now validated, but stencil is not

The supplied probe verifies Depth16 attachment and actual depth testing. It does not run a Stencil8 or packed depth-stencil framebuffer test. Do not enable stencil-dependent effects based solely on the extension string. The initial renderer should use Depth16 and avoid stencil.

### 5. Memory and resolution

The device reports a 192 MB Java memory class and 192 MB maximum Java heap. This does not expose total native/GPU memory available, but it is enough to justify conservative initial limits.

Initial prototype defaults:
- 640x360 internal render target, scaled to the 1280x720 display only if the target surface path requires it.
- One small atlas page, dimension capped at 2048 initially (not the 4096 hardware maximum).
- Reuse vertex buffers; grow capacity in bounded steps and avoid per-frame allocations.
- No post-processing, multisample render targets, shader packs, or large transient textures.
- Log frame time, Java heap, native heap/PSS where available, and GL errors during sustained testing.

These are starting limits for measurement, not claims of final performance.

## Recommended implementation sequence

### Milestone A — GLES2 HUD proof

Implement an isolated Android-only GLES2 renderer module with no wgpu dependency in its render path. Reuse Horizon's CPU-side HUD batch representation through a narrow adapter. The prototype must render:
- a solid fill;
- a textured quad from a 2D atlas;
- alpha transparency;
- a clipped/scissored region;
- a small batch of quads using one VBO upload.

Keep the wgpu implementation unchanged for every other platform. Do not add GLES2 conditionals throughout `gfx.rs`.

Acceptance on the TV:
- stable rendering after surface pause/resume and recreation;
- no GL errors;
- correct texture orientation, alpha and scissor;
- bounded memory and repeatable frame-time measurements.

### Milestone B — terrain geometry proof

Only after Milestone A passes, add a separate terrain shader and vertex format:
- CPU-convert packed integer attributes into GLES2-compatible normalized floats/bytes;
- resolve sprite layer IDs to 2D atlas UVs before upload;
- render one opaque quad/chunk with Depth16;
- then add cutout alpha test and basic lighting.

Do not port sky, entities, particles, translucent terrain, shader packs or post effects in this milestone.

### Milestone C — parity and low-end tuning

Add terrain batching, chunk limits, animation updates and remaining passes incrementally. Measure on the actual TV at each step. CI compilation is necessary but is not device validation.

## Explicit non-goals for the first GLES2 milestone

- Making wgpu 30.0.1's GLES3 backend operate on GLES2.
- Translating the entire 441 KB `gfx.rs` in one pass.
- Reproducing texture arrays, integer varyings, storage buffers, compute-like passes or all shader-pack effects.
- Claiming game compatibility from the capability probe alone.

## Pinned source references

- Horizon 0.7.8 renderer crate: https://github.com/horizon-rust/horizon/tree/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render
- GPU renderer: https://github.com/horizon-rust/horizon/blob/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render/src/gfx.rs
- Terrain shader: https://github.com/horizon-rust/horizon/blob/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render/src/terrain.wgsl
- HUD CPU batch and wgpu renderer: https://github.com/horizon-rust/horizon/blob/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render/src/hud.rs
- Screen draw command model: https://github.com/horizon-rust/horizon/blob/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render/src/screen_draw.rs
- wgpu 30.0.1 GLES backend: https://github.com/gfx-rs/wgpu/blob/v30.0.1/wgpu-hal/src/gles/mod.rs
- wgpu 30.0.1 EGL context creation: https://github.com/gfx-rs/wgpu/blob/v30.0.1/wgpu-hal/src/gles/egl.rs
