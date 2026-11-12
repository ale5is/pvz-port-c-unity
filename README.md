# Horizon Port Pavo

Port de Horizon Android 0.7.8 con objetivo de Native Hooks API 359 y compatibilidad con mods nativos como Simple Voice Chat.

## Base y referencias

- Runtime Android objetivo: Horizon 0.7.8, commit `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`.
- API nativa de referencia: Starling, commit `c4e44c1e0df785373572a479a9adb4fb99eff7fb`.
- Las dos fuentes completas están fijadas como submódulos en `upstream/horizon-078` y `upstream/starling-api359`.

Clonar el proyecto correctamente:

```sh
git clone --recurse-submodules https://github.com/ale5is/pvz-port-c-unity.git
cd pvz-port-c-unity
git checkout port/horizon-078-hooks-359
git submodule update --init --recursive
```

## Auditoría verificada

- Horizon 0.7.8: API 80, 108 entradas de catálogo.
- Starling actual: API 359, 386 entradas de catálogo.
- 108 hooks compartidos, 278 nuevos y ninguno eliminado.
- 219 métodos `native_*` en Horizon 0.7.8 y 498 en Starling; 283 nombres nuevos.
- El catálogo y los métodos no bastan por sí solos: también hay que portar los puntos de llamada del motor y las dependencias.

## Estado

La rama ya incluye una adaptación específica de Simple Voice Chat para Horizon 0.7.8 / API 80: 23 pruebas Linux del crate pasaron, se añadió un puente de posiciones para audio de proximidad y se corrigió el filtrado de comandos. La integración del bundle en el código Horizon y la compilación Android ARM64 siguen en validación.

**El backport general de los 278 hooks API 359 todavía no está implementado. No hay APK funcional ni prueba en dispositivo; no se declara compatibilidad API 359.

## Documentación

- [Estado y tareas pendientes](PORTING_STATUS.md)
- [Auditoría técnica API 359](HOOKS_API_359_PORT.md)
- [Revisiones upstream](UPSTREAM.md)
- [Workflow de auditoría](.github/workflows/native-hooks-audit.yml)
- [Comparador de catálogos](tools/compare_native_hooks.py)
- [Fuentes de referencia](reference/README.md)
