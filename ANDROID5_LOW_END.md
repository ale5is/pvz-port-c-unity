# PavoHorizon Android 5 — adaptación desde Horizon Android 0.7.8 ARMv7

Esta rama se dedica a adaptar Horizon Android 0.7.8 de 32 bits a dispositivos Android 5 de gama baja. No se toma Starling actual ni Horizon Nightly como base del cliente.

## Base de código fijada
- Proyecto: Horizon Android, repositorio `starling-client/starling`.
- Versión: tag `v0.7.8`.
- Commit exacto: `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`.
- Arquitectura: `armeabi-v7a` (Rust target `armv7-linux-androideabi`).
- Comando de compilación upstream: `python tools/android_build.py --abi armeabi-v7a --public`.
- Fuente: https://github.com/starling-client/starling/tree/68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29

La versión 0.7.8 incluye una compilación ARMv7 experimental. Su propio script asigna API 27 a las ABI antiguas (ARMv7 y x86), y el manifiesto requiere OpenGL ES 3.0. La compilación ARM32, por tanto, no significa compatibilidad con Android 5.

## Dispositivo objetivo
- Android 5.1 / API 22.
- ARMv7 de 32 bits (armeabi-v7a).
- 1 GB de RAM.
- GPU Mali-450 MP.
- OpenGL ES 2.0.
- Resolución de referencia: 1280×720.

## Bloqueos verificados
1. **Android API:** Horizon 0.7.8 establece `minSdk=27` para ARMv7. Android 5.1 es API 22. Hay que auditar y reemplazar APIs Java/Android, permisos, servicios y bibliotecas nativas que no existían en API 22.
2. **Gráficos:** el manifiesto de 0.7.8 declara OpenGL ES 3.0 obligatorio. La Mali-450 de referencia solo informa GLES 2.0. El renderer, shaders, formatos, extensiones y límites de textura deben adaptarse; cambiar el manifiesto no crea soporte GLES2.
3. **Audio:** el cliente Android usa AAudio. Debe evaluarse un backend disponible en API 22, como OpenSL ES, o una compilación de diagnóstico sin audio mientras se implementa el backend.
4. **Memoria:** la compilación ARMv7 de 0.7.8 no garantiza que el juego quepa en 1 GB. Hay que reducir atlas, resolución, chunks, workers, cachés y buffers de audio/GPU y medir el pico real.
5. **Dependencias:** Rust, NDK, wgpu/winit y crates nativos deben compilar y enlazar para ARMv7 con un nivel mínimo API 22. Se comprobarán símbolos ELF y llamadas a APIs nuevas.

## Etapas de trabajo
### 1. Reproducir la base original
- Compilar el tag exacto `v0.7.8` para `armeabi-v7a` sin cambiar su mínimo API 27.
- Guardar APK, metadatos, dependencias y logs como baseline.
- No mezclar cambios de Starling posterior ni del port Nightly.

### 2. Compatibilidad Android 5
- Bajar el mínimo de API de forma controlada a 22.
- Reemplazar las llamadas Android incompatibles y probar ciclo de vida, almacenamiento, red, teclado y controles.
- Sustituir AAudio por un backend compatible con API 22.
- Crear una APK de diagnóstico para comprobar instalación y arranque en API 22 antes de integrar el juego completo.

### 3. GLES 2.0
- Auditar el uso de wgpu y el backend GLES de la revisión fijada.
- Determinar si el renderer puede adaptarse a GLES2 o si requiere un backend específico.
- Reducir o reescribir shaders y rutas gráficas que dependan de GLES3.
- Probar en Mali-450 real; no aceptar una compilación exitosa como prueba de renderizado.

### 4. Gama baja
- Aplicar presets de baja memoria y baja resolución.
- Limitar distancia de renderizado y workers.
- Reducir memoria temporal de atlas, chunks, texturas y audio.
- Medir RAM, FPS, tiempos de frame, cierres y temperatura.

### 5. Entrega
- Generar APK `armeabi-v7a` con `minSdkVersion 22`.
- Verificar que no contiene bibliotecas ARM64 ni símbolos Android posteriores a API 22.
- Instalar y probar en Android 5.1: launcher, descarga/preparación, menú, mundo local, controles, guardado, red y audio.
- Publicar logs y limitaciones reales.

## Criterios de aceptación
- Instala y arranca en Android 5.1/API 22.
- Ejecuta en ARMv7 sin bibliotecas ARM64.
- Renderiza en GLES 2.0 real, sin exigir Vulkan ni GLES3.
- No agota la memoria del dispositivo objetivo al abrir un mundo de prueba.
- Se documentan las pruebas físicas; una compilación CI no equivale a validación en teléfono.

## Aislamiento
Todos los cambios específicos de Android 5 permanecen en `port/android-5-low-end`. No modificar ni fusionar automáticamente la rama principal, la rama PC ni la Android moderna.
