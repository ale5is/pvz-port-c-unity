# Android GLES2: renderer extraction boundary

Status: **the frame contract and GPU-independent vertex-format crate are extracted; the renderer crate is not backend-neutral.**

Upstream is pinned to `starling-client/starling` at `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`.

## What is already separated

The patch pipeline moves `Frame`, `BorderDraw`, `EntitySpan`, lightning input data and `PackInputs` into `horizon-client-render/src/frame.rs`. It also extracts the shared `Vertex` and `TerrainVertex` GPU-upload layouts, their constants and UV conversion methods, the plain-value `Sky` frame data, and the screen-effect `ScreenVertex`/`ScreenQuad` batch contract, and the HUD `HudVertex`, blend modes and `EndPortalFill` primitives into the independent `horizon-render-types` crate. That crate depends only on `bytemuck`; it has no wgpu, window, EGL or platform dependency. `horizon-client-render` re-exports the types through `mesher` to preserve existing call sites.

## Why a backend trait is not safe yet

`horizon-client-render/Cargo.toml` still has a mandatory `wgpu = 30.0.1` dependency (including Vulkan/Metal/WGSL and GLES features). The extracted frame still refers to types owned by that crate, including `mesher::Vertex`, `hud::HudBatch`, `environment::Sky`, `screen_effects::ScreenQuad`, post-chain request data and pack inputs. Moving only `Frame` to a new crate would create dependency cycles or pull wgpu-owned modules back into the supposedly neutral contract.

The current `gfx::Renderer` owns the wgpu instance, adapter, surface, device, queue, configuration, pipelines, textures, buffers, shader-pack state, screenshots and GPU timing. Its `render(&Frame)` implementation executes the complete render graph. The Game and Android Launcher also share renderer construction and surface lifecycle; replacing only the Game field would leave the Launcher loading UI on the old owner.

## Window-generation contract added to the backend API

The independent `horizon-render-api` crate now defines `WindowGeneration` and `WindowGenerationClock`. Generation values are non-zero, start at 1, advance strictly monotonically, and allow callers to accept only work tagged for the active native-window lifetime. Unit tests cover stale/future rejection, zero rejection and rollback prevention. This is a reusable contract only: the Game upload queue and live EGL runtime are not yet wired to this clock, so it does not by itself prevent stale work in the running game.

## Required extraction sequence

1. **Completed:** move `Vertex`, `TerrainVertex`, culling constants, UV format definitions, `Sky`, `ScreenVertex`, `ScreenQuad`, screen blend data, `HudVertex`, HUD blend modes and `EndPortalFill` into `horizon-render-types`; verify upload layouts and UV packing with unit tests.
2. Move complete HUD batches/atlas descriptions (including font patches and inline profiles), pack inputs and post-chain request data into neutral data crates.
3. Compile the complete frame-contract crates independently for Android ARMv7/API 22 and desktop.
4. Define an object-safe backend boundary around frame submission, surface lifecycle, streaming uploads/removals, resize, screenshots and timing. Replace generic iterator parameters with slices or explicit owned batches where object safety requires it.
5. Implement the wgpu backend against that boundary without changing desktop behavior.
6. Implement the GLES2 backend against the same frame contract, covering every render pass before selecting it for Android.
7. Give Launcher and Game one transferred render-owner handle. Never let wgpu and EGL independently own the same Android native window.
8. Only after all passes and lifecycle tests pass, select GLES2 for Android and keep wgpu for PC.

## Current proof boundary

The CI checks validate the extracted CPU-only frame source, the new wgpu-free render-types crate and its vertex/screen/HUD layout tests, native GLES2 terrain/HUD adapters, EGL lifecycle code and ARMv7 library linkage. They do not establish a complete GLES2 frame renderer, a playable APK or a successful run on the Philips MT5861 TV.

`tools/check_android5_render_crate_boundary.py` intentionally reports the remaining crate-level wgpu dependency as a blocker while passing the current source-level checks. This prevents the extracted file from being described as a fully independent renderer contract.


## Renderer status extraction

The next migration step adds `RendererMetrics` to `horizon-render-api`. The wgpu adapter now reports drawable dimensions, suspended state, rendered-section count, pending-screenshot state, last GPU/display wait in nanoseconds, and the count of unread GPU timing samples through a backend-neutral value type. The Game call sites use this snapshot for window sizing, automatic world-icon screenshot gating, upload budgeting, and Android pause checks. Screenshot requests are sent through a method rather than writing the renderer's private request slot directly.

This is deliberately not presented as full backend polymorphism: the Game still has a concrete `Renderer` owner, GPU adapter information and timing-vector draining still need extraction, and loading/world asset handoff still uses wgpu-owned resources. The snapshot is a narrow seam that can be implemented by GLES2 without inventing fake wgpu fields.


## Adapter capabilities and GPU timing extraction

The Game no longer needs the concrete wgpu::AdapterInfo to apply its integrated-GPU LOD distance cap. The wgpu adapter now publishes a neutral integrated_gpu capability, and the graphics policy accepts that capability rather than a wgpu type. Adapter name and backend label are exposed through BackendInfo for diagnostics. GPU timing samples are consumed through take_gpu_time_ms(), which preserves the previous latest-sample-then-clear behavior without exposing the internal vector.

This removes the Game's direct adapter-info and GPU-timing reads. It does not yet remove wgpu from the renderer implementation or the Game's resource-construction path.


## HUD atlas ownership extraction

The existing renderer-hud-boundary patch already provides Renderer::configure_hud and migrates initial world construction, loading renderer setup, and both renderer replacement handoff paths. It preserves the loading renderer's separate text-shader behavior and keeps wgpu Device/HUD fields private to the renderer. No second HUD adapter patch is applied.


## Existing Game-side renderer boundary reused

The Android HUD-boundary patch already extracts the LOD cap, framebuffer size, HUD setup, screenshot result/request, wait durations, GPU timing drains, adapter labels, GL/Vulkan selection, suspension and renderer handoff behind renderer methods. The capability patch now builds on those methods instead of duplicating them. It adds integrated-GPU and backend identity fields to the neutral API and checks that Game no longer reads those wgpu-owned fields directly.


## Terrain streaming backend contract

The backend API now includes a separate `TerrainStreamBackend<Mesh>` contract with a stable section key and explicit operations for section replacement, column eviction, LOD-detail revision queries and occlusion updates. The wgpu renderer implements this contract by delegating to its existing methods; the camera crosses the API as three scalar coordinates rather than leaking glam or wgpu types.

This is an incremental extraction of the real streamed-world operations, not yet a backend switch. The Game still stores the concrete wgpu `Renderer`, and the native GLES2 implementation does not yet implement this trait. The API and adapter are checked in the Android diagnostic workflow before the ARMv7 build.
