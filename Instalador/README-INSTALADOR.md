# Instalador SchettiniGestion — guía para testing

Objetivo: que el tester instale con **Siguiente → Siguiente → Listo** y tenga el sistema funcionando en su PC.

## Qué hace el instalador automáticamente

| Paso | Qué ocurre |
|------|------------|
| 1 | Copia la aplicación WPF y dependencias |
| 2 | Instala **.NET Framework 4.8** si falta (offline, empaquetado) |
| 3 | Instala **SQL Server LocalDB** si falta |
| 4 | Escribe `conexion.cfg` en `%ProgramData%\SchettiniGestion` |
| 5 | Ejecuta `SchettiniGestion.WPF.exe /bootstrap` (crea `SchPosDB`, tablas y usuario `admin`) |
| 6 | (Opcional) Copia `licencia.key` si la incluiste al compilar |

La **licencia** puede ir aparte (pegar clave en activación) o empaquetada para testing (ver abajo).

## Requisitos en la PC de build (tu máquina)

1. **MSBuild** — Visual Studio 2022 / Build Tools, o compilar en VS y usar `-SkipBuild`
2. **Inno Setup 6** — https://jrsoftware.org/isdl.php
3. **Internet** la primera vez que corrés `-BuildInstaller` (descarga LocalDB + .NET 4.8 offline, ~120 MB total)

## Generar el Setup.exe (un solo comando)

```powershell
cd C:\schpos\SchettiniGestion
git pull origin cursor/installer-testing-d53a
.\Instalador\build-release.ps1 -BuildInstaller
```

Eso compila Release x64, descarga prerequisitos a `Instalador\prerequisites\`, arma `staging\` e invoca Inno Setup.

Si MSBuild no se detecta:

```powershell
.\Instalador\build-release.ps1 -MsBuildPath "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" -BuildInstaller
```

Si ya compilaste en Visual Studio (Release, x64):

```powershell
.\Instalador\build-release.ps1 -SkipBuild -BuildInstaller
```

Solo descargar prerequisitos sin compilar:

```powershell
.\Instalador\download-prerequisites.ps1
```

**Salida:** `Instalador\Output\SchettiniGestion-Setup-1.0.0-testing.exe`

### Incluir licencia en el Setup (opcional, para mañana)

```powershell
dotnet run --project LicenseGenerator
# Copiar la clave generada a:
#   Instalador\prerequisites\licencia.key
.\Instalador\build-release.ps1 -SkipBuild -BuildInstaller
```

El tester no verá la pantalla de activación si la clave es válida.

## Generar licencias para testers

```powershell
dotnet run --project LicenseGenerator
```

1. Completá CUIT/nombre, días de validez y módulos (`s` = sí).
2. Copiá la **clave Base64** larga que imprime la consola.
3. Enviásela al tester por mail/WhatsApp.

### Cómo activa el tester (si no va licencia en el Setup)

**Opción A — Pegar clave (recomendada)**  
Instalar → abrir SchettiniGestion → pantalla **Activación** → pegar clave → **Activar**.

**Opción B — Archivo**  
Crear `licencia.key` con la clave (una línea) en la carpeta del programa o en activación → **Cargar archivo**.

## Flujo del tester (resumen)

```
Setup.exe → Siguiente… → Fin
        ↓
[Instalador] .NET 4.8 + LocalDB + bootstrap BD (sin intervención)
        ↓
Abrir SchettiniGestion
        ↓
[Si no hay licencia] Activación → pegar clave
        ↓
Login admin → cambiar contraseña → usar el sistema
```

Ya **no** debería aparecer el asistente de primer uso SQL en una PC limpia con LocalDB instalado por el Setup.

## Checklist antes de enviar a testing

- [ ] `.\Instalador\build-release.ps1 -BuildInstaller` sin errores
- [ ] Existe `Instalador\Output\SchettiniGestion-Setup-1.0.0-testing.exe`
- [ ] (Opcional) `licencia.key` en prerequisites si querés saltar activación
- [ ] Clave generada con los módulos a probar (si va aparte)
- [ ] Documentar al tester: usuario `admin` y cambiar contraseña
- [ ] AFIP en homologación hasta certificado de producción

## Solución de problemas

| Problema | Qué hacer |
|----------|-----------|
| Falla descarga de prerequisitos | Ejecutar `download-prerequisites.ps1` con Internet; verificar antivirus |
| "No hay licencia activa" | Generar y pegar clave, o incluir `licencia.key` al compilar |
| Error de conexión SQL tras instalar | Reinstalar; verificar que LocalDB esté en `sqllocaldb info MSSQLLocalDB` |
| Bootstrap falló | Abrir la app; intentará configurar BD al inicio |
| .NET faltante y Setup sin offline | Volver a compilar con `-BuildInstaller` (descarga ndp48) |
| Clave "no válida" | Sin espacios extra; regenerar con `LicenseGenerator` |

## Licenciamiento

Usar solo **`LicenseGenerator`** (Base64 JSON). `GeneradorLicencias` (AES) no es compatible.

En **Release** no hay licencia embebida de desarrollo. En **Debug** sigue la licencia de prueba.
