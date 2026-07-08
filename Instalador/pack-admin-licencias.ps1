# Compila AdminLicencias Release y arma ZIP portable para la oficina.
# Uso: .\Instalador\pack-admin-licencias.ps1

param(
    [string]$MsBuildPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repoRoot "AdminLicencias\AdminLicencias.csproj"
$outDir = Join-Path $repoRoot "AdminLicencias\bin\Release"
$zipDir = Join-Path $PSScriptRoot "Output"
$zipPath = Join-Path $zipDir "AdminLicencias-Portable.zip"

function Find-MSBuild {
    param([string]$ExplicitPath)
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath) -and (Test-Path $ExplicitPath)) {
        return (Resolve-Path $ExplicitPath).Path
    }
    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
    )
    foreach ($p in $candidates) {
        if (Test-Path $p) { return $p }
    }
    throw "No se encontró MSBuild. Indique -MsBuildPath."
}

$msbuild = Find-MSBuild -ExplicitPath $MsBuildPath
Write-Host "Compilando AdminLicencias (Release)..." -ForegroundColor Cyan
& $msbuild $proj /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw "MSBuild falló con código $LASTEXITCODE" }

$exe = Join-Path $outDir "AdminLicencias.exe"
if (-not (Test-Path $exe)) { throw "No se encontró $exe" }

if (-not (Test-Path $zipDir)) { New-Item -ItemType Directory -Path $zipDir | Out-Null }

$staging = Join-Path $env:TEMP "AdminLicencias-Portable"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging | Out-Null

Copy-Item (Join-Path $outDir "AdminLicencias.exe") $staging -Force
Copy-Item (Join-Path $outDir "AdminLicencias.exe.config") $staging -Force
Copy-Item (Join-Path $outDir "Newtonsoft.Json.dll") $staging -Force
Copy-Item (Join-Path $outDir "SchettiniGestion.dll") $staging -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $outDir "SchettiniGestion.dll.config") $staging -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $outDir "ModulosCatalog.json") $staging -Force -ErrorAction SilentlyContinue
if (-not (Test-Path (Join-Path $staging "ModulosCatalog.json"))) {
    Copy-Item (Join-Path $repoRoot "SchettiniGestion\ModulosCatalog.json") $staging -Force
}
Copy-Item (Join-Path $repoRoot "AdminLicencias\README.md") $staging -Force

$leeme = @"
IMPORTANTE — AdminLicencias portable
==================================

1) NO ejecute AdminLicencias.exe desde dentro del ZIP.
2) Extraiga TODOS los archivos a una carpeta (ej: C:\SCHPOS\AdminLicencias).
3) Ejecute AdminLicencias.exe desde esa carpeta.

Si abre el .exe sin extraer, puede fallar con error de Newtonsoft.Json
aunque el archivo este dentro del ZIP.

Requisito: .NET Framework 4.7.2 o superior (Windows 10/11).
"@
Set-Content -Path (Join-Path $staging "LEEME.txt") -Value $leeme -Encoding UTF8

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath -Force
Remove-Item $staging -Recurse -Force

Write-Host ""
Write-Host "Listo:" -ForegroundColor Green
Write-Host "  $zipPath"
Write-Host "Copiá el ZIP a la PC de la oficina, EXTRAÉ todo en una carpeta y ejecutá AdminLicencias.exe."
