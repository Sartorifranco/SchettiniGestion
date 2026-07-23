# Instalador SCHPOS — guía para testing y entrega

Objetivo: que el tester instale con **Siguiente → Siguiente → Listo** y tenga **SCHPOS v2.1.9** funcionando en su PC.

## Qué hace el instalador automáticamente

| Paso | Qué ocurre |
|------|------------|
| 1 | Copia la aplicación WPF (`SCHPOS.exe`) y dependencias |
| 2 | Instala **.NET Framework 4.8** si falta (offline, empaquetado) |
| 3 | Instala **SQL Server LocalDB** si falta |
| 4 | Escribe `conexion.cfg` en `%ProgramData%\SCHPOS` |
| 5 | Ejecuta `SCHPOS.exe /bootstrap` (crea `SchPosDB`, tablas y usuario `admin`) |
| 6 | (Opcional) Copia `licencia.key` si la incluiste al compilar |

La **licencia** puede ir aparte (pegar clave en activación) o empaquetada para testing.

## Generar el Setup.exe (un solo comando)

```powershell
cd C:\schpos\SchettiniGestion
git pull origin cursor/installer-testing-d53a
.\Instalador\build-release.ps1 -BuildInstaller
```

Eso compila Release, descarga prerequisitos a `Instalador\prerequisites\`, arma `staging\` e invoca Inno Setup.

Si MSBuild no se detecta:

```powershell
.\Instalador\build-release.ps1 -MsBuildPath "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" -BuildInstaller
```

Si ya compilaste en Visual Studio (Release):

```powershell
.\Instalador\build-release.ps1 -SkipBuild -BuildInstaller
```

Solo descargar prerequisitos:

```powershell
.\Instalador\download-prerequisites.ps1
```

**Salida:** `Instalador\Output\SCHPOS-Setup-2.1.9.exe`

### Incluir licencia en el Setup (opcional)

```powershell
dotnet run --project LicenseGenerator
# Copiar la clave a: Instalador\prerequisites\licencia.key
.\Instalador\build-release.ps1 -SkipBuild -BuildInstaller
```

### Empaquetar AdminLicencias portable

```powershell
.\Instalador\pack-admin-licencias.ps1
```

Salida: `Instalador\Output\AdminLicencias-Portable.zip`

## Flujo del tester

```
SCHPOS-Setup-2.1.9.exe → Siguiente… → Fin
        ↓
[Instalador] .NET 4.8 + LocalDB + bootstrap BD
        ↓
Abrir SCHPOS
        ↓
[Si no hay licencia] Activación → pegar clave
        ↓
Login admin → cambiar contraseña → usar el sistema
```

## Checklist antes de enviar a testing

- [ ] `.\Instalador\build-release.ps1 -BuildInstaller` sin errores
- [ ] Existe `Instalador\Output\SCHPOS-Setup-2.1.9.exe`
- [ ] (Opcional) `licencia.key` en prerequisites
- [ ] Probar en VM o PC limpia
- [ ] Documentar: usuario `admin`, cambiar contraseña
- [ ] ARCA en homologación hasta certificado de producción (`docs/Guia_Activacion_ARCA_ARCA.md`)

## Solución de problemas

| Problema | Qué hacer |
|----------|-----------|
| Falla descarga de prerequisitos | `download-prerequisites.ps1` con Internet |
| "No hay licencia activa" | Pegar clave o incluir `licencia.key` al compilar |
| Error SQL tras instalar | `sqllocaldb info MSSQLLocalDB` en CMD |
| Bootstrap falló | Abrir la app; intentará configurar BD al inicio |
| Setup sin .NET offline | Recompilar con `-BuildInstaller` |

## Licenciamiento

Usar **`LicenseGenerator`** o **`AdminLicencias`** (portable). `GeneradorLicencias` (AES) no es compatible.
