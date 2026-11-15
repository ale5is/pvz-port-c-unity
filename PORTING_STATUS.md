# PavoHorizonNightly — estado del port

## Objetivo

- Horizon Nightly: `9ade5838fb9c2e89c85a914016adcc8ab5faa4b3`
- horizon-mixins: `5a64af938ec86b00d53d16a408aabc0038fb3e57`
- Rama de trabajo: `pavohorizonnightly`
- APK previsto: `PavoHorizonNightly.apk`
- Identificador Android previsto: `dev.pavo.horizonnightly`
- Arquitectura inicial: ARM64 (`arm64-v8a`), perfil optimizado `fast`

## Simple Voice Chat

- El bundle se adaptó a la estructura de Horizon Nightly.
- Se validaron los nueve hooks requeridos.
- CI de compatibilidad: [run 43751754439](https://github.com/ale5is/pvz-port-c-unity/actions/runs/43751754439), resultado satisfactorio.
- Esta validación compila y prueba el mod contra `horizon-client`; **no equivale a una compilación Android ni a una prueba en un teléfono**.

## Port Android

- Starling es la referencia para la capa Android, el empaquetado NativeActivity y la entrada JNI.
- La rama `starling` está sincronizada con Horizon en una revisión anterior (la documentación de Starling registra la sincronización del 2 de noviembre de 2026).
- Se probó la integración temporal de Starling sobre el commit Nightly objetivo, sin modificar la rama estable.
- La fusión directa detectó **129 archivos en conflicto**, incluidos `Cargo.toml`, `Cargo.lock`, `horizon-client`, `horizon-client-render` y `horizon-client-input`.
- No se aplicó una resolución masiva automática: podría reemplazar código reciente del motor, controles o renderizador y causar regresiones.
- El SDK Android, NDK y toolchain Rust ARM64 se instalaron correctamente en CI. La compilación se detuvo en la etapa de integración, antes de compilar la biblioteca nativa o empaquetar el APK.

## Rendimiento y estado de entrega

- No hay todavía un APK Nightly.
- No se ha probado el port Android en un dispositivo.
- No se ha medido FPS, frametime, memoria ni temperatura frente a la versión estable.
- No afirmar que el rendimiento se conserva hasta completar la compilación, las pruebas funcionales y una comparación en el mismo dispositivo.
- La rama estable y su APK no se modifican.

## Siguiente trabajo

Resolver de forma selectiva la integración Android sobre Nightly, conservando las implementaciones actuales del motor cuando no sean necesarias para Android. Después: compilar ARM64 con Simple Voice Chat, verificar firma/paquete/permisos, instalar y probar arranque, mundo local, servidor, controles, micrófono y audio; finalmente medir rendimiento frente a la versión estable.
