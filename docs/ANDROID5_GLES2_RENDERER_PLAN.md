# Android 5 GLES2 renderer plan — Horizon 0.7.8

Status: design/audit only. No GLES2 renderer has been implemented yet.

## Fixed target and evidence

- Horizon source: `horizon-rust/horizon`, commit `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29` (0.7.8).
- Device: Philips QM152E, Android 5.1 / API 22, ARMv7, Mali-450 MP, 1 GB RAM, 1280x720.
- Device probe: OpenGL ES 2.0, GLSL ES 1.00; GLES 3 EGL config selection failed with `0x3004`.
- Reported limits: max texture size 4096, fragment texture units 8, vertex texture image units 0, vertex attributes 16, varying vectors 12.
- RGBA8 framebuffer: complete.
- The TV has passed the complete expanded GLES2 probe: GLSL ES 1.00 compile/link, RGBA texture upload, VBO upload, RGBA8 + Depth16 FBO, real Depth16 comparison, textured HUD quad, alpha blending, scissor clipping, two-quad batching in one VBO upload/draw call, and all four atlas UV quadrants. The user confirmed that the new tests display PASS on the Philips QM152E.

## Source architecture audit

The pinned `horizon-client-render` crate is not a thin renderer wrapper. Its public module surface includes world data and presentation systems such as `mesher`, `chunk`, `light`, `lod`, `particles`, `font`, `hud`, `screen_draw`, `graphics_ui`, `post_chain`, `skins`, `block_entity_render` and `gfx`.

The central GPU implementation is `src/gfx.rs` (about 441 KB in this revision). It owns terrain, sky, block outline and HUD GPU rendering and is built around wgpu types: `Device`, `Queue`, `TextureView`, `BindGroupLayout`, `RenderPipeline`, command encoders and WGSL shader modules. The crate directly depends on wgpu 30.0.1 with GLES enabled on non-macOS targets.

The existing `gfx/terrain_source.rs` is a GLES lowering path for the current wgpu backend. Its comments explicitly target GLES 3.0 constraints and it still emits WGSL for Naga/wgpu; it is not a GLSL ES 1.00 backend. `gfx/entity_source.rs` similarly rewrites selected WGSL constructs for GLES linker quirks, not for GLES2.

The UI drawing layer (`screen_draw.rs`) already represents many screen operations as CPU-side draw commands (sprite, blit, fill, highlight, text, scissor and tooltip). This is a useful conceptual boundary for a future GLES2 UI renderer, but it does not currently provide a standalone GLES implementation.

## Confirmed and unconfirmed device capabilities

Confirmed by the first TV report:
- GLES2 context and GLSL ES 1.00 are available.
- RGBA8 color texture attached to an FBO is complete.
- At least 8 fragment texture units and 16 vertex attributes are reported.
- No vertex texture fetch units are available.
- The attempted GLES3 EGL config was rejected.

Confirmed on the TV by the user's diagnostic reports:
- Basic vertex/fragment GLSL ES 1.00 compile and link.
- RGBA texture upload and VBO buffer upload.
- RGBA8 + Depth16 FBO completeness and a real depth comparison verified by glReadPixels.
- A textured 2D atlas quad rendered from a VBO and passed its center-pixel check.

Still not confirmed on the TV:
- Stencil8 attachment completeness and stencil operations.
- Long-session frame time, thermal behavior, memory use and surface recreation.
- Glyph atlas rendering and the complete Horizon screen-draw command set.
- Integration of any of these operations with Horizon's actual HUD batch data.

The new tests are independent capability checks; they do not imply integration with Horizon's HUD renderer.

## Recommended implementation strategy

Do not try to make wgpu 30.0.1's GLES backend pretend to be GLES2. Its GLES path is a GLES3 backend, and the current renderer's WGSL pipeline is not a direct GLSL ES 1.00 execution path.

Keep the existing wgpu renderer intact for supported platforms. Add an isolated Android-only GLES2 renderer crate/module, behind an explicit build feature and target cfg, rather than scattering GLES2 conditionals through the existing wgpu code.

### Phase A — minimal renderer proof

1. Create EGL/GLES2 context and lifecycle tied to the Android surface.
2. Implement a tiny GLSL ES 1.00 program, vertex/index buffers and a textured quad.
3. Render to the default framebuffer and to an RGBA8 FBO.
4. Add depth testing only after the extended probe confirms Depth16.
5. Validate alpha blending, scissor, multi-quad batching and atlas UVs with offscreen FBO pixel readback. (Completed on the TV.)
6. Measure CPU frame time, GPU frame time where available, memory and stability on the TV.

Acceptance: stable textured quad and depth-tested geometry on the actual TV, no GL errors, repeatable surface recreation.

### Phase B — 2D/HUD path

Use the existing CPU-side screen draw concepts as a reference. Implement:
- one or a small number of 2D texture atlases;
- batched quads with position, UV and color;
- text glyph atlas and scissor rectangles;
- explicit alpha blending;
- no post-processing dependency.

