# Respuesta al análisis de Claude (abr. 2026)

Este documento contrasta los hallazgos del código con correcciones aplicadas en el mismo repositorio.

## ✅ Diagnóstico acertado (ya estaba o reconocido)

- Facturación con CAE, cobranzas, ARCA desde BD y SQL parametrizado en búsquedas sensibles era coherente con la versión actual.
- **README_CAMBIOS §16** mencionaba `Informe*Control*.xaml` que **no existían**: el texto se **actualizó** para describir solo `InformesControl` y aclarar el alcance real.
- **Bypass `admin`** (login sin contraseña y sesión forzada) era real y **grave para producción** — ver abajo (**corregido**).
- **Libro IVA con /1.21** sobre el total de la factura **ignoraba alícuotas por línea** — ver abajo (**corregido** en SQL usando detalle × `TipoIVA` por producto, con clasificación simplificada Exento / 10,5 % / otros→21%).
- **`GuardarProducto` overload largo** ignoraba flags/campos extendidos — **corregido** (UPDATE de `StockMinimo`, `UsaVariantes`, `EsCombo` tras el guardado base; migración de columnas en `AsegurarMigracionLite`).
- **Backup** con nombre fijo `SchPosDB` — **corregido** usando `InitialCatalog` de la cadena de conexión.
- Contraseña del certificado en claro **sigue siendo una deuda**: mitigar con DPAPI o cifrado de columna en una iteración siguiente.
- **Instalador .iss**: sigue opcional/no versionado — ver `INSTALACION_Y_VERSION_LITE.md`.
- Stock **VARIOS** 999999: sigue comportamiento actual; opcional revisar filtros en reportes más adelante.

## Cambios realizados ante la revisión

| Tema | Acción |
|------|--------|
| Login admin sin contraseña | Eliminado bypass en `ValidarUsuario` / `CargarSesionUsuario`. |
| BD sin usuarios (primer uso) | `AsegurarUsuarioAdminInicial()` crea usuario `admin` con hash PBKDF2; contraseña inicial en `UsuarioBootstrapAdminContraseña` (`Admin#2026`) — **cambiar tras el primer acceso.** |
| Llamadas al arranque | Tras inicializar BD: `DatabaseService.AsegurarUsuarioAdminInicial()` desde `App` y desde el asistente de primer uso. |
| Libro IVA | Consultas desde `FacturaDetalle`/`CompraDetalle` + `TipoIVA` del producto. |
| Migración Productos | Columnas `StockMinimo`, `UsaVariantes`, `EsCombo`; `InitializeDatabase()` ejecuta migración lite. |
| Stock al facturar | Validación en `FacturacionControl` antes de ARCA/guardado (omitido código `VARIOS`). |
| Tester / QA extendido | Sesión mediante `ValidarUsuario` / credencial bootstrap cuando la BD está vacía. |
| EliminarProducto | `DELETE … WHERE ProductoID=@id` parametrizado. |

## Contraseña inicial del operador «admin»

Por defecto, base vacía: usuario **`admin`**, contraseña **`Admin#2026`** (véase constante `DatabaseService.UsuarioBootstrapAdminContraseña`). En entrega a cliente hay que oblígar a cambio desde **Usuarios** y documentar ese flujo internamente.
