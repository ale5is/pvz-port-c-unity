# Android 5 GLES2 probe — HUD raster test matrix

Target: Philips QM152E, Android 5.1/API 22, ARMv7, Mali-450 MP, OpenGL ES 2.0 / GLSL ES 1.00.

This probe is a hardware capability test. It is not the Horizon renderer and does not claim that Horizon's HUD or world renderer has been integrated.

## Tests already reported PASS on the TV

- GLSL ES 1.00 vertex/fragment shader compile and link.
- RGBA texture upload.
- VBO allocation and data upload.
- RGBA8 color target + Depth16 renderbuffer framebuffer completeness.
- Real depth comparison: draw the nearer green quad first, then the farther blue quad; read the center pixel and verify green remains visible.
- Textured 2D atlas quad using an interleaved position/UV VBO, followed by center-pixel readback.

The user reported the visible scene as a blue outer square with a green center and the new textured VBO test as PASS.

## Expanded tests — confirmed PASS on the Philips QM152E (24-11-2026)

| Test | Operation | Pixel-level acceptance |
|---|---|---|
| Alpha blending | Clear an RGBA8 FBO blue, then draw red at 50% alpha with SRC_ALPHA / ONE_MINUS_SRC_ALPHA. | Center pixel approximately (128,0,127); tolerance is used for driver rounding. |
| Scissor | Clear black, enable an 8×8 scissor rectangle inside a 16×16 FBO, then clear green. | Pixel inside is green; pixel outside remains black. |
| Multi-quad batch | Upload two differently colored quads in one VBO and issue one glDrawArrays call for 12 triangle vertices. | Left sample is red and right sample is cyan. |
| Atlas UV quadrants | Upload a 2×2 atlas (red, green, blue, white), draw one textured quad and sample all four quadrants. | Four readbacks match the four source colors, verifying UV interpolation and orientation. |

Every test is run on the GL thread and uses a small 16×16 offscreen target. The user has now confirmed that all four expanded tests display PASS on the actual TV. These are correctness probes, not performance benchmarks.

## What a PASS does and does not mean

A PASS means the tested GLES2 operation worked on the current device/driver and the specified pixel assertion succeeded. It does not prove:

- Horizon's actual HudBatch or HudRenderer is connected to this code;
- text glyph atlases, blend-mode variants, tooltips or the complete screen-draw command set work;
- frame time, thermal stability or memory usage are acceptable in a long session;
- the terrain renderer, texture paging, entities, particles or audio work.

## Next integration gate

With the expanded tests now passing on the Philips TV, implement a separate, Android-only GLES2 HUD prototype with bounded reusable VBO storage, 2D atlas sampling, alpha blending and scissor. Keep the existing wgpu renderer unchanged for other platforms. Only then adapt Horizon's CPU-side HUD command/batch data through a narrow adapter.
