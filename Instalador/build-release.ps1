# Compila SchettiniGestion en Release x64 y prepara carpeta staging para Inno Setup.
# Uso (PowerShell en Windows):
#   .\Instalador\build-release.ps1
#   .\Instalador\build-release.ps1 -BuildInstaller

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("x64", "x86", "Any CPU")]
    [string]$Platform = "Any CPU",
    [switch]$BuildInstaller,
    [switch]$SkipBuild,
    [string]$MsBuildPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $repoRoot "SchettiniGestion.sln"
$staging = Join-Path $PSScriptRoot "staging"
# AnyCPU compila a bin\Release\ (sin subcarpeta de plataforma)
# x64/x86 compilan a bin\x64\Release\ o bin\x86\Release\
$platformFolder = if ($Platform -eq "Any CPU") { "" } else { "$Platform\" }
$outputRelative = "SchettiniGestion.WPF\bin\${platformFolder}$Configuration"

function Find-MSBuild {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (Test-Path $ExplicitPath) { return (Resolve-Path $ExplicitPath).Path }
        throw "No existe la ruta MSBuild indicada: $ExplicitPath"
    }

    $found = @()

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vswhereArgs = @(
            "-latest", "-prerelease",
            "-requires", "Microsoft.Component.MSBuild",
            "-find", "MSBuild\**\Bin\MSBuild.exe"
        )
        $found += & $vswhere @vswhereArgs 2>$null

        $vswhereArgsAll = @(
            "-all", "-prerelease",
            "-requires", "Microsoft.Component.MSBuild",
            "-find", "MSBuild\**\Bin\MSBuild.exe"
        )
        $found += & $vswhere @vswhereArgsAll 2>$null
    }

    $editionRoots = @("Community", "Professional", "Enterprise", "BuildTools")
    # Incluir VS18 (versión "18" usada en algunas instalaciones de VS 2022)
    $yearRoots = @("2022", "2019", "18", "17")
    $programFiles = @(${env:ProgramFiles}, ${env:ProgramFiles(x86)})
    foreach ($root in $programFiles) {
        if ([string]::IsNullOrWhiteSpace($root)) { continue }
        foreach ($year in $yearRoots) {
            foreach ($edition in $editionRoots) {
                $found += Join-Path $root "Microsoft Visual Studio\$year\$edition\MSBuild\Current\Bin\MSBuild.exe"
            }
        }
    }

    $fromPath = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($fromPath) { $found += $fromPath.Source }

    foreach ($p in ($found | Select-Object -Unique)) {
        if (-not [string]::IsNullOrWhiteSpace($p) -and (Test-Path $p)) {
            return (Resolve-Path $p).Path
        }
    }

    $msg = @"
No se encontro MSBuild en este equipo.

Opciones:
  1) Instalar Visual Studio 2022 con la carga 'Desarrollo de .NET de escritorio'
     o 'Build Tools for Visual Studio 2022' con MSBuild.
  2) Compilar desde Visual Studio: abrir SchettiniGestion.sln, elegir Release y x64,
     menu Compilar > Compilar solucion. Luego ejecutar solo el empaquetado:
       .\Instalador\build-release.ps1 -BuildInstaller -SkipBuild
  3) Indicar la ruta manualmente:
       .\Instalador\build-release.ps1 -MsBuildPath 'C:\Ruta\a\MSBuild.exe'
"@
    throw $msg
}

if (-not $SkipBuild) {
    $banner = "== SCHPOS - build {0} / {1} ==" -f $Configuration, $Platform
    Write-Host $banner -ForegroundColor Cyan

    $msbuild = Find-MSBuild -ExplicitPath $MsBuildPath
    Write-Host "MSBuild: $msbuild"

    & $msbuild $sln /t:Restore,Build /p:Configuration=$Configuration /p:Platform=$Platform /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "MSBuild fallo con codigo $LASTEXITCODE" }
}
else {
    Write-Host "Omitiendo compilacion (-SkipBuild). Usando binarios existentes." -ForegroundColor Yellow
}

$outDir = Join-Path $repoRoot $outputRelative
$exePath = Join-Path $outDir "SCHPOS.exe"
if (-not (Test-Path $exePath)) {
    throw "No se encontro el ejecutable en: $outDir"
}

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging | Out-Null
Copy-Item -Path (Join-Path $outDir "*") -Destination $staging -Recurse -Force

Get-ChildItem $staging -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

$prereqLic = Join-Path $PSScriptRoot "prerequisites\licencia.key"
if (Test-Path $prereqLic) {
    Copy-Item $prereqLic (Join-Path $staging "licencia.key") -Force
    Write-Host "licencia.key incluida en staging (testing)." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Carpeta lista para Inno Setup:" -ForegroundColor Green
Write-Host "  $staging"
Write-Host ""
Write-Host "Pasos siguientes:" -ForegroundColor Yellow
Write-Host ('  1. Ejecute con -BuildInstaller para descargar prerequisitos y generar Setup.exe')
Write-Host ('  2. O abra ' + (Join-Path $PSScriptRoot 'SchettiniGestion.iss') + ' en Inno Setup (F9)')
Write-Host ('  3. El Setup.exe quedara en ' + (Join-Path $PSScriptRoot 'Output'))
Write-Host ""
Write-Host "Licencias: ejecute LicenseGenerator. Opcional: copie licencia.key a Instalador\prerequisites antes del build."
Write-Host "Usuario inicial tras instalar: admin (cambiar contrasena en primer uso)."

if ($BuildInstaller) {
    $downloadScript = Join-Path $PSScriptRoot "download-prerequisites.ps1"
    if (Test-Path $downloadScript) {
        Write-Host ""
        Write-Host "Descargando prerequisitos (VC++ Redist + LocalDB + .NET 4.8)..." -ForegroundColor Cyan
        & $downloadScript
    }
    else {
        Write-Warning "No se encontro download-prerequisites.ps1"
    }
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
        if ($LASTEXITCODE -ne 0) { throw "ISCC fallo con codigo $LASTEXITCODE" }
        $outInstaller = Join-Path $PSScriptRoot "Output"
        Write-Host "Instalador generado en $outInstaller" -ForegroundColor Green
    }
}
