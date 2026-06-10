# Compila SchettiniGestion en Release x64 y prepara carpeta staging para Inno Setup.
# Uso (PowerShell en Windows, desde la raíz del repo o desde Instalador\):
#   .\Instalador\build-release.ps1
#   .\Instalador\build-release.ps1 -BuildInstaller

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("x64", "x86", "Any CPU")]
    [string]$Platform = "x64",
    [switch]$BuildInstaller
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $repoRoot "SchettiniGestion.sln"
$staging = Join-Path $PSScriptRoot "staging"
$outputRelative = "SchettiniGestion.WPF\bin\$Platform\$Configuration"

function Find-MSBuild {
    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    )
    foreach ($p in $candidates) {
        if (Test-Path $p) { return $p }
    }
    $fromPath = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }
    throw "No se encontró MSBuild. Instale Visual Studio 2022 con carga de trabajo .NET desktop."
}

Write-Host "== SchettiniGestion — build $Configuration | $Platform ==" -ForegroundColor Cyan

$msbuild = Find-MSBuild
Write-Host "MSBuild: $msbuild"

& $msbuild $sln /t:Restore,Build /p:Configuration=$Configuration /p:Platform=$Platform /v:minimal
if ($LASTEXITCODE -ne 0) { throw "MSBuild falló con código $LASTEXITCODE" }

$outDir = Join-Path $repoRoot $outputRelative
if (-not (Test-Path (Join-Path $outDir "SchettiniGestion.WPF.exe"))) {
    throw "No se encontró el ejecutable en: $outDir"
}

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging | Out-Null
Copy-Item -Path (Join-Path $outDir "*") -Destination $staging -Recurse -Force

# Quitar símbolos de depuración del paquete de testing (opcional)
Get-ChildItem $staging -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Carpeta lista para Inno Setup:" -ForegroundColor Green
Write-Host "  $staging"
Write-Host ""
Write-Host "Pasos siguientes:" -ForegroundColor Yellow
Write-Host "  1) (Opcional) Coloque SqlLocalDB.msi en Instalador\prerequisites\"
Write-Host "  2) Abra Instalador\SchettiniGestion.iss en Inno Setup y compile (F9)"
Write-Host "  3) El Setup.exe quedará en Instalador\Output\"
Write-Host ""
Write-Host "Licencias: ejecute LicenseGenerator y envíe la clave al tester."
Write-Host "Usuario inicial tras instalar: admin (cambiar contraseña en primer uso)."

if ($BuildInstaller) {
    $isccCandidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    $iscc = $null
    foreach ($p in $isccCandidates) {
        if (Test-Path $p) { $iscc = $p; break }
    }
    if (-not $iscc) {
        Write-Warning "Inno Setup no encontrado. Compile SchettiniGestion.iss manualmente."
    }
    else {
        $iss = Join-Path $PSScriptRoot "SchettiniGestion.iss"
        & $iscc $iss
        if ($LASTEXITCODE -ne 0) { throw "ISCC falló con código $LASTEXITCODE" }
        Write-Host "Instalador generado en Instalador\Output\" -ForegroundColor Green
    }
}
