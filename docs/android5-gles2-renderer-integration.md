# Horizon 0.7.8 Android 5 GLES2 renderer integration

Status: architecture investigation; no claim of a playable GLES2 client.

Pinned upstream source:
- Repository: https://github.com/starling-client/starling
- Revision: `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`

## Finding: the backend seam is above Renderer construction, not inside upload_mesh

The upstream `horizon-client-render::gfx::Renderer` is a concrete wgpu implementation. It owns the wgpu instance, adapter, surface, device, queue, pipelines, textures, section arenas, LOD storage, entity/weather/sky/HUD resources, frame timing, screenshots and surface lifecycle. Its public constructor is asynchronous and creates all of those resources together.

The game does not interact with terrain upload alone. It also:
- constructs and replaces renderers through `build_renderer`, `loading_renderer`, `Game::renderer_for` and Android launcher setup;
- calls section and LOD lifecycle methods (`upload`, `remove_columns`, `clear_lods`, `queue_lods`, `upload_lods`, `start_occlusion`);
- calls frame methods (`render`, `resize`, `suspend_surface`, `recreate_surface`, `release_surface`);
- reads diagnostics and state (section counts, GPU timings, screenshots, adapter information);
- accesses public fields directly, including `device`, `queue`, `config`, `info`, `hud`, `screenshot`, `screenshot_result`, `gamma`, `fullbright`, `animated_textures`, `drawn_sections`, `section_draws`, `section_draw_calls`, `uploaded_bytes`, `gpu_times`, `last_wait` and `multi_draw`.

This means replacing `Renderer` with a small trait that only exposes `upload(SectionMesh)` and `render(Frame)` is not a safe first patch. It would either break many game call sites or leak wgpu types back through the trait, defeating the separation. The render crate also has `#![forbid(unsafe_code)]`; EGL/GLES2 FFI must remain in an Android host/native boundary.

## Required migration shape

1. **Keep the current wgpu Renderer intact.** PC and existing Android Vulkan/OpenGL behavior remain the reference backend.
2. **Introduce a client-facing renderer owner above backend construction.** The owner selects a backend once, at renderer creation, and owns the common lifecycle. The existing wgpu renderer initially remains behind the owner without changing its draw behavior.
3. **Move backend-specific public-field access behind typed operations.** In particular, remove direct `wgpu::Device`, `wgpu::Queue` and `wgpu::SurfaceConfiguration` exposure from game/UI call sites. HUD resource setup must become a backend operation rather than reaching into `renderer.hud` and `renderer.device`.
4. **Separate common render inputs from GPU resources.** The common boundary should accept prepared assets, frame data, section mesh updates/removals, LOD updates, resize and lifecycle events. It must not expose wgpu handles or EGL handles.
5. **Add the GLES2 implementation in the Android host/native layer.** It owns EGL context/surface and GLES objects, recreates them on window changes, and consumes the already-tested CPU terrain conversion. The Rust render crate remains safe Rust.
6. **Connect real world resources and passes incrementally.** First terrain atlas + lightmap + section VBO storage + visibility ranges; then camera/projection/fog; then translucent ordering; then block entities, weather, sky, HUD and menus. Unsupported features must be explicitly disabled or given a fallback, never silently drawn as if supported.
7. **Add device-level verification.** Build and static checks are not enough: capture logs for EGL/GLES initialization, upload a real section, present multiple frames, test suspend/resume, and verify the TV's Mali-450 output.

## First GLES2 milestone (before calling the port playable)

- GLES2 context creation and Android window lifecycle.
- Real section mesh upload and replacement/removal.
- Correct atlas tile addressing, lightmap channel ordering and emission.
- Camera matrices and world-space section origins.
- Solid depth pass and back-to-front translucent pass across visible sections.
- No wgpu object or unsafe EGL call in the shared CPU adapter.
- A diagnostic scene generated from actual Horizon world meshes, not synthetic test quads.

## Current reusable work

The branch already contains:
- a Rust CPU adapter that packs Horizon sprite layers into a 2D GLES2 atlas;
- Horizon quad-to-triangle conversion, material/emission extraction and `Sides` visibility-range conversion;
- translucent quad sorting helper;
- GLSL ES 1.00 terrain shaders;
- a 48-byte Rust/C++ vertex ABI contract;
- a separate NativeActivity prototype that draws synthetic quads.

