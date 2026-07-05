#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Limpia el entorno de instalacion de SCHPOS para simular una PC virgen.

.DESCRIPTION
    Elimina la instancia de LocalDB, los archivos MDF/LDF de SchPosDB, la carpeta de
    configuracion de la aplicacion y (opcionalmente) desinstala LocalDB y la app.
    Util para probar el instalador .exe desde cero antes de entregar al cliente.

.PARAMETER Nivel
    1 = Rapido   : borra instancia + MDF/LDF + config  (mantiene LocalDB instalado)
    2 = Completo : todo lo anterior + desinstala LocalDB (simula PC sin SQL Engine)
    3 = Total    : todo lo anterior + desinstala la aplicacion SCHPOS

.EXAMPLE
    .\reset-clean-env.ps1              # interactivo, pide nivel
    .\reset-clean-env.ps1 -Nivel 1    # solo limpiar datos/instancia
    .\reset-clean-env.ps1 -Nivel 2    # + desinstala LocalDB
    .\reset-clean-env.ps1 -Nivel 3    # + desinstala app
#>
param(
    [ValidateSet(1, 2, 3)]
    [int]$Nivel = 0
)

$ErrorActionPreference = "Continue"

function Write-Step { param($msg) Write-Host "`n>> $msg" -ForegroundColor Cyan }
function Write-OK   { param($msg) Write-Host "   OK  $msg" -ForegroundColor Green }
function Write-Skip { param($msg) Write-Host "   --  $msg" -ForegroundColor DarkGray }
function Write-Warn { param($msg) Write-Host "   !!  $msg" -ForegroundColor Yellow }

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Magenta
Write-Host "       SCHPOS -- Reset entorno de instalacion        " -ForegroundColor Magenta
Write-Host "=====================================================" -ForegroundColor Magenta

# Elegir nivel si no se paso por parametro
if ($Nivel -eq 0) {
    Write-Host ""
    Write-Host "Selecciona el nivel de limpieza:" -ForegroundColor Yellow
    Write-Host "  [1] Rapido   - Borra instancia LocalDB + MDF/LDF + config app"
    Write-Host "  [2] Completo - Todo lo anterior + desinstala LocalDB"
    Write-Host "  [3] Total    - Todo lo anterior + desinstala la app"
    Write-Host "  [Q] Salir"
    Write-Host ""
    $resp = Read-Host "Nivel"
    if ($resp -eq "Q" -or $resp -eq "q") { exit 0 }
    $Nivel = [int]$resp
}

# =============================================================================
# NIVEL 1 -- Instancia LocalDB + archivos BD + configuracion de la app
# =============================================================================

# --- 1a. Buscar sqllocaldb.exe por ruta absoluta ---
$sqlLocalDb = $null
foreach ($ver in @("160","150","140","130","120")) {
    foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not $root) { continue }
        $c = Join-Path $root "Microsoft SQL Server\$ver\Tools\Binn\sqllocaldb.exe"
        if (Test-Path $c) { $sqlLocalDb = $c; break }
    }
    if ($sqlLocalDb) { break }
}
if (-not $sqlLocalDb) { $sqlLocalDb = "sqllocaldb" }
Write-Skip "Usando sqllocaldb: $sqlLocalDb"

# --- 1b. Detener y eliminar instancia MSSQLLocalDB ---
Write-Step "Limpiando instancia LocalDB (MSSQLLocalDB)..."

& $sqlLocalDb stop MSSQLLocalDB -i 2>&1 | Out-Null
Write-OK "Instancia detenida (o ya estaba detenida)"

$delResult = & $sqlLocalDb delete MSSQLLocalDB 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-OK "Instancia MSSQLLocalDB eliminada"
} else {
    Write-Warn "No se pudo eliminar la instancia: $delResult"
}

# --- 1c. Borrar archivos MDF / LDF de SchPosDB ---
Write-Step "Eliminando archivos de base de datos (MDF/LDF)..."

$searchDirs = @(
    $env:USERPROFILE,
    (Join-Path $env:LOCALAPPDATA "Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB")
)
$found = $false
foreach ($dir in $searchDirs) {
    if (-not (Test-Path $dir)) { continue }
    $files = Get-ChildItem $dir -Filter "SchPosDB*" -ErrorAction SilentlyContinue
    foreach ($f in $files) {
        Remove-Item $f.FullName -Force -ErrorAction SilentlyContinue
        Write-OK "Eliminado: $($f.FullName)"
        $found = $true
    }
}
if (-not $found) { Write-Skip "No se encontraron archivos MDF/LDF de SchPosDB" }

# --- 1d. Eliminar carpeta de configuracion de la app ---
Write-Step "Eliminando configuracion de la aplicacion..."

$cfgDir = "$env:ProgramData\SCHPOS"
if (Test-Path $cfgDir) {
    Get-ChildItem $cfgDir | ForEach-Object {
        Remove-Item $_.FullName -Force
        Write-OK "Eliminado: $($_.FullName)"
    }
    Remove-Item $cfgDir -ErrorAction SilentlyContinue
    if (-not (Test-Path $cfgDir)) { Write-OK "Carpeta $cfgDir eliminada" }
} else {
    Write-Skip "Carpeta $cfgDir no existe (ya estaba limpio)"
}

