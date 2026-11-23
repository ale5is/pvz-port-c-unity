# PavoHorizon Android 5 — rama experimental de gama baja

Esta rama separa el trabajo de compatibilidad Android antigua del desarrollo Nightly/PC y del port Android moderno.

## Objetivo de hardware
- Android 5.1 / API 22.
- ARMv7 de 32 bits (armeabi-v7a).
- 1 GB de RAM.
- GPU Mali-450 MP.
- OpenGL ES 2.0; no asumir Vulkan ni OpenGL ES 3.
- Resolución de referencia: 1280×720.

El objetivo inicial es comprobar si se puede ejecutar una versión reducida del cliente Rust de Minecraft en este perfil. No se considera compatible hasta producir una APK instalable y superar pruebas reales en el dispositivo.

## Base ARM32 seleccionada
- Proyecto base: [Starling](https://github.com/starling-client/starling), rama `starling`.
- Revisión fijada para esta adaptación: `0e5bb58a44efa8e38ee067172a11e3c573829976`.
- Documento de arquitectura ARMv7: [docs/android-armv7.md](https://github.com/starling-client/starling/blob/starling/docs/android-armv7.md).
- ABI objetivo: `armeabi-v7a`, Rust target `armv7-linux-androideabi`.
- Comando ARMv7 documentado por el proyecto: `python packaging/android/build_apk.py --abi armeabi-v7a --dev`.

Esta es la base correcta de 32 bits: ya incluye el selector de ABI, empaquetado ARM32, runtime Java ARM32, límites de heap para preparación y ajustes del renderizador Android. No se debe reconstruir esa parte desde cero.

**Importante:** la propia documentación de Starling indica Android 8/API 26 como mínimo para ARMv7. Por eso se toma su implementación ARM32 y se adapta; no se considera que el APK ARMv7 actual funcione en Android 5.

## Estado de compatibilidad y bloqueos
1. El script de empaquetado de Starling fija `MIN_SDK = 26` explícitamente por AAudio (CPAL). Hay que sustituir esa ruta de audio por una compatible con API 22 (por ejemplo, un backend Android antiguo como OpenSL ES, si la versión de CPAL/dependencias lo permite) o compilar una variante sin audio como etapa intermedia.
2. El manifiesto Android moderno declara Vulkan como requisito en su plantilla principal. La variante ARM32 puede ajustar la selección de backend, pero eso no demuestra compatibilidad con la Mali-450.
3. El documento ARMv7 de Starling habla de compatibilidad GLES y de workarounds para drivers antiguos, pero no garantiza OpenGL ES 2.0. La Mali-450 de referencia informa GLES 2.0; se debe auditar el nivel GLSL, las extensiones, texturas, buffers y límites reales antes de activar esa GPU.
4. La versión ARM32 usa un runtime Java 25 preparado para Android moderno. Debe comprobarse por separado si ese runtime y sus bibliotecas pueden ejecutarse en API 22; no basta con que el binario sea ARM32.
5. Con 1 GB de RAM, los límites de heap del runtime Java no limitan la memoria total del proceso Rust/GPU. El cliente completo puede seguir excediendo el presupuesto.

Bajar únicamente `minSdkVersion` no convierte el motor en compatible.

## Plan de adaptación
### Etapa 1 — fijar y compilar la base ARM32
- Partir de la revisión Starling indicada arriba.
- Mantener el ABI `armeabi-v7a` y el target Rust `armv7-linux-androideabi`.
- Crear un build de referencia ARMv7 con su mínimo Android original (API 26), para separar errores de arquitectura de los errores de compatibilidad API 22.
- No cambiar todavía el mínimo SDK en todo el proyecto.

### Etapa 2 — llevar la plataforma a API 22
- Auditar APIs Android, NDK, crates Rust y bibliotecas nativas que requieran API superior a 22.
- Resolver primero el audio AAudio/CPAL mediante backend compatible o variante sin audio.
- Revisar clases Java, métodos Android, permisos, servicios, notificaciones, almacenamiento y llamadas JNI introducidas después de API 22.
- Verificar el runtime Java ARM32 y la preparación de recursos; sustituirlo si no admite Android 5.
- Crear una APK mínima de diagnóstico con minSdk 22 para probar arranque, ciclo de vida, almacenamiento privado, red y entrada táctil.

### Etapa 3 — GPU y presupuesto de memoria
- Auditar si wgpu/GLES de esta revisión puede crear un dispositivo real GLES 2.0; no simular compatibilidad bajando el requisito del manifiesto.
- Si GLES2 no es viable con el renderer actual, evaluar un backend GLES2 específico o detener esa ruta antes de integrar el juego.
- Reducir resolución interna, distancia de renderizado, cachés y carga simultánea de chunks.
- Establecer límites de memoria y cierre controlado ante falta de recursos.

### Etapa 4 — integración del cliente
- Integrar el cliente completo solo cuando plataforma, runtime Java, audio y GPU hayan superado las pruebas.
- Mantener la compatibilidad de mundos y protocolos que permita la revisión elegida.
- Compilar APK ARMv7, instalarla en Android 5.1 y probar menú, mundo local, controles, audio y red.

## Criterios de aceptación
- La APK instala en Android 5.1 sin modificar el sistema.
- La aplicación arranca con ABI ARMv7 y no carga bibliotecas ARM64.
- No depende de Vulkan ni de GLES3 si el dispositivo solo ofrece GLES2.
- Puede abrir un mundo de prueba sin agotar la memoria.
- Se publican logs, métricas de RAM/FPS y limitaciones observadas en el dispositivo real.

## Regla de seguridad del desarrollo
No cambiar ni fusionar automáticamente la rama principal, la rama PC ni la Android moderna. Todo cambio específico de Android 5 permanece en esta rama hasta que las pruebas demuestren que es viable.
