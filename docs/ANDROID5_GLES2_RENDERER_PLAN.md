# Android 5 GLES2 renderer plan — Horizon 0.7.8

Status: design/audit only. No GLES2 renderer has been implemented yet.

## Fixed target and evidence

- Horizon source: `horizon-rust/horizon`, commit `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29` (0.7.8).
- Device: Philips QM152E, Android 5.1 / API 22, ARMv7, Mali-450 MP, 1 GB RAM, 1280x720.
- Device probe: OpenGL ES 2.0, GLSL ES 1.00; GLES 3 EGL config selection failed with `0x3004`.
- Reported limits: max texture size 4096, fragment texture units 8, vertex texture image units 0, vertex attributes 16, varying vectors 12.
- RGBA8 framebuffer: complete.
- The extended probe (commit `5b056d294791edad4819cfdb9e4cc45f0c8df56f`) additionally tests depth16/stencil8 attachments, basic GLSL ES 1.00 program link and RGB/RGBA texture uploads. These results still require a run on the TV.

## Source architecture audit

The pinned `horizon-client-render` crate is not a thin renderer wrapper. Its public module surface includes world data and presentation systems such as `mesher`, `chunk`, `light`, `lod`, `particles`, `font`, `hud`, `screen_draw`, `graphics_ui`, `post_chain`, `skins`, `block_entity_render` and `gfx`.

The central GPU implementation is `src/gfx.rs` (about 441 KB in this revision). It owns terrain, sky, block outline and HUD GPU rendering and is built around wgpu types: `Device`, `Queue`, `TextureView`, `BindGroupLayout`, `RenderPipeline`, command encoders and WGSL shader modules. The crate directly depends on wgpu 30.0.1 with GLES enabled on non-macOS targets.

The existing `gfx/terrain_source.rs` is a GLES lowering path for the current wgpu backend. Its comments explicitly target GLES 3.0 constraints and it still emits WGSL for Naga/wgpu; it is not a GLSL ES 1.00 backend. `gfx/entity_source.rs` similarly rewrites selected WGSL constructs for GLES linker quirks, not for GLES2.

The UI drawing layer (`screen_draw.rs`) already represents many screen operations as CPU-side draw commands (sprite, blit, fill, highlight, text, scissor and tooltip). This is a useful conceptual boundary for a future GLES2 UI renderer, but it does not currently provide a standalone GLES implementation.

## Confirmed and unconfirmed device capabilities

Confirmed by the first TV report:
- GLES2 context and GLSL ES 1.00 are available.
- RGBA8 color texture attached to an FBO is complete.
- At least 8 fragment texture units and 16 vertex attributes are reported.
- No vertex texture fetch units are available.
- The attempted GLES3 EGL config was rejected.

Still awaiting the extended probe on the TV:
- Depth16 renderbuffer attachment completeness.
- Stencil8 renderbuffer attachment completeness.
- Basic vertex/fragment shader compile and link.
- RGB and RGBA texture upload error-free status.

Do not infer depth/stencil support from RGBA8 color FBO completeness.

## Recommended implementation strategy

Do not try to make wgpu 30.0.1's GLES backend pretend to be GLES2. Its GLES path is a GLES3 backend, and the current renderer's WGSL pipeline is not a direct GLSL ES 1.00 execution path.

Keep the existing wgpu renderer intact for supported platforms. Add an isolated Android-only GLES2 renderer crate/module, behind an explicit build feature and target cfg, rather than scattering GLES2 conditionals through the existing wgpu code.

### Phase A — minimal renderer proof

1. Create EGL/GLES2 context and lifecycle tied to the Android surface.
2. Implement a tiny GLSL ES 1.00 program, vertex/index buffers and a textured quad.
3. Render to the default framebuffer and to an RGBA8 FBO.
4. Add depth testing only after the extended probe confirms Depth16.
5. Add alpha blending and scissor tests.
6. Measure CPU frame time, GPU frame time where available, memory and stability on the TV.

Acceptance: stable textured quad and depth-tested geometry on the actual TV, no GL errors, repeatable surface recreation.

### Phase B — 2D/HUD path

Use the existing CPU-side screen draw concepts as a reference. Implement:
- one or a small number of 2D texture atlases;
- batched quads with position, UV and color;
- text glyph atlas and scissor rectangles;
- explicit alpha blending;
- no post-processing dependency.

Acceptance: title/menu/HUD prototype rendered on device with bounded draw calls and memory.

### Phase C — world terrain prototype

Before porting all of `gfx.rs`, define a narrow CPU-to-GPU interface that accepts already-built mesh vertices and indices. Then implement:
- GLES2-compatible vertex layout (no integer vertex attributes; encode discrete values as normalized floats/bytes where safe);
- atlas-based texture selection (2D atlas or multiple 2D textures; no texture arrays);
- GLSL ES 1.00 vertex/fragment shader pair;
- depth buffer after validation;
- opaque terrain first, then cutout and translucent passes.

Keep meshing, chunk data and world logic on CPU where possible. Avoid porting shader packs, HDR, OIT, GPU occlusion, frame generation, compute-like passes and advanced post effects in the first milestone.

Acceptance: one chunk with opaque and cutout blocks, correct depth, texture selection and basic lighting.

### Phase D — incremental parity

Add entities, block entities, particles, weather, sky, hand, fonts, GUI and translucent materials one subsystem at a time. Each stage needs a small deterministic scene and visual comparison against the existing renderer.

## Performance constraints for the 1 GB TV

- Start with 640x360 internal resolution, upscale to 1280x720; compare quality and frame time before increasing.
- Start with render distance 2 chunks; expose a low cap rather than inheriting desktop defaults.
- Use a strict texture/atlas memory budget and release staging buffers after upload.
- Limit worker threads based on available cores and memory; do not assume more workers improve performance.
- Prefer static/dynamic VBO reuse and batch draws; avoid per-frame allocation.
- Keep optional visual systems disabled initially: HDR, shader packs, distant terrain, GPU occlusion, frame generation, expensive post-processing and high-cost transparency.
- Record peak process RSS/PSS and frame-time percentiles on hardware; CI build success is not performance evidence.

## Risks and open questions

- Android API22 native compatibility is separate from GLES2 compatibility; the existing API22 APK is a packaging/launch diagnostic only.
- Audio still needs a legacy Android backend (AAudio is not available on API22).
- Depth16/stencil8 and shader/texture tests have not yet been confirmed on the TV.
- GLES2 has no vertex texture sampling in this device's reported limits; any current rendering data sourced from vertex-stage texture reads must be moved to CPU or fragment stage.
- The source renderer contains extensive advanced features; a full parity port is a substantial project. The first realistic goal is a minimal world renderer, not full feature parity.

## Pinned source references

- Horizon 0.7.8 renderer crate: https://github.com/horizon-rust/horizon/tree/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render
- Main GPU renderer: https://github.com/horizon-rust/horizon/blob/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render/src/gfx.rs
- GLES terrain lowering (GLES3/WGSL, not GLES2): https://github.com/horizon-rust/horizon/blob/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render/src/gfx/terrain_source.rs
- CPU-side GUI draw commands: https://github.com/horizon-rust/horizon/blob/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render/src/screen_draw.rs
