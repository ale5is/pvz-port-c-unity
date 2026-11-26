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

## Implemented CPU upload-queue generation guard

The Rust-side Gles2SectionQueue now carries a monotonically increasing window/context generation in addition to its existing byte and entry budgets:

- advance_generation(next) accepts only a strictly newer generation, clears all pending payloads from the previous window lifetime, and updates the active generation.
- enqueue_for_generation(generation, payload) rejects stale or future-generation work without mutating the queue and returns ownership of the rejected payload to the producer.
- Existing coalescing-by-section-key, replacement accounting, maximum pending section count, and reserved-capacity byte accounting remain in force.
- Regression tests cover stale producer rejection, queue invalidation on generation advance, monotonicity, active-generation acceptance, and byte accounting after invalidation.

This closes one CPU-side lifecycle hazard: a delayed meshing result cannot be admitted to a queue after the owner has moved to a newer native-window generation, provided the future bridge tags every enqueue with the generation captured when the work was scheduled.

This is **not yet wired into Horizon's live Game upload path or an EGL owner**. The caller must still capture the active generation when scheduling conversion and pass it to enqueue_for_generation; no live window-generation source or native bridge currently exists. The guard is therefore implemented and unit-testable, but stale native-window work is not yet prevented in the running game.
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
| UV | `[u16; 2]`, fixed point at `UV_STEPS = 32768` | Decode with `TerrainVertex::uv()`; do not reinterpret as normalized 16-bit UV; reject non-finite positions and UV-scale overflow before enqueue |
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

This establishes the conversion contract from Horizon's existing section meshes to the prototype's expanded GLES2 vertex. A first CPU-only implementation now exists in `tools/gles2-hud-prototype/app/src/main/cpp/horizon_terrain_adapter.{h,cpp}`; its exact scope and remaining gaps are listed below.

### Existing GL shader path is not the ES2 implementation

The pinned `gfx/terrain_source.rs` is a legacy GLSL/WGSL lowering path for wgpu's GL backend. It retains texture-array sampling and other features that are not available in core GLES 2.0. It is useful as a source of rendering semantics, but it cannot be reused as an ES2 shader by changing only the version directive. The ES2 renderer needs its own shader and explicit 2D atlas sampling.


The pinned `sprites.rs` confirms the pixel layout precisely: `Sprites::finish()` resamples every sprite frame to one common square edge length (`RenderAssets.sprite_size`) and appends each layer's RGBA bytes consecutively to `RenderAssets.pixels`. Thus the CPU buffer is layer-major, with exactly `sprite_size * sprite_size * 4` bytes per layer. The ES2 atlas builder can copy layer `n` from byte range `n * layer_bytes .. (n + 1) * layer_bytes`; it must not assume the source buffer is already a single 2D atlas. Animated sprite frames are maintained separately in `RenderAssets.animations` and will need matching atlas-cell updates when animation support is integrated.


## First CPU adapter implementation

Two CPU adapter layers now exist, with deliberately different roles:

- The workflow patch `gles2-terrain-adapter.py` installs `crates/horizon-client-render/src/gles2_terrain.rs` into the pinned Horizon checkout. This is the Rust-side adapter that works directly with Horizon's `SectionMesh`, `TerrainVertex` and `Sprites` types. It builds an `AtlasGrid`, packs layer-major RGBA pixels with a duplicated one-texel gutter around every sprite cell, decodes vertex metadata, expands Horizon's four-vertex quads into six-vertex GLES2 triangles, retains solid side buckets, and provides per-section translucent back-to-front sorting. `convert_section()` now returns an owned `Gles2SectionPayload` that also preserves the section key, visibility graph, block entities, light-source positions, used sprite layers, bounds and `keep` flag; its checked `byte_len()` can feed a future bounded upload queue. Its unit tests include atlas edge/gutter and metadata-preservation checks and run in the Android 5 workflow.
- The standalone NativeActivity prototype now also contains `horizon_terrain_adapter.{h,cpp}`, compiled into its Android CMake target. It declares the exact 24-byte Horizon input layout, packs the same layer-major atlas layout, and converts raw vertex records into the prototype's 48-byte GLES2 ABI. This is an isolated native-side counterpart; the real Rust adapter is the authoritative conversion for Horizon-owned mesh types.