These pieces are useful building blocks, but they are not yet wired into Horizon's real `Renderer::upload` / frame path. The prototype's synthetic atlas and geometry must not be mistaken for the game's assets or world.

## Remaining integration milestones and completion count

The NativeActivity renderer remains an isolated GLES2 proof of concept. The current CI addition compiles its Android 5 ARMv7 C++ target with the NDK; it does not connect the renderer to Horizon's game loop. After the current adapter/build-validation batch, eight integration milestones remain:

1. **Backend ownership boundary:** introduce a game-facing renderer owner and a backend contract; keep wgpu as the unchanged PC implementation and move direct wgpu field access behind typed operations.
2. **Android EGL backend lifecycle:** attach GLES2 context creation, surface replacement, pause/resume, resize and context-loss recovery to Horizon's actual Android launcher, not the standalone NativeActivity prototype.
3. **Real resource bridge:** extract Horizon's actual terrain sprite layers, atlas metadata, lightmap and material data; validate pixel format, channel order, UV scale, texture limits and memory use on GLES2.
4. **Real section residency:** route Horizon section mesh creation, upload, replacement, eviction and unload through the GLES2 backend, including section world origins and bounded CPU/GPU memory.
5. **World camera and opaque pass:** connect the live camera/projection, viewport, depth state, face visibility and fog; render actual solid sections from a loaded world.
6. **Translucency correctness:** globally order visible translucent section/quad data, apply blending and depth writes correctly, and verify water/glass ordering while the camera moves.
7. **Remaining game rendering:** port or deliberately provide fallbacks for block entities, animated textures, sky, weather, particles, HUD, fonts, menus and screenshots; no silent placeholder rendering.
8. **Target-device acceptance and APK:** build the integrated client, install it on the Philips QM152E, load a real world, test repeated frames and pause/resume, inspect EGL/GLES logs, and tune memory/performance on Mali-450. Only this gate permits calling the APK playable.

A milestone may contain several code commits and CI iterations. The count is eight major integration gates, not eight guaranteed small edits. Device acceptance depends on the physical TV and cannot be completed by CI alone.

## Acceptance rule

Do not label any artifact "playable" until the Android client loads a real world, uploads and draws real Horizon sections, survives window pause/resume, and reports no fatal EGL/GLES errors on the target Android 5.1 ARMv7 device. A successful APK build only proves packaging and compilation.

## Pinned-source call-site inventory (verified against the locked revision)

The backend migration must preserve more than the visible terrain draw. A source inspection of `crates/horizon-client-render/src/gfx.rs` at the pinned revision confirms these renderer operations are public and called from the game:

- Construction/replacement: `Renderer::new`, `Renderer::new_with_context`, `background_context`, `activate_replacement`.
- Surface lifecycle: `release_surface`, `suspend_surface`, `recreate_surface`, `resize`.
- World residency: `upload`, `clear_sections`, `remove_columns`, `queue_lods`, `upload_lods`, `start_occlusion`.
- Frame/presentation: `render`, `wait_in_flight`.
- Resource/configuration: `set_world_assets`, `set_present_mode`, `set_texture_filtering`, `set_mipmap_levels`, shader-pack and HDR configuration.
- Visibility/diagnostics: `section_count`, `resident_sections`, `lod_quads`, `section_vertex_bytes`, `gpu_stage_times`.

The game-side call sites in the same revision still depend directly on concrete renderer state. Examples include:

- `renderer.config.width/height` to size loading HUD batches;
- `renderer.info` passed to `lod_distance_capped`;
- `renderer.hud` and `renderer.device` for HUD atlas animation and text shader setup;
- `renderer.last_wait` and `renderer.gpu_times` for frame timing;
- `renderer.screenshot` and `renderer.drawn_sections` for screenshot/debug state;
- direct lifecycle and mesh calls during Android window changes and streaming.

The HUD setup and screen-size accesses have already been partially moved behind methods by the branch's patch scripts. The remaining accesses are not all interchangeable: for example, `wgpu::AdapterInfo` is not a portable capability type, and GLES2 must not fabricate a wgpu adapter merely to satisfy `lod_distance_capped`. The next boundary change therefore needs an explicit backend-neutral capability snapshot (only the fields actually consumed by game policy), plus typed timing/screenshot/debug accessors. It must not expose a wgpu handle through a nominally backend-neutral interface.

