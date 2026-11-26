# Android 5 GLES2 full-renderer migration audit

Pinned source: starling-client/starling at commit 68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29.

Target: Android 5.1 / API 22, ARMv7, GLES 2.0 / GLSL ES 1.00, Mali-450 MP, 1280x720, approximately 1 GiB RAM.

## Source inspection result

The existing native library is NOT a replacement for Horizon's renderer. It is a linked terrain renderer plus CPU adapters. The Rust EGL runtime is compiled into the Android cdylib, but no production Game or Launcher call site owns it. The application still constructs and calls horizon_client_render::gfx::Renderer, whose implementation owns a wgpu instance, adapter, surface, device, queue, pipelines, bind groups, textures, buffers, readback, and presentation.

The migration is not a renderer alias. It requires a new backend contract and removal of the concrete wgpu API from Android application call sites.

## Exact public frame contract

The pinned gfx::Frame<'a> has 71 public fields. A complete GLES2 frame adapter must consume these values or explicitly reject unsupported features; it must never silently drop them.

- Camera and transforms: camera, yaw, pitch, fov_degrees, eye, eye_rotation, first_person, bob, spin.
- Sky and dimension: sky, skybox, end_sky, end_sky_sprite, sky_blocked, dark_disc, celestial, stars, title.
- Terrain and visibility: distance, detail_distance, distant_terrain, depth_far, cave_culling, shadow_scene_revision, shadow_cache, chunk_fade.
- Fog and water: underwater, water_plane, environment_fog, water_fog, fog.
- HUD and overlays: hud, outline, outline_after_terrain, outline_high_contrast, debug_lines, debug_lines_on_top, debug_crosshair, screen, menu_blur, menu_blur_cache.
- Entities and effects: entities, glowing, entity_spans, weather, weather_time, dynamic_lights, colored_lights, emissive_layers, clouds, lightning, additive, see_through, shadow_only, hand, hand_fov_degrees.
- Materials, pack and animation state: glint_alpha, glint_ticks, nether_lighting, pack, border, game_time, shader_game_time, end_portal_sprites, crumbling_sprite, ticks, darkness, night_vision, block_flicker, boss_darken, post_effects.

## Renderer APIs that must be migrated

### Construction and ownership
- Renderer::new, Renderer::new_with_context, background_context, activate_replacement, release_surface.
- Game factory: build_renderer.
- Loading factory: loading_renderer.
- Launcher has a separate Option<Renderer> and separately constructs the loading renderer.

### Per-frame and world state
- render, set_pass_timing, set_detailed_gpu_timing.
- upload, remove_columns, clear_sections, queue_lods, upload_lods, clear_lods.
- set_world_assets, set_loading_ui, invalidate_menu_blur, has_cached_menu_blur, start_occlusion.

### Surface lifecycle
- suspend_surface, recreate_surface, resize, set_present_mode.
- Android pause/resume in Game and Launcher must use one EGL owner for the actual ANativeWindow; there must not be a wgpu surface and a second EGL window surface competing for it.

### Direct concrete-field dependencies
Game and Launcher currently read or mutate wgpu-backed fields including config, device, hud, gpu_times, info, screenshot, screenshot_result, suspended, drawn_sections, and timing/upload counters. These must become backend-neutral methods or data structures before an Android GLES2 renderer can substitute for the concrete type.

## Required rendering coverage

1. Terrain solid, cutout, translucent, animated atlas layers, chunk fade, LOD, section visibility, cave culling, and ordering.
2. HUD sprite batches, runtime glyph/skin/map patches, text, loading screens, menu backgrounds, panorama and launcher UI.
3. Entities, item/entity glint, first-person hand, armor/item translucency, glowing outlines, name tags and shadow-only geometry.
4. Sky, End sky, stars, sun/moon, dark disc, weather, clouds, lightning, additive geometry, water mirror and water mask.
5. Fog, lightmap, dynamic/colored lights, emissive layers, darkness/night vision, gamma, screen effects, outline and debug geometry.
6. Resource-pack shader compatibility: pack uniforms, shadow inputs, post-effect chains, and a deterministic explicit fallback/error policy for unsupported packs.
7. Screenshots/readback, resize, surface loss, context loss, GPU timing replacement, and frame pacing.

## Android/API-22 compatibility work still required

- Separate Android renderer selection from the desktop wgpu renderer at compile time; merely linking GLESv2 while retaining wgpu does not remove Vulkan/wgpu initialization or memory pressure.
- Move the backend-neutral Frame and CPU scene data contract out of wgpu-owned gfx.rs, or provide a stable adapter crate that does not import wgpu types.
- Replace all Game and Launcher concrete Renderer ownership and direct field access with a backend-neutral trait/facade.
- Convert atlas and terrain uploads to bounded CPU staging with explicit memory budgets; avoid duplicating full atlas/section data for context recovery on a 1 GiB device.
- Verify GLES2 limits (texture dimensions, texture units, index widths, vertex attributes, framebuffer formats) and split atlas pages where required.
- Compile every shader as GLSL ES 1.00; wgpu WGSL pipelines and shader-pack features cannot be assumed to translate automatically.
- Validate native-window reference lifetime, EGL config selection, context loss, pause/resume, surface resize, and GL object destruction on the owning thread.
- Remove wgpu from the Android dependency graph only after all code paths that require its concrete types are feature-gated or replaced.

## Current completion status

- Native GLES2 terrain backend: prototype and linked native code exist.
- Rust EGL owner: prototype lifecycle wrapper exists.
- CPU terrain/HUD adapters: partial.
- Game frame-to-GLES2 adapter: NOT implemented.
- Game renderer factory replacement: NOT implemented.
- Launcher renderer replacement: NOT implemented.
- Full pass coverage listed above: NOT implemented.
- Android wgpu dependency removal: NOT implemented.
- Playable APK and hardware gameplay validation: NOT demonstrated.

This audit deliberately prevents a successful C++/cdylib compile from being reported as a complete renderer port.