# Licencia en carpeta de instalacion
foreach ($d in @("$env:ProgramFiles\SCHPOS", "${env:ProgramFiles(x86)}\SCHPOS")) {
    $lk = Join-Path $d "licencia.key"
    if (Test-Path $lk) { Remove-Item $lk -Force; Write-OK "Eliminado: $lk" }
}

if ($Nivel -lt 2) {
    Write-Host ""
    Write-Host "Limpieza nivel 1 completada." -ForegroundColor Green
    Write-Host "LocalDB sigue instalado pero sin instancias ni BD." -ForegroundColor DarkGray
    Write-Host "Al iniciar la app se recreara MSSQLLocalDB + SchPosDB automaticamente." -ForegroundColor DarkGray
    exit 0
}

# =============================================================================
# NIVEL 2 -- Desinstalar LocalDB
# =============================================================================
Write-Step "Desinstalando SQL Server LocalDB..."

$regRoots = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
)
$localDbEntry = $null
foreach ($root in $regRoots) {
    if (-not (Test-Path $root)) { continue }
    $localDbEntry = Get-ChildItem $root -ErrorAction SilentlyContinue |
        Get-ItemProperty -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -like "*LocalDB*" } |
        Select-Object -First 1
    if ($localDbEntry) { break }
}

if ($localDbEntry) {
    Write-Host "   Encontrado: $($localDbEntry.DisplayName)" -ForegroundColor Gray
    if ($localDbEntry.UninstallString -match "\{[0-9A-Fa-f\-]+\}") {
        $productCode = $matches[0]
        Write-Host "   ProductCode: $productCode" -ForegroundColor Gray
        $proc = Start-Process "msiexec.exe" -ArgumentList "/x $productCode /qn /norestart" -PassThru -Wait
        if ($proc.ExitCode -eq 0 -or $proc.ExitCode -eq 3010) {
            Write-OK "LocalDB desinstalado (si ExitCode=3010 se requiere reinicio)"
        } else {
            Write-Warn "msiexec ExitCode=$($proc.ExitCode). Intenta manualmente desde Agregar/Quitar Programas."
        }
    } else {
        Write-Warn "No se pudo extraer el ProductCode. Desinstala LocalDB manualmente."
    }
} else {
    Write-Skip "LocalDB no encontrado en el registro"
}

if ($Nivel -lt 3) {
    Write-Host ""
    Write-Host "Limpieza nivel 2 completada." -ForegroundColor Green
    Write-Host "LocalDB desinstalado. El instalador debera instalar SqlLocalDB.msi." -ForegroundColor DarkGray
    exit 0
}

# =============================================================================
# NIVEL 3 -- Desinstalar la app SchettiniGestion
# =============================================================================
Write-Step "Desinstalando SCHPOS..."

$appEntry = $null
foreach ($root in $regRoots) {
    if (-not (Test-Path $root)) { continue }
    $appEntry = Get-ChildItem $root -ErrorAction SilentlyContinue |
        Get-ItemProperty -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -like "*SCHPOS*" -or $_.DisplayName -like "*Schettini*" } |
        Select-Object -First 1
    if ($appEntry) { break }
}

if ($appEntry) {
    Write-Host "   Encontrado: $($appEntry.DisplayName)" -ForegroundColor Gray
    $uninstCmd = $appEntry.UninstallString
    if ($uninstCmd -match '^"(.+)"\s*(.*)$') {
        $exe  = $matches[1]
        $args = ($matches[2] + " /SILENT").Trim()
    } elseif ($uninstCmd -match '^(\S+)\s*(.*)$') {
        $exe  = $matches[1]
        $args = ($matches[2] + " /SILENT").Trim()
    }
    Write-Host "   Ejecutando: $exe $args" -ForegroundColor Gray
    $proc = Start-Process $exe -ArgumentList $args -PassThru -Wait
    if ($proc.ExitCode -eq 0) {
        Write-OK "Aplicacion desinstalada"
    } else {
        Write-Warn "Desinstalador ExitCode=$($proc.ExitCode)"
    }
} else {
    Write-Skip "SCHPOS no encontrado en el registro (quizas no estaba instalado)"
}

# Eliminar carpeta residual
foreach ($d in @("$env:ProgramFiles\SCHPOS", "${env:ProgramFiles(x86)}\SCHPOS")) {
    if (Test-Path $d) {
        Remove-Item $d -Recurse -Force -ErrorAction SilentlyContinue
        Write-OK "Carpeta eliminada: $d"
    }
}

Write-Host ""
Write-Host "Limpieza nivel 3 completada. Entorno completamente virgen." -ForegroundColor Green
Write-Host "Ahora puedes ejecutar el instalador .exe como si fuera una PC nueva." -ForegroundColor DarkGray