### Implementation order derived from the call sites

1. Keep the current `Frame` as the shared, CPU-side frame description; it contains game render inputs and does not itself require a wgpu device.
2. Define backend-neutral value types for screen extent, relevant renderer capabilities, frame timing and screenshot completion. Keep GPU handles private to the backend.
3. Move the remaining game-side reads behind narrow methods before introducing a second backend implementation. This makes the existing wgpu path compile against the same contract first.
4. Keep section upload/removal, LOD residency and surface lifecycle as explicit operations on the renderer owner; do not collapse them into a generic byte-buffer API.
5. Only after the wgpu implementation passes the same call-site tests should the Android EGL/GLES2 implementation be selected by the Android launcher.

This inventory is based on the exact upstream revision above. It is an architectural map, not a claim that a backend trait or GLES2 game-loop integration has already been implemented.

## Follow-up boundary reduction: adapter policy

The patch script `patches/horizon-078/renderer-hud-boundary.py` now also moves the two game-side far-terrain distance decisions behind `Renderer::lod_distance_capped(distance)`. The wgpu adapter metadata remains inside the current wgpu renderer; game code no longer needs to pass `wgpu::AdapterInfo` into the graphics policy for those two decisions.

This is deliberately a narrow migration, not a claim that adapter metadata is fully backend-neutral. The Android workflow now adds a cross-target `cargo check --target armv7-linux-androideabi -p horizon-client --lib` after applying the source patches. This should compile the patched game and renderer for the target architecture without packaging an APK. Its success remains unverified until GitHub Actions reports a completed run.

## Follow-up boundary reduction: screenshot and section state

The same patch now moves the world-icon screenshot path behind three renderer operations:

- `drawn_section_count()` exposes the last frame's section count as a value, rather than exposing the storage field.
- `has_pending_screenshot()` checks whether another screenshot owns the next frame.
- `request_screenshot(request)` queues a request only when the slot is free and reports whether it accepted it.

This preserves the existing world-icon behavior: it waits until more than ten sections have been drawn, does not overwrite an F2/agent screenshot, and queues the 64×64 icon capture. The game no longer reads or writes `Renderer::drawn_sections` or `Renderer::screenshot` directly at these call sites.

The methods are still implemented by the current wgpu renderer; this is an ownership/API cleanup, not yet a second backend or a backend-neutral screenshot implementation. The patch script asserts the expected upstream call-site counts and fails rather than silently applying to a changed source layout. The cross-target build and the patch itself still need a completed CI run before they can be considered validated.

## Follow-up boundary reduction: frame timing

The game-side upload budget, frame graph, slow-frame diagnostics, and Android performance telemetry now use renderer methods rather than reading or draining timing fields directly:

- `last_wait_duration()` returns the wait time from the previous render.
- `last_encode_duration()` returns the CPU-side encoding time from the previous render.
- `take_latest_gpu_time()` returns the most recent GPU timing sample for the loading-screen graph and drains that sample set.
- `take_gpu_times()` transfers the main-loop GPU timing samples to the game for frame statistics and Android telemetry.

The main loop drains samples once and shares that owned sample vector with the frame graph, GPU-utilization calculation, frame statistics, and Android performance recorder. This preserves the previous behavior of recording the full sample set and clearing the renderer's collection after consumption. The patch asserts the expected upstream call-site counts before replacing them.

This does not yet move frame pacing, GPU timestamp collection, or upload-budget policy into a backend-neutral interface. It only establishes a narrow renderer-owned API so a future backend can implement or omit GPU timing without exposing wgpu's storage layout to game code. CI validation remains pending.

## Follow-up boundary reduction: adapter metadata

Game-side diagnostics and Android renderer-replacement decisions now use four renderer accessors:

- `adapter_name()` supplies the human-readable adapter label.
- `backend_name()` supplies the backend label used in diagnostics.
- `is_gl_backend()` and `is_vulkan_backend()` keep the existing Android surface/replacement decisions without exposing `wgpu::AdapterInfo` or `wgpu::Backend` to those call sites.

The backend checks intentionally preserve the existing policy: OpenGL renderers take the GL-specific path; non-GL renderers are eligible for the Vulkan path only when the previous Vulkan condition is satisfied. This is still a wgpu-backed implementation behind a narrower API, not a GLES2 backend implementation. CI validation remains pending.
