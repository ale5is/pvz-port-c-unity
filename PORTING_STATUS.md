# Estado del port

## Objetivo actual

Backport completo de Native Hooks API 359 de Starling a la base Android Horizon 0.7.8, manteniendo los cambios Android de 0.7.8. No se limita a hacer funcionar Simple Voice Chat.

## Trabajo realizado en esta rama

- [x] Crear rama aislada `port/horizon-078-hooks-359`; no modificar `main`.
- [x] Fijar las revisiones upstream de Horizon 0.7.8 y Starling actual.
- [x] Guardar ambos catálogos `native-hooks.json` como referencias.
- [x] Guardar ambos archivos `native_hooks.rs` como referencias.
- [x] Añadir comparación reproducible de catálogos.
- [x] Confirmar 108 hooks compartidos, 278 añadidos, ninguno eliminado y ningún cambio en nombre/versión/destino de los compartidos.
- [x] Comparar declaraciones `native_*`: 219 métodos en 0.7.8, 498 en Starling; 283 nombres de método añadidos.
- [ ] Importar el árbol completo del código fuente de Horizon Android 0.7.8.
- [ ] Portar las implementaciones de los hooks nuevos y sus tipos auxiliares.
- [ ] Portar los puntos de llamada de los hooks nuevos por subsistemas.
- [ ] Portar el registro/generación del catálogo y las herramientas de `horizon-mixins`.
- [ ] Resolver diferencias Android sin reemplazar Activity, JNI, paquete ni ciclo de vida de 0.7.8.
- [ ] Ejecutar pruebas unitarias y de integración.
- [ ] Compilar ARM64 y validar en dispositivo.
- [ ] Integrar y probar Simple Voice Chat sobre API 359.

## Bloqueo actual

El directorio `sources/horizon-078` todavía contiene solo el `Cargo.toml` raíz; no contiene los crates completos. Por eso todavía no se puede compilar ni portar las funciones de manera verificable. La rama actual es una base de auditoría y referencias, no un APK ni un backport terminado.

## Resultado de validación

No hay build exitoso ni prueba en dispositivo. No declarar compatibilidad API 359 hasta completar las casillas pendientes.