Both implementations check texture fit and sprite indices rather than silently truncating layers. Both atlas builders duplicate each sprite's outermost texels into a one-pixel gutter, and each vertex's atlas rectangle points only at the original sprite pixels; this prevents linear filtering from blending adjacent atlas cells. Both converters take the UV scale explicitly (32768 for ordinary section meshes; 32 for tiled distant-terrain UVs). Both expand Horizon's four source vertices per quad into the six-vertex triangle list required by `glDrawArrays(GL_TRIANGLES)`; the native converter also validates the supplied pixel-buffer length before copying it.

The alpha metadata follows the pinned Horizon logic: `tier = 15 - (alpha & 0x0f)`; the upper nibble carries fluid-face metadata. The emission attribute remains the tier byte, and the ES2 fragment shader converts its normalized value using the same `tier * 7 / 15` scale used by `gfx/native_material.rs`.

### Explicit limitations

Neither CPU adapter is connected to the live renderer yet. The new `Gles2SectionPayload` is a defined CPU ownership boundary, not an EGL/GL upload queue: the Android launcher still constructs the existing wgpu `Renderer`, and the C++ NativeActivity prototype still draws synthetic geometry. The Rust module is installed and unit-tested in the patched Horizon source, but `loading_renderer()` and `build_renderer()` still create the existing wgpu renderer; the NativeActivity still feeds synthetic test geometry. The native section store accepts validated solid draw ranges in expanded triangle-vertex units. The CPU adapter already exposes `visible_solid_triangle_ranges()` from Horizon's `Sides::visible()`; passing those ranges to `set_section_solid_ranges()` lets the GLES2 draw path skip face buckets turned away from the camera without rewriting the mesh. Range lists are checked for triangle alignment, bounds and overlap. The current prototype still needs the real frame loop to refresh these ranges as the camera moves. The native prototype also bounds resident terrain VBO storage independently of section count: at most 512 sections and 32 MiB of combined solid/translucent vertex buffers. An upload is admitted only when the currently resident VBO bytes plus the complete replacement fit within 32 MiB, so the temporary old-plus-new allocation peak is also bounded; this can reject a replacement that would fit after deleting the old buffers, intentionally preserving atomicity and the existing visible section. The final resident accounting subtracts the replaced allocation only after both new VBOs upload successfully. Section and column eviction subtract their exact recorded byte sizes, and clearing the cache resets the counter. This is a prototype safety ceiling, not yet a measured optimal memory budget for the Philips TV. The current atlas builder uses one 2D texture and returns an error if the complete layer set cannot fit. Animated-frame atlas updates, actual Horizon lightmap generation/upload, global translucent ordering across sections, section upload/eviction, and the winit launcher/EGL lifecycle bridge remain integration work. Static checks, Rust unit tests and Android NDK compilation validate CPU conversion and build contracts, not pixel correctness or playability on the Philips TV.


## Atlas filtering correction

The initial compact atlas put sprite cells directly next to one another. Even when UVs are clamped to the cell rectangle, linear filtering can blend texels from a neighbouring sprite at exact edges or under interpolation precision. Both the Rust Horizon adapter and the isolated C++ adapter now reserve a one-pixel border around each cell and duplicate the sprite's nearest edge texels into that border. The vertex rectangle starts one texel inside the padded cell and retains the original sprite dimensions.

The Rust adapter tests verify all four edges/corners of a small sprite and the resulting normalized atlas rectangle. This is a CPU-side filtering safeguard; it does not yet implement mipmapped atlas levels or animation-frame cell replacement. The GLES2 terrain shaders remain GLSL ES 1.00 and clamp sampling to the inner sprite rectangle.


## Owned section payload boundary

The Rust adapter's `convert_section()` creates an owned payload from a real `SectionMesh` without retaining references to meshing-worker memory or introducing GPU handles. It carries the key and all section metadata needed by later renderer stages. `byte_len()` uses checked arithmetic so a bounded CPU-to-render upload queue can account for payload sizes before accepting them. The payload is currently only produced and unit-tested; the streaming completion path does not enqueue it, and no EGL/GL object is created or uploaded from it yet.

## Bounded CPU section queue

The Rust adapter now defines `Gles2SectionQueue`, a FIFO queue for owned `Gles2SectionPayload` values. Its byte limit is supplied at construction and is enforced using each payload's checked `byte_len()` before accepting an update. A separate `MAX_PENDING_SECTIONS = 512` cap limits distinct queued keys, including empty-geometry sections, so a stream of tiny updates cannot grow the queue's entry count without bound.

