# Starling 0.9.0 performance improvements — API 80 port

## Scope

This is an isolated porting branch for evaluating selected Android/runtime and rendering improvements from Starling 0.9.0 on the PavoHorizon base. It is not a Horizon version upgrade and must not change the stable launcher branch.

## Immutable compatibility baseline

- Horizon: `v0.7.8`, commit `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`.
- Horizon API: `80`.
- Keep the PavoHorizon-pinned `horizon-mixins` revision. Do not substitute Starling's newer revision.
- Preserve the existing Android package identity, signing key, ABI selection and native-mod bundle pipeline.
- Preserve the current renderer/backend choices, including the API-80 Vulkan batching/compatibility and OpenGL ES paths where supported.

## Upstream reference

- Starling 0.9.0: `ff303502a4f7982551a0e0d3622d9268efe9e829`.
- Upstream benchmark methodology: `docs/benchmark.md`.
- Upstream native mod build architecture: `docs/starling-native-mods.md`.

Starling 0.9.0 is not a drop-in update for this API-80 tree. The upstream branch has moved substantially; its current Android runtime and renderer modules have been reorganized. Port individual changes only after tracing their dependencies and adapting them to the pinned API.

## Performance changes to evaluate

1. **Paused/background rendering:** avoid rendering the 3D world when a screen or app state does not need it; retain responsive UI, correct resume behavior and audio/input lifecycle.
2. **Frame pacing and limits:** use bounded frame rates for paused/menu states without adding a per-frame busy wait or increasing wakeups.
3. **GPU calibration and device profiles:** evaluate only if the API-80 renderer exposes the same safe backend choices. Keep the existing manual backend selection as a fallback; never silently replace a known-good backend.
4. **Shadow and graphics budgets:** port only bounded resource limits with device-aware defaults. Do not raise memory use on low-RAM devices.
5. **Loading responsiveness:** reduce avoidable work on the UI/render thread; do not parallelize mutable renderer state without ownership and synchronization proof.
6. **Renderer optimizations:** require an isolated benchmark and visual/parity check for every change to chunk culling, entity culling, batching, meshing or GPU submission.

## Performance-preservation gates

Every implementation change must be independently revertible and pass:

- API-80 native compile/check for the target ABI.
- Android APK build and signature verification.
- Existing native mods still included: Simple Voice Chat, Jade and Atlas.
- No change to the Horizon or `horizon-mixins` pins unless separately reviewed and justified.
- Same-device A/B measurements against the unmodified parent build: average FPS, 1% and 0.1% lows, frame-time p50/p95/p99, startup/world-load time, peak/steady RSS, and thermal behavior after a sustained run.
- No accepted performance regression in the primary gameplay scenes. If a feature is slower or increases memory materially, keep it disabled or do not port it.
- Functional checks for pause/resume, app background/foreground, touch/controller input, world save, and renderer/backend switching.

## Current status

- [x] Created this branch from `port/atlas-api80-core`.
- [x] Recorded exact source pins and compatibility constraints.
- [ ] Capture an unmodified API-80 baseline on the target Android device.
- [ ] Map each selected upstream change to its API-80 implementation and dependencies.
- [ ] Port one isolated change at a time.
- [ ] Run compile, APK, functional and same-device performance gates.

No upstream renderer code is copied wholesale in this first step. The benchmark on a desktop PC is not evidence of Android performance.
