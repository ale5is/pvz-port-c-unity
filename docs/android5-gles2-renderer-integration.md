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

## Acceptance rule

Do not label any artifact "playable" until the Android client loads a real world, uploads and draws real Horizon sections, survives window pause/resume, and reports no fatal EGL/GLES errors on the target Android 5.1 ARMv7 device. A successful APK build only proves packaging and compilation.
