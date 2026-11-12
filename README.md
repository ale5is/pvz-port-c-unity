# Horizon Port Pavo

Port nativo de Simple Voice Chat para Horizon Android 0.7.8.

Este repositorio queda dedicado al port. El contenido anterior del proyecto PvZ se reemplaza en la rama principal por este espacio de trabajo.

## Objetivo
Adaptar el mod nativo de Simple Voice Chat para Horizon Android 0.7.8, tomando el cargador nativo de Starling actual como referencia sin actualizar el runtime objetivo.

## Estado
Investigación y preparación inicial. Aún no es un port compilado ni probado en dispositivo.

## Documentación
- [Estado de adaptación](PORTING_STATUS.md)
- [Fuentes y revisiones upstream](UPSTREAM.md)
- [Manifiesto del workspace Simple Voice Chat](sources/simple-voice-chat/Cargo.toml)
- [Manifiesto de Horizon 0.7.8](sources/horizon-078/Cargo.toml)

## Compatibilidad confirmada
- Horizon 0.7.8: Native Hooks API 80, 108 entradas.
- Starling actual: Native Hooks API 359, 386 entradas.
- El mod actual declara API 359: no se puede cargar sin adaptar.
- Falta resolver el hook de player-submit y adaptar el contrato de entrada de teclado.
