# Android 5 GLES2 backend integration contract

Status: architecture contract and implementation plan; this document does not claim that the GLES2 prototype is connected to Horizon or that the game renders on the Philips QM152E.

Pinned upstream source: `starling-client/starling` at `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`.

Target profile: Android 5.1 / API 22, armeabi-v7a, MT5861 / Mali-450 MP, OpenGL ES 2.0, GLSL ES 1.00, approximately 1 GiB RAM, 1280x720.

## Findings from the pinned source

### The real Android entry point is winit

`crates/horizon-client/src/android/launcher.rs` owns the installer's window and renderer. Its `open()` method:

1. Reuses the existing renderer when both renderer and window survive, prepares the display, recreates the surface and resizes.
2. Otherwise creates a winit `Window`, prepares the display, calls `game::loading_renderer(...)`, installs the loading UI and retains the window/renderer.

This is the launcher used by the actual application. The standalone NativeActivity in `tools/gles2-hud-prototype` is a driver/lifecycle experiment, not a replacement entry point.

### The game has two renderer construction paths

In `crates/horizon-client/src/game.rs`:

- `loading_renderer(...)` creates the lightweight loading/menu renderer.
- `build_renderer(...)` creates the world renderer from `PackResources`, `UiResources`, prepared textures and an optional previous renderer.
- Both currently return the concrete `horizon_client::gfx::Renderer` (wgpu-backed).
- The Android game lifecycle calls `suspend_surface()` when suspended and `recreate_surface()` on resume. The loading-to-world transition can also suspend a GL surface while a background worker prepares a replacement renderer.

A GLES2 backend must therefore cover both loading and world rendering, and must participate in the existing resume/suspend/resize/replacement flow. Merely adding GLES2 calls to the demo Activity does not satisfy this requirement.

### The current prototype is not a world renderer

The C++ `Gles2TerrainRenderer` accepts already-converted vertices, atlas pixels and lightmap pixels. Its current Activity supplies synthetic test textures and geometry. It does not consume Horizon's section meshes, atlas/animation data, camera matrices, fog state, world lighting or game lifecycle. Its API is a prototype boundary, not a completed adapter.

## Required ownership model

Do not put fake wgpu handles behind a GLES2 implementation. Do not move EGL objects, GL names, or context-bound resources through the existing wgpu `Renderer` fields.

Use three explicit domains:

### 1. CPU preparation domain

May run on workers. Owns only ordinary Rust/C++ memory and immutable data:

- decoded/packed atlas and lightmap pixel buffers;
- converted terrain vertex/index data;
- section coordinates, bounds and material/translucency metadata;
- camera-independent render descriptions;
- UI batches and glyph/texture upload descriptions;
- resource generation IDs and byte/count estimates.

This domain must not own EGLDisplay, EGLContext, EGLSurface, GLuint, wgpu Device/Queue/Surface, or any other GPU object. Data crossing into the render domain must have explicit ownership (move/Arc) and a defined lifetime.

### 2. Window/lifecycle domain

The winit Android application handler remains authoritative for native window events:

- attach a newly available native window;
- report pixel size and density changes;
- suspend/detach before the native window becomes invalid;
- resume/recreate presentation after the window is available again;
- request shutdown and wait for renderer acknowledgement before releasing window-owned state.

Every attach/detach/resize operation must be ordered. A stale window generation must never be used after a newer attach. Surface loss must be recoverable without discarding CPU-side world data.

### 3. GLES2 context/render domain

One serialized owner must create, make-current, use and destroy the EGL context and all GL objects. The same owner performs:

- capability query and context validation (ES 2.0, texture limits, vertex attributes, extensions);
- shader/program creation;
- texture/VBO allocation and upload;
- frame execution and presentation;
- GL object deletion;
- context-loss recovery.

No GL call may run when the context is not current on its owning thread. No GLuint may be deleted from a worker or after its context has been destroyed.

## Initial threading decision

For the first real integration, keep EGL context ownership serialized on the winit render/event thread. Perform expensive file IO, asset decoding, atlas packing and CPU mesh conversion on workers, then enqueue immutable prepared payloads. This avoids introducing a second native-window/surface owner while the lifecycle contract is still being validated.

Do not move GL work to a dedicated render thread until profiling proves the event-thread model is inadequate and a complete command/acknowledgement protocol exists for attach, detach, resize, present, context loss and shutdown. In particular, do not let the existing background renderer replacement worker call GLES2 or steal an EGL context.

## Narrow game-facing backend boundary

The game should eventually depend on a backend-neutral renderer contract, not on wgpu types or GLES2 types. The contract should be introduced incrementally and should cover only operations actually consumed by game/launcher code:

- configure HUD and UI resources;
- report framebuffer size and backend capabilities;
- upload or replace prepared world sections;
- update frame-independent state (camera, fog, lighting, animation);
- render/present one frame;
- request/collect screenshots where supported;
- suspend, resume, resize, recreate and shut down;
- expose timing/diagnostics without exposing GPU handles.

The wgpu implementation must preserve current desktop behavior. The GLES2 implementation may report unsupported capabilities explicitly; it must not silently emulate unsupported wgpu features.

Do not start by replacing the entire large renderer with a single trait. First inventory call sites by responsibility, then extract CPU payloads and lifecycle methods in small compiling steps. Keep the current desktop renderer path untouched.

## Integration gates