Acceptance: title/menu/HUD prototype rendered on device with bounded draw calls and memory.

### Phase C — world terrain prototype

Before porting all of `gfx.rs`, define a narrow CPU-to-GPU interface that accepts already-built mesh vertices and indices. Then implement:
- GLES2-compatible vertex layout (no integer vertex attributes; encode discrete values as normalized floats/bytes where safe);
- atlas-based texture selection (2D atlas or multiple 2D textures; no texture arrays);
- GLSL ES 1.00 vertex/fragment shader pair;
- depth buffer after validation;
- opaque terrain first, then cutout and translucent passes.

Keep meshing, chunk data and world logic on CPU where possible. Avoid porting shader packs, HDR, OIT, GPU occlusion, frame generation, compute-like passes and advanced post effects in the first milestone.

Acceptance: one chunk with opaque and cutout blocks, correct depth, texture selection and basic lighting.

### Phase D — incremental parity

Add entities, block entities, particles, weather, sky, hand, fonts, GUI and translucent materials one subsystem at a time. Each stage needs a small deterministic scene and visual comparison against the existing renderer.

## Performance constraints for the 1 GB TV

- Start with 640x360 internal resolution, upscale to 1280x720; compare quality and frame time before increasing.
- Start with render distance 2 chunks; expose a low cap rather than inheriting desktop defaults.
- Use a strict texture/atlas memory budget and release staging buffers after upload.
- Limit worker threads based on available cores and memory; do not assume more workers improve performance.
- Prefer static/dynamic VBO reuse and batch draws; avoid per-frame allocation.
- Keep optional visual systems disabled initially: HDR, shader packs, distant terrain, GPU occlusion, frame generation, expensive post-processing and high-cost transparency.
- Record peak process RSS/PSS and frame-time percentiles on hardware; CI build success is not performance evidence.

## Risks and open questions

- Android API22 native compatibility is separate from GLES2 compatibility; the existing API22 APK is a packaging/launch diagnostic only.
- Audio still needs a legacy Android backend (AAudio is not available on API22).
- Depth16 and the expanded shader/texture/blend/scissor/batching tests are confirmed on the TV; Stencil8 remains untested.
- GLES2 has no vertex texture sampling in this device's reported limits; any current rendering data sourced from vertex-stage texture reads must be moved to CPU or fragment stage.
- The source renderer contains extensive advanced features; a full parity port is a substantial project. The first realistic goal is a minimal world renderer, not full feature parity.

## Pinned source references

- Horizon 0.7.8 renderer crate: https://github.com/horizon-rust/horizon/tree/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render
- Main GPU renderer: https://github.com/horizon-rust/horizon/blob/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render/src/gfx.rs
- GLES terrain lowering (GLES3/WGSL, not GLES2): https://github.com/horizon-rust/horizon/blob/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render/src/gfx/terrain_source.rs
- CPU-side GUI draw commands: https://github.com/horizon-rust/horizon/blob/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29/crates/horizon-client-render/src/screen_draw.rs


## Actualización de diagnóstico en el televisor (23-11-2026)

El usuario ejecutó el APK de pruebas ampliado en el Philips QM152E y entregó el informe completo. Las pruebas realizadas en el dispositivo aprobaron:

- GLSL ES 1.00 compile/link.
- Carga de textura RGBA 2x2.
- VBO + `glBufferData`.
- FBO RGBA8 + Depth16.
- Prueba real de profundidad con lectura central `0,255,0,255` (verde), confirmando que el objeto cercano prevalece sobre el lejano.

Datos relevantes: ARM / Mali-450 MP, GLES 2.0, GLSL ES 1.00, textura máxima 4096, 8 unidades de textura fragmentaria, 16 atributos, 12 varyings, framebuffer por defecto con profundidad de 24 bits y memoryClass/heap máximo de 192 MB.

La prueba ampliada confirma Depth16 y depth testing real. No confirma Stencil8, RGB upload en esta ejecución, rendimiento del juego ni compatibilidad del renderer Horizon completo. La lista de extensiones tampoco sustituye una prueba funcional de cada formato.

Auditoría adicional del código fuente:
- `HudBatch` y `screen_draw::Draw` son interfaces CPU reutilizables para un backend independiente.
- `HudRenderer` sigue siendo wgpu y no es un backend GLES2.
- El atlas HUD actual admite hasta 16384, por encima del límite 4096 del TV; el backend Android 5 debe imponer un límite propio.
- El terreno usa texture arrays, atributos enteros y varyings `flat`; se necesita convertir atributos y capas en CPU y usar atlas 2D para GLES2.

La auditoría detallada y la secuencia propuesta (HUD primero, terreno después) están en [ANDROID5_GLES2_AUDIT_2026-11-23.md](ANDROID5_GLES2_AUDIT_2026-11-23.md).


## Actualización tras la prueba ampliada en el Philips (24-11-2026)

