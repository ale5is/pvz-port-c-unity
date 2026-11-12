# Estado del port

## Incompatibilidades
1. Horizon 0.7.8 ofrece Native Hooks API 80; Starling actual ofrece 359; el mod requiere 359.
2. Horizon 0.7.8 tiene 108 hooks; Starling actual 386.
3. Hay equivalentes preliminares para world_lifecycle, net_from_server, command y frame; hay que cotejar firmas y semántica.
4. player_submit no tiene equivalente directo en 0.7.8. No reemplazarlo por un hook no relacionado.
5. Horizon 0.7.8 expone native_key(&mut self, key: &str, pressed: bool); el mod espera un evento de teclado más rico. Requiere adaptador.
6. Horizon usa paquete Android dev.horizon.android y HorizonActivity.java; Starling actual usa io.github.juanluci.starling. Hay que conservar la identidad de Horizon 0.7.8.

## Lista de trabajo
- [ ] Importar el código completo del mod y sus recursos/dependencias, incluido Opus.
- [ ] Implementar capa de compatibilidad de hooks para API 80.
- [ ] Resolver player-submit contra el ciclo real de jugador/renderizado de 0.7.8.
- [ ] Adaptar eventos de teclado conservando key-down/key-up.
- [ ] Backport del cargador nativo a HorizonActivity/JNI de 0.7.8.
- [ ] Adaptar validación de metadatos y permisos.
- [ ] Resolver revisiones de horizon-mixins y ABI.
- [ ] Preparar build reproducible con NDK/Rust.
- [ ] Compilar, instalar y probar captura, reproducción, conexión y ciclo de vida.

## Estado de validación
No hay compilación exitosa, instalación ni sesión de voz probada. No considerar el port funcional hasta superar esas pruebas.