1. **Contract tests:** lifecycle ordering, window-generation invalidation, payload ownership, vertex ABI, checked buffer sizes and context-thread assertions.
2. **GLES2 renderer tests:** compile all GLSL ES 1.00 programs on a real ES2 implementation; verify texture limits, attributes, blending/depth state, buffer upload and context recreation.
3. **Horizon data adapter:** convert actual Horizon section mesh output and actual atlas/lightmap data into CPU payloads. No synthetic squares or white-only lightmap.
4. **Loading renderer integration:** draw the real loading UI through the new backend and survive Android pause/resume/resize.
5. **World renderer integration:** draw real opaque and translucent sections, then entities/sky/weather/UI in staged increments; preserve chunk upload and eviction behavior.
6. **Playable build:** package only after the real launcher, loading flow, world rendering, input, audio policy and lifecycle work together. Then test on the target TV.

Passing the existing ARMv7/API22 cross-check proves compile compatibility only. Passing the native GLES2 prototype checks proves its isolated ABI/shader/build contract only. Neither is evidence that Horizon is playable or that the target television has been tested.


## Exact Horizon terrain input discovered

The pinned source already exposes the CPU-side terrain mesh before GPU upload. The authoritative types are in `crates/horizon-client-render/src/mesher.rs`:

- `SectionMesh.solid: Vec<TerrainVertex>`
- `SectionMesh.translucent: Vec<TerrainVertex>`
- `SectionMesh.sides` stores directional buckets used for solid-face culling.
- `SectionMesh.visibility`, `block_entities`, `lights`, `sprite_layers`, `bounds` and `keep` are separate section metadata and must not be discarded by a geometry-only upload.

The actual `TerrainVertex` is a 24-byte packed structure, not the prototype's 48-byte GLES2 structure:

| Field | Horizon representation | Conversion requirement |
|---|---|---|
| Position | `[f32; 3]`, section-relative | Preserve exactly; add camera-relative section origin at draw time |
| UV | `[u16; 2]`, fixed point at `UV_STEPS = 32768` | Decode with `TerrainVertex::uv()`; do not reinterpret as normalized 16-bit UV |
| Layer | `u16` | Separate sprite index from `OPAQUE_LAYER` flag; resolve layer into a 2D atlas rectangle |
| Light | `u16`, sky in low byte and block in high byte | Decode both channels; do not treat as a single normalized scalar |
| Colour | packed RGBA8 `u32` | Preserve byte order and alpha metadata; alpha carries HDR emission tier and fluid-face information |

The mesher deliberately bakes directional face shade, tint and ambient occlusion into colour. The terrain shader then combines that with the lightmap. The alpha byte is not ordinary opacity: `mesher.rs` encodes HDR tier and fluid-face metadata there, and `gfx/native_material.rs` decodes it. A GLES2 adapter must decode and route these flags before it constructs its own vertex attributes; blindly copying alpha as transparency will break water and emissive blocks.

The `layer` value also has semantic flags. The high `OPAQUE_LAYER` bit is used for opaque leaves and quads over opaque texels, while the lower bits identify the sprite layer. The shader tests the high bit for alpha-cutout behavior and masks it before sprite lookup. The GLES2 path must preserve both behaviors.

### Sprite-array to GLES2 texture conversion

`RenderAssets.pixels` is organized as equal-sized sprite layers for `texture_2d_array`, with `sprite_size` and `texture_layers` describing the resource. GLES2 ES 2.0 has no core 2D texture-array sampler. Therefore the ES2 path needs an explicit 2D atlas builder:

1. Choose a grid and cell size from the device's `GL_MAX_TEXTURE_SIZE`; reject an atlas that cannot fit rather than overflowing dimensions.
2. Copy each source layer into its deterministic cell, preserving the original sprite index to cell mapping.
3. Convert each mesh vertex's sprite layer to normalized atlas origin/scale.
4. Add per-cell edge padding or clamp sampling within the sprite rectangle to prevent neighboring sprites bleeding under linear filtering.
5. Preserve the opaque-layer bit separately from the sprite index.
6. Upload the atlas and lightmap only while the GLES2 context is current.

The source also supports multiple sprite arrays after the first 2048 layers. An ES2 atlas builder cannot assume the layer index is below 2048; it must cover all `texture_layers` or return a clear unsupported-resource error. Device memory and maximum texture dimensions must be checked before allocating the packed atlas.

### Geometry conversion and ordering

The first adapter should copy the two existing mesh lists independently:

- `solid` remains the opaque/cutout pass, with the source side buckets retained for face culling where possible.
- `translucent` remains a separate pass and must be sorted using the camera-relative ordering expected by the current renderer. Concatenating it with solid geometry is incorrect.
- The section origin remains metadata, not baked into each vertex, so camera-relative precision is retained.
- Empty meshes must be valid zero-count uploads; all byte-size multiplication and GLES2 `GLsizei` conversions must be checked.

This establishes a concrete conversion contract from Horizon's existing section meshes to the prototype's expanded GLES2 vertex. It does not yet implement that conversion or prove the atlas packing/filtering matches the game's output.

### Existing GL shader path is not the ES2 implementation

The pinned `gfx/terrain_source.rs` is a legacy GLSL/WGSL lowering path for wgpu's GL backend. It retains texture-array sampling and other features that are not available in core GLES 2.0. It is useful as a source of rendering semantics, but it cannot be reused as an ES2 shader by changing only the version directive. The ES2 renderer needs its own shader and explicit 2D atlas sampling.


The pinned `sprites.rs` confirms the pixel layout precisely: `Sprites::finish()` resamples every sprite frame to one common square edge length (`RenderAssets.sprite_size`) and appends each layer's RGBA bytes consecutively to `RenderAssets.pixels`. Thus the CPU buffer is layer-major, with exactly `sprite_size * sprite_size * 4` bytes per layer. The ES2 atlas builder can copy layer `n` from byte range `n * layer_bytes .. (n + 1) * layer_bytes`; it must not assume the source buffer is already a single 2D atlas. Animated sprite frames are maintained separately in `RenderAssets.animations` and will need matching atlas-cell updates when animation support is integrated.
