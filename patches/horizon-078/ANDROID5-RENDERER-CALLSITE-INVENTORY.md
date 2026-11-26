# Android 5 renderer call-site inventory

Status: source audit at the pinned upstream revision. This is an implementation map, not a renderer abstraction or a claim that GLES2 is integrated.

- Upstream: `starling-client/starling`
- Revision: `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`
- Local target branch: `port/android-5-low-end`
- Target: Android 5.1 / API 22 / armeabi-v7a / GLES 2.0.

## Audit result

The concrete renderer type is not confined to one drawing call. The game stores `Option<Renderer>`, the Android launcher separately stores `Option<Renderer>`, and both loading and world renderer factories return the same concrete type.

Authoritative sites:

| Site | Concrete dependency |
|---|---|
| `crates/horizon-client/src/game.rs` around the `Game` fields | `renderer: Option<Renderer>` |
| `game.rs::loading_renderer` | constructs `Renderer::new(...)` for loading/title UI |
| `game.rs::build_renderer` | constructs `Renderer::new(...)` for world assets, receives `Option<&Renderer>` for resource reuse |
| `crates/horizon-client/src/android/launcher.rs` | `Launcher.renderer: Option<Renderer>`; calls the loading factory before Game exists |
| `crates/horizon-client-render/src/gfx.rs` | `Renderer` owns wgpu Instance, Adapter, Surface, Device, Queue, pipelines, textures and GPU buffers |

The launcher path is an independent entry path. Replacing only the Game field would leave the installer/loading UI on wgpu. The loading renderer is also constructed before the game world renderer, so backend choice must be made before either factory creates GPU resources.

## Calls made by the game

The following are the direct `renderer.method(...)` calls found in `game.rs` at the pinned revision. Calls through short aliases (for example `r`) are not counted in this direct-call list; the source still contains additional renderer queries and direct field reads.

| Responsibility | Direct calls |
|---|---|
| Frame and lifecycle | `render`, `resize`, `suspend_surface`, `recreate_surface`, `release_surface` |
| Section and LOD streaming | `upload`, `remove_columns`, `clear_lods`, `queue_lods`, `upload_lods`, `lod_detail_revision` |
| Loading/UI and menu blur | `set_loading_ui`, `invalidate_menu_blur`, `has_cached_menu_blur` |
| Frame diagnostics | `set_pass_timing`, `set_detailed_gpu_timing` |
| World resource replacement | `set_world_assets` |
| Visibility/occlusion | `start_occlusion` |

Other renderer members read in `game.rs` include `config`, `device`, `drawn_sections`, `gpu_times`, `hud`, `info`, `last_wait`, and `screenshot`. Some are accessed through optional references and helper closures. Those accesses are significant: a trait that only declares draw/upload methods cannot replace the current concrete type without first moving these queries behind backend-neutral methods or diagnostics snapshots.

The renderer is also passed into helpers and resource-replacement routines as `&Renderer` or `&mut Renderer`. A future interface must cover those signatures too; changing the field type alone is insufficient.

## Calls made by the Android launcher

At the pinned revision, the launcher directly calls:

- `resize(width, height)`
- `recreate_surface()`
- `set_loading_ui(...)`
- `suspend_surface()`

It also owns the native window and performs initial window creation, display preparation, renderer construction, and pause/resume handling. A native GLES2 implementation must be selected before `game::loading_renderer(...)` and must preserve the same ordering guarantees for the window.

## Why a single thin trait is not yet safe

A naive trait containing only `render`, `resize`, and `upload` would compile only after a large amount of surrounding code is changed. Current game logic also asks the renderer about section residency, LOD revisions, menu blur, screenshot state, GPU timings, display changes, visibility, shader-pack capabilities and configuration. It reads renderer fields directly, and world-renderer replacement currently reuses wgpu-specific resources.

The current `Renderer` also owns a wgpu surface and device. These are not neutral handles that can be populated with EGL/GLES2 objects. The `horizon-client-render` crate has `#![forbid(unsafe_code)]`; EGL FFI and unsafe GL calls must stay in a narrow native/FFI crate or NDK layer, not by weakening this crate-wide rule.

## Recommended extraction order

1. **Split construction from concrete wgpu construction.** Introduce a backend selection/factory at the launcher/game boundary. Keep the existing desktop factory and its wgpu `Renderer` unchanged. The Android selection must occur before both loading and world renderer construction.
2. **Extract lifecycle and frame presentation first.** Define window attach, resize, suspend, resume/recreate, render/present and shutdown semantics. Use a monotonically increasing window generation so stale native-window work cannot be applied after resume.
3. **Extract terrain streaming as an owned CPU interface.** Keep the existing `pending_uploads` budget/coalescing semantics. Convert `SectionMesh` only at the selected backend boundary, transfer owned payloads, and define explicit rejection/release behavior. Do not add a second unbounded queue.
4. **Move renderer queries behind snapshots/capabilities.** Replace direct reads such as GPU info, timing and section counts with backend-neutral query structs. Explicitly report unsupported GLES2 features instead of fabricating wgpu values.
5. **Extract remaining rendering responsibilities by pass.** Loading/menu/HUD, terrain, entities/block entities, sky/weather/clouds, particles/effects, text, screenshots and pack-specific paths need deliberate support or explicit fallback.
6. **Keep desktop regression checks.** The existing desktop wgpu implementation must remain the default on non-Android targets and retain its existing call path.

Do not implement a broad trait or enum wrapper until the call-site groups and resource replacement semantics are covered. In particular, `previous: Option<&Renderer>`, `Renderer::background_context` / replacement activation, and direct `device` access are wgpu-specific seams that need their own design rather than unsafe casts or fake handles.

## Practical next implementation boundary

The first code change should not attempt to make the entire world renderer polymorphic. It should introduce an Android-only renderer factory boundary shared by the launcher loading path and the Game loading/world paths, while keeping the desktop `Renderer::new` path untouched. The factory can initially return the current wgpu renderer on all targets; this is a compile-preserving seam only. The next commit can then add the GLES2 implementation behind that seam and expand the interface one responsibility group at a time.

Until that factory and lifecycle contract exist, do not connect the CPU terrain queue to Game and do not build a diagnostic APK as though it were a playable game.