- A pending update for an already queued section key is coalesced: the older payload is replaced and the newest payload moves to the back of the queue.
- A payload larger than the entire byte budget, an accounting overflow, an update that would exceed the byte budget, or a new key after the 512-entry cap is reached is rejected. Rejection returns ownership of the incoming payload and leaves the existing queue unchanged, including when an oversized update targets a key already pending. Replacing an existing key remains allowed at the entry cap.
- `pop_front()` transfers the next payload to the consumer and decrements accounting.
- `remove_section()` removes one exact section key; `remove_columns()` removes every queued vertical section for the requested horizontal (x, z) chunk columns.
- `clear()` releases all pending payloads and resets byte accounting.
- Tests cover replacement order, over-budget rejection, atomic replacement failure, the 512-key cap and replacement at capacity, section/column unload, and accounting after pop/clear.

The limit measures the complete inline `Gles2SectionPayload` size plus the reserved capacities of its converted vertex and owned metadata vectors. It therefore includes spare capacity retained by those payload vectors, but excludes allocator bookkeeping, spare capacity in the queue's own `VecDeque`, temporary conversion peaks before enqueue, atlas/lightmap memory, and GPU allocations. It is a bounded pending-payload budget, not a complete process-memory ceiling. The queue is CPU-only and owns no GL/EGL handles. It is not yet connected to Horizon's meshing completion path, game section unload events, or a renderer; the launcher still constructs wgpu. Passing its unit tests will validate queue semantics only, not terrain presentation or playability.

## Live game-loop integration seam (pinned upstream audit)

The pinned source was traced beyond the CPU adapter to identify where real meshes and Android window ownership enter the game. This is the integration boundary; the queue is not wired into it yet.

### Section mesh production and current upload ownership

- `horizon-client/src/game.rs` owns `pending_uploads: Vec<((i32, i32, i32), SectionMesh)>`. Completed meshes arrive in the frame's `uploads` collection.
- The existing game loop already coalesces pending meshes by section key: fresh meshes replace older pending meshes, and entries for chunks no longer loaded are dropped. It sorts pending work by distance and applies `UploadBudget` before calling `renderer.upload(key, &mesh)`.
- On Android client sessions, `mobile_uploads` forces this budgeted path. After uploads, `renderer.remove_columns(unloaded...)` handles renderer-side chunk-column eviction.
- The non-budgeted desktop fast path also calls `renderer.upload` directly. It must remain unchanged for desktop.
- `horizon-client-render/src/gfx.rs` owns the concrete `Renderer`; `upload` updates occlusion visibility, block-entity and light metadata, LOD state, GPU arena allocations and resident section tables before drawing. It is not a narrow terrain-only interface.

This means the upstream game already has a CPU-side scheduling/coalescing queue. A GLES2 integration should not blindly add a second unbounded `Vec<SectionMesh>`. The adapter payload queue is useful only after conversion and needs to replace or deliberately sit behind the Android pending-upload stage, with a single documented memory budget and correct return/free ownership for rejected or consumed meshes.

### Android window and EGL lifecycle

- `Game` implements winit `ApplicationHandler`. Android `suspended()` marks the surface lost and calls `renderer.suspend_surface()`.
- Android `resumed()` reuses the existing renderer, resizes it, and calls `renderer.recreate_surface()`; initial window creation happens later through `ActiveEventLoop::create_window`.
- The normal startup helpers `loading_renderer()` and `build_renderer()` both construct the same concrete wgpu `Renderer`; the game field and its callers are typed to that renderer.
- The current renderer owns a wgpu surface/device/queue, dozens of pipelines and GPU resources, HUD, entity, sky, post-processing and other passes. Replacing it with a terrain-only GLES2 class would leave the rest of the game without a renderer. Keeping wgpu for the HUD while creating an unrelated EGL context on the same native window also cannot be assumed to work: surface ownership, buffer swaps, synchronization and context lifetime must be explicitly designed.

### Required integration sequence

