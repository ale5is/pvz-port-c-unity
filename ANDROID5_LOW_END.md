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

## Base de esta rama
- Repositorio: ale5is/pvz-port-c-unity.
- Rama base: pavohorizonnightly.
- Esta rama: port/android-5-low-end.
- La integración Android moderna de Starling/Horizon se usa como referencia de arquitectura, no como código que se pueda copiar directamente y esperar que funcione en Android 5.

## Bloqueos conocidos antes de programar
1. El port Android moderno de Starling/Horizon declara Android 8 o posterior y ABI ARM64. No satisface API 22 ni ARMv7.
2. La ruta Android actual de PavoHorizonNightly usa Android NDK y empaquetado con minSdkVersion 26, y compila aarch64-linux-android; tampoco sirve como APK para Android 5.
3. Mali-450 MP no ofrece Vulkan y el dispositivo de referencia reporta OpenGL ES 2.0. No se puede dar por hecho que el renderizador moderno basado en Vulkan/GLES3 arranque.
4. Con 1 GB de RAM, el cliente completo puede exceder el presupuesto incluso si se resuelve la compatibilidad de API y GPU.

Estos puntos son límites técnicos que deben resolverse o demostrarse con pruebas; bajar únicamente minSdkVersion no convierte el motor en compatible.

## Plan por etapas
### Etapa 1 — auditoría sin cambiar el motor
- Identificar APIs Android, NDK, crates Rust y dependencias que exigen API superior a 22.
- Auditar el renderizador: Vulkan, GLES3 y cualquier ruta GLES2 real.
- Auditar ABI ARMv7, atomics, alineación, dependencias nativas y bibliotecas de audio.
- Medir el consumo mínimo de memoria de launcher, preparación de recursos y mundo vacío.
- Generar un informe reproducible y una matriz de compatibilidad.

### Etapa 2 — prueba mínima de plataforma
- Crear una APK de diagnóstico con minSdkVersion 22 y ABI armeabi-v7a.
- Verificar arranque, ciclo de vida, almacenamiento privado, red y entrada táctil/control.
- No incluir todavía el motor completo si las dependencias no pasan la auditoría.

### Etapa 3 — renderizado y presupuesto de memoria
- Probar una ruta GLES2 real antes de portar el juego completo.
- Reducir resolución interna, distancia de renderizado, cachés y carga simultánea de chunks.
- Evitar shaders/efectos y funciones gráficas que el Mali-450 no soporte.
- Establecer límites de memoria y cierre controlado ante falta de recursos.

### Etapa 4 — integración del cliente
- Portar solo las partes del motor que superen las etapas anteriores.
- Mantener la compatibilidad de mundos y protocolos que permita la revisión elegida.
- Compilar APK ARMv7, instalarla en Android 5.1 y probar menú, mundo local, controles, audio y red.

## Criterios de aceptación
- La APK instala en Android 5.1 sin modificar el sistema.
- La aplicación arranca y no depende de Vulkan ni de GLES3 si el dispositivo solo ofrece GLES2.
- No requiere bibliotecas ARM64.
- Puede abrir un mundo de prueba sin agotar la memoria.
- Se publican logs, métricas de RAM/FPS y limitaciones observadas en el dispositivo real.

## Regla de seguridad del desarrollo
No cambiar ni fusionar automáticamente la rama principal, la rama PC ni la Android moderna. Todo cambio específico de Android 5 permanece en esta rama hasta que las pruebas demuestren que es viable.