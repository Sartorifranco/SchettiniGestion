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

## Alternativa consola

Proyecto `LicenseGenerator/` — generador por consola, misma clave AES.

## Empaquetado portable (oficina)

```powershell
.\Instalador\pack-admin-licencias.ps1
```

Genera `Instalador\Output\AdminLicencias-Portable.zip` listo para copiar a otra PC.