El usuario confirmó que todos los tests nuevos aparecen como `PASS` en el televisor. Quedan confirmadas las operaciones de alpha blending (`SRC_ALPHA`, `ONE_MINUS_SRC_ALPHA`), scissor con comprobación dentro/fuera, dos quads con una sola carga VBO y una sola llamada de dibujo, y muestreo de los cuatro cuadrantes UV del atlas 2D. Esto cierra la puerta de validación de capacidades GLES2 básicas para iniciar el prototipo HUD independiente; no equivale todavía a integración con Horizon ni a una prueba de rendimiento sostenido.


## Implementación inicial del adaptador de terreno (25-11-2026)

Se añadió patches/horizon-078/gles2-terrain-adapter.py. Durante el build instala horizon_client_render::gles2_terrain en el Horizon fijado y convierte cada TerrainVertex CPU a un formato sin atributos enteros ni texture arrays:

- posición XYZ y UV local como float;
- origen y escala del sprite dentro de una cuadrícula de atlas 2D;
- color RGBA como cuatro bytes;
- luz de cielo y bloque como dos bytes;
- marcador de capa opaca como float;
- conserva los grupos solid y translucent por separado.

La cuadrícula calcula columnas y filas respetando el límite de textura GLES2 y los 15 bits que Horizon deja disponibles para el índice de sprite (el bit alto identifica la capa opaca). Se rechazan capas fuera del conjunto cargado y configuraciones que excedan el límite de textura.

Este es el primer adaptador de malla real del terreno; no es todavía el cargador/empacador de píxeles del atlas, el shader GLSL ES 1.00, la subida VBO ni el renderer EGL. La ruta wgpu de Horizon sigue siendo la que crea el launcher y el juego, por lo que este módulo todavía no se ejecuta durante una partida.

El workflow Android 5 ahora instala y ejecuta sus pruebas unitarias junto a las del adaptador HUD. La aceptación de esta etapa requiere que el CI compile el módulo contra el commit exacto de Horizon. La integración visual en el televisor seguirá siendo una etapa posterior.


### Actualización del adaptador de terreno: empaquetado de texturas

El adaptador ahora también copia las capas RGBA base de Horizon a un atlas 2D compacto, en orden de capa y sin reservar automáticamente todo el ancho máximo de la GPU. La cuadrícula intenta mantener una forma aproximadamente cuadrada y valida el tamaño de entrada, los cálculos de memoria y el límite de 4096 px. Se agregó una prueba de ubicación de píxeles para verificar que las capas 0, 1 y 2 terminan en los tiles esperados.

El empaquetador trabaja con el bloque RGBA base layer-major que Horizon entrega a su preparación de texturas; todavía no reproduce los mipmaps ni el sistema de animación de sprites. El renderer GLES2 tendrá que actualizar las regiones de atlas cuando cambien las texturas animadas y aplicar un margen de muestreo para evitar bleeding entre tiles.


### Prototipo inicial de shaders GLES2 para terreno (25-11-2026)

Se añadieron tres fuentes GLSL ES 1.00 independientes:
- `gles2-terrain.vert`: recibe el layout CPU convertido (posición, UV, origen/escala de atlas, color, luz y marca opaca) y calcula posición/profundidad de cámara.
- `gles2-terrain-solid.frag`: muestrea el atlas y lightmap, aplica alpha cutout para hojas/vegetación y niebla lineal.
- `gles2-terrain-translucent.frag`: conserva alpha y separa el descarte de píxeles casi transparentes para el pase translúcido.

La fórmula de repetición UV se ajustó a la función `tile_uv` exacta del WGSL de Horizon; no se usa `fract` porque en UV enteras (por ejemplo 1.0) produciría un borde distinto. El adaptador CPU también expone ahora un paso UV explícito: 1/32768 para secciones normales y 1/32 para mallas de terreno distante. Así no se confunden las dos codificaciones fijas de Horizon.

Se añadió `tools/check_gles2_terrain_shaders.py` al CI para verificar que los atributos del shader coincidan con el adaptador y que los pases sólido/translúcido mantengan contratos separados. Este control es estático: no es un compilador GLSL, no ejecuta OpenGL y no demuestra que el shader se vea correctamente en el Philips.

Pendiente antes de integrar:
- compilar/linkar ambos programas en el dispositivo con el contexto GLES2 real;
- implementar la creación de VBO y los punteros de atributos usando offsets/stride exactos de `Gles2TerrainVertex`;
- confirmar la orientación y contenido del lightmap cargado;
- enlazar matrices, origen relativo, niebla y atlas desde el renderer;
- implementar EGL, carga de texturas, subida de mallas y los pases de profundidad/mezcla;
- manejar sprites animados y sus actualizaciones del atlas.

Por tanto, estos shaders son una primera implementación fuente del camino de terreno, pero aún no se ejecutan en Horizon ni forman parte de un APK jugable.