1. Introduce a deliberate Android backend boundary at the game/renderer ownership level, without changing desktop's concrete wgpu path. Keep loading/menu/HUD responsibilities explicit rather than pretending the terrain adapter implements them.
2. Define a renderer lifecycle contract for create, resize, suspend, resume/recreate and shutdown on the winit event thread. EGL display, context and window surface must be created and destroyed on a controlled owner thread with the Android native window valid.
3. Route Android completed `SectionMesh` values through `convert_section()`, preserving the original key and metadata. Transfer owned payloads to the render owner; return/release rejected payloads and remove queued sections on unload. Keep the existing upload budget semantics and avoid double-accounting or holding both original and converted meshes longer than necessary.
4. Implement actual GLES2 terrain resource upload and draw from the converted payload, including atlas/lightmap, solid side buckets, camera-relative transforms, visibility, material/fluid/emission rules, animated atlas cells and section eviction. The current synthetic NativeActivity renderer is not this implementation.
5. Only after terrain can be drawn from live Horizon sections, add a real-world integration smoke test and build an integration APK. A playable APK additionally requires the remaining visible game passes and lifecycle/input path; CPU tests and a native demo do not establish playability.

No upstream game-loop or renderer source has been changed by this audit. The PC renderer and `main` remain outside this Android-only port.


## Backend-selection audit: OpenGL is not the GLES2 port

A further audit of the pinned source resolved an important ambiguity: setting Horizon's existing `GraphicsApi::OpenGl` does **not** select the new GLES2 adapter. It selects wgpu's `Backends::GL` path:

- `horizon-client-render/Cargo.toml` enables wgpu's `gles` feature for non-macOS targets.
- `horizon-client-render/src/gfx.rs::backend_order()` maps `GraphicsApi::OpenGl` to `wgpu::Backends::GL`.
- `gfx::Renderer::new()` / `new_with_context()` still create a wgpu instance, adapter, device, queue, surface, pipelines and the existing wgpu resource graph.
- The Android launcher creates this wgpu renderer for the loading UI before it creates the gameplay handler. Therefore, replacing only `Game.renderer` after loading would be too late: the boot/loading renderer also needs a backend-neutral owner and an Android-specific implementation.
- The wgpu GL backend is not a contract for OpenGL ES 2.0. No GLES2 guarantee can be inferred from the word "OpenGL", from enabling wgpu's `gles` feature, or from a successful ARMv7 cargo check.

The Philips QM152E target reports OpenGL ES 2.0 / GLSL ES 1.00. The GLES2 shaders and CPU mesh adapter in this repository are therefore a separate backend implementation, not a configuration switch for the existing wgpu renderer.

### Architecture consequence

Do not route this device through `GraphicsApi::OpenGl` and call it GLES2. The Android-only branch must be selected before the launcher constructs its loading renderer. A viable split needs a shared renderer-facing contract (or an Android-specific launcher/game facade) whose lifecycle covers both loading and gameplay. The desktop build must continue to instantiate the existing wgpu renderer without behavior changes.

The current render crate has `#![forbid(unsafe_code)]`; native EGL/GLES calls should not be smuggled into that crate by weakening the lint globally. Keep the unsafe platform boundary isolated (for example, a small Android native/FFI crate or the existing NDK layer), expose a narrow safe Rust API to the game, and make the event-thread ownership explicit. This also avoids two independent EGL owners attempting to present to the same winit window.

A terrain-only backend is still not a playable backend. The contract must include at least the loading/menu/HUD path, world terrain, entities/block entities, sky/weather, particles/effects, text/UI, input-driven camera updates, resize and Android suspend/resume. Unsupported passes must be tracked explicitly rather than silently omitted.

### Verified source anchors

The Android 5 workflow now checks the pinned game-loop upload/lifecycle seam. The additional backend-selection check verifies that the upstream source still maps `GraphicsApi::OpenGl` to wgpu GL and that the Android launcher constructs the loading renderer before handing control to the game. These are source-contract checks only; they do not claim a GLES2 context was created or a frame was rendered.

## Native atlas integration correction

The packed GLES2 atlas uses cells of `sprite_size + 2` texels because every sprite receives a one-texel filtering gutter. Therefore an atlas generated from 16×16 source sprites commonly has 18×18 cells, and the final atlas width/height are multiples of the cell size, not necessarily multiples of 16.

The native renderer previously rejected such valid atlases by requiring `atlas_width % sprite_size == 0` and `atlas_height % sprite_size == 0`. That validation contradicted the CPU atlas builder and prevented the packed atlas from reaching texture upload. The renderer now validates positive dimensions and lets the atlas builder's checked cell/grid calculations own layout validation. The native renderer checker includes a 16px sprite / 18px cell regression guard.

This fixes a native prototype integration defect only. It does not connect the prototype to Horizon's launcher or game loop.

## Native GLES2 renderer hardening batch

