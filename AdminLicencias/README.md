# AdminLicencias — Generador de licencias SCHPOS (con interfaz)

Herramienta interna para generar claves de licencia compatibles con `LicenseManager` de SCHPOS.

## Compilar

Desde la raíz del repositorio:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  AdminLicencias\AdminLicencias.csproj /p:Configuration=Release
```

Salida: `AdminLicencias\bin\Release\AdminLicencias.exe`

Requisito en la PC destino: **.NET Framework 4.7.2+** (incluido en Windows 10/11).

## Uso

1. Ejecutar `AdminLicencias.exe`
2. Alta de cliente (CUIT, razón social, IP opcional)
3. **Nueva licencia** → pegar **Hardware ID** que envía el cliente desde la pantalla de activación de SCHPOS
4. Elegir módulos (`ACCESO_*`) y fecha de vencimiento
5. Copiar la clave Base64 y enviarla al cliente (pegar en activación o archivo `licencia.key`)

Los datos (clientes e historial) se guardan en `%AppData%\SCHPOSAdmin\datos.json`.  
Desde **Configuración** podés apuntar a una carpeta compartida (OneDrive, red).

## Catálogo de módulos (automático)

El listado de módulos para tildar al generar licencias sale de un solo archivo:

`SchettiniGestion/ModulosCatalog.json`

Al compilar **AdminLicencias** o **SCHPOS**, ese JSON se copia junto al `.exe`.  
**No hace falta editar checkboxes a mano** en el licenciador.

### Agregar un módulo nuevo

1. Editá `SchettiniGestion/ModulosCatalog.json` (entrada con `codigo`, `nombre`, `licenciable`, `orden`, etc.).
2. En SCHPOS: agregá `PERMISO_XXX` en `DatabaseService.cs` y el botón/menú con `PuedeModulo(...)`.
3. Compilá AdminLicencias y/o el instalador SCHPOS.

El licenciador mostrará el nuevo checkbox automáticamente.

## Alternativa consola

Proyecto `LicenseGenerator/` — generador por consola, misma clave AES.

## Empaquetado portable (oficina)

```powershell
.\Instalador\pack-admin-licencias.ps1
```

Genera `Instalador\Output\AdminLicencias-Portable.zip` listo para copiar a otra PC.
