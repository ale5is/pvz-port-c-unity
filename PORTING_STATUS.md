# Estado del port

## Objetivo

Backport completo de Native Hooks API 359 de Starling a la base Android Horizon 0.7.8, manteniendo el comportamiento Android 0.7.8. El objetivo no es adaptar solo Simple Voice Chat: es ofrecer compatibilidad general con mods que usen API 359.

## Trabajo realizado

- [x] Crear rama aislada `port/horizon-078-hooks-359`; `main` permanece intacta.
- [x] Fijar revisiones exactas de Horizon 0.7.8 y Starling API 359.
- [x] Guardar snapshots de ambos catálogos y de ambos archivos `native_hooks.rs`.
- [x] Añadir comparador reproducible de catálogos.
- [x] Añadir submódulos Git con el árbol fuente completo de Horizon 0.7.8 y Starling API 359.
- [x] Añadir workflow de GitHub Actions para verificar los commits fijados y comparar las declaraciones nativas.
- [x] Comparar los catálogos: 108 compartidos, 278 añadidos, ninguno eliminado; los hooks compartidos conservan versión y destino.
- [x] Comparar declaraciones `native_*`: 219 métodos antiguos, 498 actuales, 283 nombres nuevos. Cuatro helpers antiguos ya no aparecen con el mismo nombre.
- [x] Registrar el único cambio textual detectado en la firma compartida de `dispense_item`.

## Revisiones fijadas

| Fuente | Commit |
|---|---|
| Horizon Android 0.7.8 | `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29` |
| Starling API 359 | `c4e44c1e0df785373572a479a9adb4fb99eff7fb` |

Los submódulos están en `upstream/horizon-078` y `upstream/starling-api359`. Clonar con:

```sh
git clone --recurse-submodules https://github.com/ale5is/pvz-port-c-unity.git
git checkout port/horizon-078-hooks-359
git submodule update --init --recursive
```

## Pendiente: backport real

- [ ] Extraer los cambios de implementación de los 278 hooks nuevos.
- [ ] Portar los 283 métodos nativos nuevos y los tipos auxiliares que requieren.
- [ ] Portar los puntos de llamada del motor por subsistemas: lifecycle, input, render, entidades, inventario, mundo, red y eventos.
- [ ] Portar las modificaciones de `game.rs` sin reemplazar cambios Android específicos de 0.7.8.
- [ ] Portar el registro y generador del catálogo, y alinear `horizon-mixins` con las macros/contratos de API 359.
- [ ] Ejecutar pruebas de cada grupo y resolver errores de compilación.
- [ ] Compilar APK ARM64 conservando paquete, JNI, permisos y ciclo de vida de Horizon 0.7.8.
- [ ] Integrar Simple Voice Chat, validar micrófono, audio de salida, Opus y UDP.

## Estado de validación

La auditoría está guardada y los árboles fuente completos están fijados como submódulos. El workflow de verificación quedó configurado, pero todavía no se ha confirmado una ejecución exitosa. **El backport de API 359 no está implementado ni compilado; no hay APK funcional todavía.**

No se debe cambiar `mod/horizon-native.json` a API 359 ni declarar compatibilidad hasta que el runtime implemente realmente el catálogo y las pruebas pasen.
