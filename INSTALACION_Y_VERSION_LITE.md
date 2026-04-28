# Instalación y versión «Lite»

## ¿Hay un instalador listo para usar?

**En este repositorio no hay un instalador empaquetado** (no hay proyecto WiX, Inno Setup, ClickOnce ni `.msi` / `.exe` de setup versionado).

Lo que sí está listo es **compilar** la solución y **distribuir** la carpeta de salida del ejecutable (despliegue tipo **portable** o **xcopy**):

1. Compilar en **Release** (por ejemplo `dotnet build SchettiniGestion.sln -c Release` o MSBuild/Visual Studio).
2. Copiar al equipo destino la carpeta de salida de **SchettiniGestion.WPF** (p. ej. `SchettiniGestion.WPF\bin\Release\` o `bin\x64\Release\` según la plataforma elegida), **incluyendo todas las DLL y archivos de configuración** que genere ese build.
3. En el cliente: **Windows 10/11**, **.NET Framework 4.7.2** instalado y **SQL Server** (Express o superior) accesible con la cadena de conexión que use la aplicación (`App.config`, etc.).

Si el negocio exige un **instalador con accesos directos, desinstalador y chequeo de requisitos**, habría que **añadir** un proyecto aparte (p. ej. Inno Setup o WiX) que empaquete esa carpeta; **no viene incluido hoy**.

---

## ¿Qué es la «versión Lite» en este producto?

**Lite no es un ejecutable diferente compilado.** Es **el mismo programa** SchettiniGestion.WPF, pero con una **licencia** (`licencia.key` o configuración equivalente) que permite **solo los módulos listados** en el JSON firmado/embebido en la clave.

- `LicenseManager` decodifica la licencia Base64 → JSON con `ModulosPermitidos`.
- `PrincipalWindow.AplicarPermisosLite()` (nombre histórico) **oculta botones y bloquea menús** según `LicenseManager.IsModuleEnabled(...)` y permisos del usuario.
- Los nombres de módulo coinciden con constantes tipo `ACCESO_FACTURACION`, `ACCESO_VENTAS`, `ACCESO_PRODUCTOS`, etc. (ver `DatabaseService` y `LicenseManager`).

Un **cliente Lite** típico tendría una licencia con **menos** entradas en `ModulosPermitidos` (ejemplo de prueba en `LicenseGenerator`: solo `FACTURACION` y `STOCK` en el listado de ejemplo; en producción deben usarse los strings `ACCESO_*` que usa la app).

Para **generar** licencias se usan las herramientas del repo (**`GeneradorLicencias`** / **`LicenseGenerator`**) con el hardware ID del equipo y la lista de módulos deseada.

---

## Checklist para «largar» la versión Lite

| Ítem | Estado típico |
|------|----------------|
| Build Release del WPF + DLL dependientes | Hacer en máquina de build |
| Cadena de conexión SQL y base creada / migrada | Responsabilidad del despliegue |
| Archivo **`licencia.key`** (o `LicenciaBase64` en config) con módulos **Lite** | Generar con herramienta de licencias |
| Usuario SQL con permisos sobre la BD | Configuración en servidor |
| Prueba en homologación AFIP antes de producción | Ver `ENTREGA_AFIP_Y_AMBIENTE.md` |
| Instalador MSI/EXE unificado | **Opcional; hoy no está en el repo** |

---

## Resumen

- **Instalador automático:** no incluido; se puede usar el producto con **copia de carpeta** tras compilar, o **definir** un instalador externo.
- **Lite:** misma compilación que la edición «completa»; diferencia = **archivo/contenido de licencia** (módulos habilitados) + permisos de usuario en la base.
