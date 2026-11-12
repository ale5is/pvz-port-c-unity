# Fuentes upstream

| Componente | Repositorio / revisión | Uso |
|---|---|---|
| Horizon Android 0.7.8 | https://github.com/starling-client/starling — tag v0.7.8, commit 68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29 | Runtime objetivo |
| Starling actual | https://github.com/starling-client/starling — commit c4e44c1e0df785373572a479a9adb4fb99eff7fb | Referencia del cargador |
| Simple Voice Chat | https://github.com/Sparticle62ops/horizon-simple-voice-chat — árbol main b3bf3b792a04f8053dfcf22ed1e5f879227e6c89 | Implementación a adaptar |
| Horizon mixins | https://github.com/horizon-rust/horizon-mixins | Dependencia de mixins |

## Revisiones requeridas por el mod actual
- Horizon revision: 3fb4c3138e0c5d8fdd2fdb555cf31368e8897497
- horizon-mixins revision: d9c83d7096b7ea5b83678da2efdfd6161f74997e
- Starling build script referencia horizon-mixins: ddd9700b21997c95d95ac6ee5ecafe3830cf622a

## Límite
Los manifiestos copiados son fuentes de referencia. Todavía falta importar el código completo, recursos nativos de Opus y dependencias.
