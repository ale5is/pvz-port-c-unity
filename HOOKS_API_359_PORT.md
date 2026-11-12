# Horizon Port Pavo — Native Hooks API 359

## Objetivo

Actualizar la base Android de Horizon 0.7.8 para que implemente el contrato nativo de Starling API 359, conservando la identidad y las modificaciones Android de Horizon 0.7.8. No se debe limitar el trabajo a cambiar el número de API ni adaptar solamente Simple Voice Chat.

## Revisiones fijadas

| Elemento | Revisión |
|---|---|
| Horizon Android 0.7.8 | `v0.7.8` — `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29` |
| Starling actual | `starling` — `c4e44c1e0df785373572a479a9adb4fb99eff7fb` |
| Catálogo API 80 | `reference/horizon-078/native-hooks.json` |
| Catálogo API 359 | `reference/starling-api359/native-hooks.json` |
| Implementación API 80 | `reference/horizon-078/native_hooks.rs` |
| Implementación API 359 | `reference/starling-api359/native_hooks.rs` |

## Comparación verificada

- API antigua: 80.
- API actual: 359.
- Entradas del catálogo antiguo: 108.
- Entradas del catálogo actual: 386.
- Hooks compartidos: 108.
- Hooks añadidos: 278.
- Hooks eliminados: 0.
- Hooks compartidos cuyo `version` o `target` cambió: 0.

Los 108 hooks compartidos conservan el mismo nombre, versión y destino en los dos catálogos. Starling agrega metadatos `mod_api`; eso no se interpreta como cambio de destino o versión del hook.\n\nComparación adicional de `native_hooks.rs`: el análisis de declaraciones encontró 219 métodos `native_*` en 0.7.8 y 498 en Starling; 215 nombres aparecen en ambos y 283 nombres de métodos son nuevos. Cuatro métodos auxiliares antiguos no aparecen con el mismo nombre en Starling (`entities_left`, `items_left_since`, `orbs_left_since`, `entity_joins`); no son entradas del catálogo público. Entre los métodos compartidos se detectó una sola diferencia textual de firma (`dispense_item`), que usa nombres de tipos completamente cualificados en 0.7.8 y aliases en Starling; debe verificarse la resolución de esos tipos al portar.

## Hallazgo crítico de implementación

El catálogo no es la implementación completa. El archivo `native_hooks.rs` creció de 221 KB a 455 KB y `game.rs` también cambió sustancialmente. Además, el árbol actual incluye nuevos archivos y puntos de integración, por ejemplo `crates/horizon-client/src/native_client_hooks.rs`, que no existe en el tag 0.7.8.

Por eso, el port requiere llevar al código 0.7.8:
1. Las nuevas funciones nativas y sus tipos auxiliares.
2. Los puntos de llamada en el ciclo del juego, render, entrada, red y eventos de servidor.
3. Los cambios de módulos y dependencias que esas funciones necesitan.
4. El registro/generador del catálogo y el contrato que valida `horizon-mixins`.
5. Las pruebas de cada familia de hooks.

No se debe copiar el `game.rs` completo de Starling sobre 0.7.8: el proyecto divergió en miles de commits y ese archivo contiene cambios no relacionados, incluidos cambios Android. El port debe ser selectivo y compilable por etapas.

## Comprobación reproducible

Desde la raíz del repositorio:

```sh
python tools/compare_native_hooks.py reference/horizon-078/native-hooks.json reference/starling-api359/native-hooks.json
```

El script valida duplicados, adiciones, eliminaciones y cambios en `version`/ `target`.

## Estado real

Esta rama contiene los catálogos y archivos de implementación upstream fijados como referencia y una comparación reproducible. **Todavía no contiene el backport de los 278 hooks al código fuente completo de Horizon 0.7.8 ni un APK compilado.**

El árbol completo del tag 0.7.8 ya está disponible en el submódulo `upstream/horizon-078`; Starling API 359 está en `upstream/starling-api359`. El siguiente paso del backport general sigue siendo portar las implementaciones y puntos de llamada por grupos, ejecutando pruebas después de cada grupo. La adaptación específica de Simple Voice Chat se mantiene separada en `mod/simple-voice-chat` y usa API 80.

## Criterio de aceptación

- API declarada: 359.
- Catálogo: 386 entradas.
- 108 hooks originales conservados sin cambios de contrato.
- 278 hooks nuevos con implementación y punto de llamada funcionales.
- Pruebas de API y build ARM64 satisfactorias.
- Android 0.7.8 conserva su paquete, Activity, permisos y ciclo de vida.
- Simple Voice Chat compila y funciona con captura, reproducción y UDP.
