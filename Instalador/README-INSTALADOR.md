# Instalador SchettiniGestion — guía para testing

Objetivo: que el tester instale con **Siguiente → Siguiente → Listo** y tenga el sistema funcionando en su PC, activándolo solo con la **clave** que vos generás.

## Qué incluye el instalador

| Componente | Cómo se resuelve |
|------------|------------------|
| Aplicación WPF | Carpeta `staging` generada por el build |
| Base de datos | **SQL Server LocalDB** (recomendado) + creación automática de `SchPosDB` en el **primer inicio** |
| Esquema / tablas | La propia app (`App.xaml.cs` + `DatabaseService`) |
| Usuario inicial | `admin` (contraseña definida en `DatabaseService`; cambiarla en el módulo Usuarios) |
| Licencia | **No** va en el instalador. Vos enviás la clave por separado |

## Requisitos en la PC de build (tu máquina)

1. Visual Studio 2022 con **.NET desktop development**
2. **Inno Setup 6** — https://jrsoftware.org/isdl.php
3. (Opcional) **SqlLocalDB.msi** en `Instalador/prerequisites/` para instalar LocalDB sin pasos manuales  
   - Descarga: buscar *SQL Server Express LocalDB* en Microsoft  
   - Si no lo incluís, el tester debe tener LocalDB o SQL Express ya instalado

## Generar el Setup.exe

```powershell
cd C:\schpos\SchettiniGestion
.\Instalador\build-release.ps1
# o todo en uno si Inno está instalado:
.\Instalador\build-release.ps1 -BuildInstaller
```

Salida: `Instalador\Output\SchettiniGestion-Setup-1.0.0-testing.exe`

## Generar licencias para testers

```powershell
cd C:\schpos\SchettiniGestion
dotnet run --project LicenseGenerator
```

1. Completá CUIT/nombre, días de validez y módulos (`s` = sí).
2. Copiá la **clave Base64** larga que imprime la consola.
3. Enviásela al tester por mail/WhatsApp (no hace falta el ID de máquina con este formato).

### Cómo activa el tester

**Opción A — Pegar clave (recomendada)**  
1. Instalar y abrir SchettiniGestion.  
2. Pantalla **Activación de licencia** → pegar clave → **Activar**.

**Opción B — Archivo**  
1. Crear `licencia.key` con la clave adentro (solo texto, una línea).  
2. Copiarlo a la carpeta del programa o usar **Cargar archivo licencia.key** en la activación.

**Opción C — Desde Configuración**  
Módulo Configuración → pestaña Licencia (si ya entró con licencia de prueba).

## Flujo del tester (resumen)

```
Instalar Setup.exe → Siguiente… → Fin
        ↓
Abrir SchettiniGestion
        ↓
[Si no hay licencia] Activación → pegar clave
        ↓
[Si no hay SQL] Asistente primer uso (LocalDB por defecto)
        ↓
Login admin → cambiar contraseña → usar el sistema
```

## Licenciamiento vs Eligestion

| Eligestion (referencia) | SchettiniGestion hoy |
|-------------------------|----------------------|
| Activación en línea | No implementada (futuro) |
| Cargar archivo de licencia | Sí (`licencia.key` + botón en activación) |
| QR / modo online | No implementado (futuro) |
| Clave del desarrollador | Sí — `LicenseGenerator` (Base64 JSON) |

El formato de clave es **JSON en Base64** con `ModulosPermitidos` tipo `ACCESO_FACTURACION`, `ACCESO_STOCK`, etc.

> **Nota:** `GeneradorLicencias` usa cifrado AES distinto y **no** es compatible con la app. Usar solo **`LicenseGenerator`**.

## Build Release sin licencia embebida

En compilación **Release** no hay licencia de desarrollo embebida: sin clave válida aparece la pantalla de activación. En **Debug** sigue la licencia de prueba para desarrollo.

## Checklist antes de enviar a testing

- [ ] `build-release.ps1` sin errores
- [ ] Setup.exe probado en una VM o PC limpia
- [ ] LocalDB instalado (por el MSI o manualmente)
- [ ] Clave generada con los módulos que querés probar
- [ ] Documentar al tester: usuario `admin` y que cambie la contraseña
- [ ] AFIP en homologación hasta certificado de producción (`ENTREGA_AFIP_Y_AMBIENTE.md`)

## Solución de problemas

| Problema | Qué hacer |
|----------|-----------|
| "No hay licencia activa" | Generar y pegar clave con `LicenseGenerator` |
| Error de conexión SQL | Instalar LocalDB o ejecutar asistente de primer uso |
| .NET faltante | Instalar .NET Framework 4.8 |
| Clave "no válida" | Verificar que no tenga espacios/saltos de línea extra; regenerar con `LicenseGenerator` |