The standalone native terrain prototype now applies the CPU atlas contract consistently and checks device-sized allocations before issuing GL calls:

- The terrain atlas uses linear filtering so the duplicated one-texel gutters actually protect sprite edges during interpolation. The lightmap remains linearly filtered.
- Atlas and lightmap inputs are checked for null pointers, positive dimensions, and overflow-safe RGBA byte counts. The byte count must fit the signed pointer-sized GL upload range on 32-bit ARM.
- `GL_MAX_TEXTURE_SIZE` is queried and validated before texture allocation; a failed or invalid capability query is reported instead of accepting an unbounded texture.
- Texture allocation failure is detected, and unpack alignment is restored to the GLES default after uploads so the renderer does not leak pixel-store state into other code.
- VBO uploads validate both `GLsizei` vertex counts and `GLsizeiptr` byte sizes, reject a zero buffer name, and clear stale error text after successful uploads.
- Both fragment shaders select high precision when `GL_FRAGMENT_PRECISION_HIGH` is available and fall back to mediump otherwise. The distance varying uses matching mediump precision across the vertex/fragment interface.

The native renderer checker now guards these requirements, including the 16px sprite / 18px gutter-cell atlas case. These changes harden the isolated GLES2 prototype and its resource uploads; they do not yet implement Horizon's complete material system or connect the renderer to the winit launcher.

The native draw path also caches all nine uniform locations once after linking each solid/translucent program, rather than calling `glGetUniformLocation` for every section pass on every frame. It clears both texture units after drawing and resets cached locations when GL resources are released. The CI source checker rejects a regression that performs uniform lookups inside the per-frame draw function.

A new workflow step extracts the three exact GLSL strings compiled at runtime by `Gles2TerrainRenderer` and runs `glslangValidator` on them. This closes a previous validation gap: compiling the separate reference shader files did not prove that the shader strings actually embedded in the Android C++ renderer were valid.



## EGL owner-thread enforcement in the native prototype

The native terrain renderer now binds its EGL owner thread on the first `initialize()` call. Every public operation that can touch GL state checks that same thread before doing work: texture/program initialization, standalone and keyed uploads, side-range updates, section/column eviction, cache clearing, both draw paths, and release.

An operation issued before initialization or from a different thread is rejected before its first GL call. Boolean operations return `false`; void operations leave the GL resources untouched and record a diagnostic in `last_error()`. Reinitialization is permitted only on the original owner thread. The renderer does not silently migrate its context ownership.

This is a defensive guard, not a replacement for lifecycle coordination: the owner must still make the EGL context current before calling the API, call `release()` before EGL teardown, and destroy the renderer on its owner thread. A wrong-thread destructor cannot safely delete GL objects; callers must obey the destruction contract. The static GLES2 checker now verifies that each public GL entry point contains its owner-thread guard. Android NDK CI is the compile check for the C++ implementation; neither check demonstrates integration with Horizon's live game loop.

## Native backend bridge now installed (not yet selected by Game)

The repository now carries a reusable C ABI in `tools/gles2-hud-prototype/app/src/main/cpp/horizon_gles2_bridge.{h,cpp}`. It accepts Horizon's real packed section vertices, solid face ranges, atlas layers, section removals and frame draw parameters. The API owns the native `Gles2TerrainRenderer` object but deliberately does not create an EGL context itself.

A separate `horizon_egl_owner.{h,cpp}` owns one Android native-window reference, EGLDisplay, ES2 EGLConfig, EGLSurface and EGLContext. It enforces the creating thread for make-current, resize, clear, swap and teardown. The Rust runtime wrapper composes the two owners and declares the GL renderer before the EGL owner so the GL objects are destroyed while the context is still alive.

The Android patch script copies these native sources into `crates/horizon-android/gles2-native`, adds a `cc` build dependency and `build.rs`, links EGL/GLESv2/android/log, and installs the Rust FFI and winit Android native-window wrapper. The API is therefore part of the Android cdylib build rather than a separately compiled demo-only shared library.

**Remaining integration boundary:** `Game` and `Loading` still store `Option<gfx::Renderer>`, and their render/upload paths still invoke wgpu. The new `gles2_egl::Runtime` is not yet constructed by the launcher, transferred into `Game`, or called from `renderer.upload` / `renderer.render`. EGL surface detach/re-attach and context-loss recovery are also not yet wired to Android lifecycle callbacks. This commit series must not be described as a connected game renderer until those call sites are replaced and the Android cdylib build passes.
