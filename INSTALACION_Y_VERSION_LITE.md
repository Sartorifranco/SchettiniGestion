# Instalación y versión «Lite»

## ¿Hay un instalador listo para usar?

**Sí — Inno Setup** en la carpeta `Instalador/`:

1. En Windows: `.\Instalador\build-release.ps1` (compila Release x64 y arma `staging\`).
2. Abrir `Instalador\SchettiniGestion.iss` en **Inno Setup 6** y compilar (F9), o `build-release.ps1 -BuildInstaller`.
3. Entregar `Instalador\Output\SchettiniGestion-Setup-*.exe` al tester.

Guía completa: **[Instalador/README-INSTALADOR.md](Instalador/README-INSTALADOR.md)**

La base de datos se crea sola en el **primer inicio** (LocalDB por defecto). La **licencia no va en el instalador**: generarla con `LicenseGenerator` y enviarla al tester para pegar en la pantalla de activación o en `licencia.key`.

Alternativa manual (sin Setup): compilar Release x64 y copiar `SchettiniGestion.WPF\bin\x64\Release\` con todas las DLL.

---

## Primera vez: usuario `admin`

Si la tabla `Usuarios` está vacía, al iniciar la app se crea el usuario **`admin`** con una contraseña inicial definida en código (`UsuarioBootstrapAdminContraseña` en `DatabaseService`, valor documentado **`Admin#2026`**). **Cambiá esa contraseña** desde el módulo de usuarios antes de entregar la instalación a un cliente final.

---

## ¿Qué es la «versión Lite» en este producto?

**Lite no es un ejecutable diferente compilado.** Es **el mismo programa** SchettiniGestion.WPF, pero con una **licencia** (`licencia.key` o configuración equivalente) que permite **solo los módulos listados** en el JSON firmado/embebido en la clave.

- `LicenseManager` decodifica la licencia Base64 → JSON con `ModulosPermitidos`.
- `PrincipalWindow.AplicarPermisosLite()` (nombre histórico) **oculta botones y bloquea menús** según `LicenseManager.IsModuleEnabled(...)` y permisos del usuario.
- Los nombres de módulo coinciden con constantes tipo `ACCESO_FACTURACION`, `ACCESO_VENTAS`, `ACCESO_PRODUCTOS`, etc. (ver `DatabaseService` y `LicenseManager`).

Un **cliente Lite** típico tendría una licencia con **menos** entradas en `ModulosPermitidos` (ejemplo de prueba en `LicenseGenerator`: solo `FACTURACION` y `STOCK` en el listado de ejemplo; en producción deben usarse los strings `ACCESO_*` que usa la app).

Para **generar** licencias de testing usar **`LicenseGenerator`** (consola interactiva, clave Base64). El tester activa pegando la clave o cargando `licencia.key`. El proyecto `GeneradorLicencias` (AES) no es compatible con la app actual.

---

## Checklist para «largar» la versión Lite

| Ítem | Estado típico |
|------|----------------|
| Build Release del WPF + DLL dependientes | Hacer en máquina de build |
| Cadena de conexión SQL y base creada / migrada | Responsabilidad del despliegue |
| Archivo **`licencia.key`** (o `LicenciaBase64` en config) con módulos **Lite** | Generar con herramienta de licencias |
| Usuario SQL con permisos sobre la BD | Configuración en servidor |
| Prueba en homologación ARCA antes de producción | Ver `ENTREGA_AFIP_Y_AMBIENTE.md` |
| Instalador Setup.exe (Inno) | `Instalador/build-release.ps1` + `SchettiniGestion.iss` |

---

## Resumen

- **Instalador automático:** `Instalador/` (Inno Setup) + activación por clave del `LicenseGenerator`.
- **Lite:** misma compilación que la edición «completa»; diferencia = **archivo/contenido de licencia** (módulos habilitados) + permisos de usuario en la base.
