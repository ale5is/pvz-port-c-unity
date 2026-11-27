# Android GLES2: renderer extraction boundary

Status: **source-level frame data is separated; the renderer crate is not backend-neutral.**

Upstream is pinned to `starling-client/starling` at `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`.

## What is already separated

The patch pipeline moves `Frame`, `BorderDraw`, `EntitySpan`, lightning input data and `PackInputs` into `horizon-client-render/src/frame.rs`. The file itself contains no `wgpu::` references and no `crate::gfx::` references. This is a useful CPU data boundary, but it is not yet a standalone crate boundary.

## Why a backend trait is not safe yet

`horizon-client-render/Cargo.toml` still has a mandatory `wgpu = 30.0.1` dependency (including Vulkan/Metal/WGSL and GLES features). The extracted frame still refers to types owned by that crate, including `mesher::Vertex`, `hud::HudBatch`, `environment::Sky`, `screen_effects::ScreenQuad`, post-chain request data and pack inputs. Moving only `Frame` to a new crate would create dependency cycles or pull wgpu-owned modules back into the supposedly neutral contract.

The current `gfx::Renderer` owns the wgpu instance, adapter, surface, device, queue, configuration, pipelines, textures, buffers, shader-pack state, screenshots and GPU timing. Its `render(&Frame)` implementation executes the complete render graph. The Game and Android Launcher also share renderer construction and surface lifecycle; replacing only the Game field would leave the Launcher loading UI on the old owner.

## Required extraction sequence

1. Move the vertex/mesh CPU data types and their format definitions out of wgpu implementation modules.
2. Move HUD batches/atlas descriptions, sky/environment frame data, screen-effect inputs, pack inputs and post-chain request data into a contract crate with no wgpu dependency.
3. Make the contract crate compile independently for Android ARMv7/API 22 and desktop.
4. Define an object-safe backend boundary around frame submission, surface lifecycle, streaming uploads/removals, resize, screenshots and timing. Replace generic iterator parameters with slices or explicit owned batches where object safety requires it.
5. Implement the wgpu backend against that boundary without changing desktop behavior.
6. Implement the GLES2 backend against the same frame contract, covering every render pass before selecting it for Android.
7. Give Launcher and Game one transferred render-owner handle. Never let wgpu and EGL independently own the same Android native window.
8. Only after all passes and lifecycle tests pass, select GLES2 for Android and keep wgpu for PC.

## Current proof boundary

The CI checks validate the extracted CPU-only source file, native GLES2 terrain/HUD adapters, EGL lifecycle code and ARMv7 library linkage. They do not establish a complete GLES2 frame renderer, a playable APK or a successful run on the Philips MT5861 TV.

`tools/check_android5_render_crate_boundary.py` intentionally reports the remaining crate-level wgpu dependency as a blocker while passing the current source-level checks. This prevents the extracted file from being described as a fully independent renderer contract.
