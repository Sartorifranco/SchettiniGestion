# Descarga SqlLocalDB y .NET Framework 4.8 offline para empaquetar en el Setup.
# Uso: .\Instalador\download-prerequisites.ps1

param(
    [string]$PrerequisitesDir = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PrerequisitesDir)) {
    $PrerequisitesDir = Join-Path $PSScriptRoot "prerequisites"
}

$downloads = @(
    @{
        Name = "SqlLocalDB.msi"
        Url  = "https://go.microsoft.com/fwlink/?linkid=2216019"
    },
    @{
        Name = "ndp48-x86-x64-allos-enu.exe"
        Url  = "https://go.microsoft.com/fwlink/?linkid=2088631"
    }
)

New-Item -ItemType Directory -Path $PrerequisitesDir -Force | Out-Null

foreach ($item in $downloads) {
    $dest = Join-Path $PrerequisitesDir $item.Name
    if (Test-Path $dest) {
        $sizeMb = [math]::Round((Get-Item $dest).Length / 1MB, 1)
        Write-Host "OK: $($item.Name) ya existe ($sizeMb MB)."
        continue
    }

    Write-Host "Descargando $($item.Name) ..." -ForegroundColor Cyan
    try {
        Invoke-WebRequest -Uri $item.Url -OutFile $dest -UseBasicParsing
    }
    catch {
        throw "No se pudo descargar $($item.Name) desde $($item.Url). Verifique conexion a Internet."
    }

    $sizeMb = [math]::Round((Get-Item $dest).Length / 1MB, 1)
    Write-Host "Guardado: $dest ($sizeMb MB)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Prerequisitos listos en: $PrerequisitesDir" -ForegroundColor Green
Write-Host "Opcional: copie licencia.key en esa carpeta antes de compilar el Setup (solo testing)."
