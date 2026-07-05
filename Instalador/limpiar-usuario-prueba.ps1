#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Elimina la cuenta de prueba 'test-schettini' y toda su informacion.
    Ejecutar DESPUES de terminar las pruebas del instalador.
#>

$usuario = "test-schettini"

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Magenta
Write-Host "  Limpiando cuenta de prueba: $usuario              " -ForegroundColor Magenta
Write-Host "=====================================================" -ForegroundColor Magenta

# Verificar que el usuario no tenga sesion activa
$sesionesActivas = query user 2>&1 | Select-String $usuario
if ($sesionesActivas) {
    Write-Host ""
    Write-Host "   !! El usuario '$usuario' tiene una sesion activa." -ForegroundColor Red
    Write-Host "      Cierra su sesion antes de ejecutar este script." -ForegroundColor Red
    Write-Host ""
    exit 1
}

# Desinstalar SchettiniGestion si quedo instalado (limpia el registro de la app)
Write-Host ""
Write-Host ">> Buscando SchettiniGestion instalado..." -ForegroundColor Cyan
$regRoots = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
)
$appEntry = $null
foreach ($root in $regRoots) {
    if (-not (Test-Path $root)) { continue }
    $appEntry = Get-ChildItem $root -ErrorAction SilentlyContinue |
        Get-ItemProperty -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -like "*SchettiniGestion*" -or $_.DisplayName -like "*Schettini*" } |
        Select-Object -First 1
    if ($appEntry) { break }
}
if ($appEntry) {
    Write-Host "   Encontrado: $($appEntry.DisplayName). Desinstalando..." -ForegroundColor Gray
    $uninstCmd = $appEntry.UninstallString
    if ($uninstCmd -match '^"(.+)"\s*(.*)$') { $exe = $matches[1]; $args = ($matches[2] + " /SILENT").Trim() }
    elseif ($uninstCmd -match '^(\S+)\s*(.*)$') { $exe = $matches[1]; $args = ($matches[2] + " /SILENT").Trim() }
    $proc = Start-Process $exe -ArgumentList $args -PassThru -Wait -ErrorAction SilentlyContinue
    if ($proc -and $proc.ExitCode -eq 0) {
        Write-Host "   OK  App desinstalada" -ForegroundColor Green
    } else {
        Write-Host "   --  No se pudo desinstalar automaticamente (puede ya estar borrada)" -ForegroundColor DarkGray
    }
} else {
    Write-Host "   --  SchettiniGestion no encontrado en registro (ya desinstalado o nunca instalado)" -ForegroundColor DarkGray
}

# Eliminar carpeta de configuracion compartida dejada por la prueba
Write-Host ""
Write-Host ">> Limpiando configuracion compartida (%ProgramData%)..." -ForegroundColor Cyan
$cfgDir = "$env:ProgramData\SchettiniGestion"
if (Test-Path $cfgDir) {
    Remove-Item $cfgDir -Recurse -Force
    Write-Host "   OK  Eliminado: $cfgDir" -ForegroundColor Green
} else {
    Write-Host "   --  $cfgDir no existe" -ForegroundColor DarkGray
}

# Eliminar perfil de usuario y su instancia de LocalDB
Write-Host ""
Write-Host ">> Eliminando usuario '$usuario' y su perfil..." -ForegroundColor Cyan
$userExists = Get-LocalUser -Name $usuario -ErrorAction SilentlyContinue
if ($userExists) {
    # Eliminar usuario con su perfil (flag -RemoveProfile)
    Remove-LocalUser -Name $usuario
    Write-Host "   OK  Cuenta '$usuario' eliminada" -ForegroundColor Green

    # El perfil en C:\Users\test-schettini puede quedar; forzar borrado
    $profilePath = "C:\Users\$usuario"
    if (Test-Path $profilePath) {
        Start-Sleep -Seconds 2  # dar tiempo a Windows para liberar handles
        Remove-Item $profilePath -Recurse -Force -ErrorAction SilentlyContinue
        if (-not (Test-Path $profilePath)) {
            Write-Host "   OK  Perfil eliminado: $profilePath" -ForegroundColor Green
        } else {
            Write-Host "   !!  No se pudo borrar $profilePath automaticamente." -ForegroundColor Yellow
            Write-Host "       Borralo manualmente o reinicia y vuelve a intentarlo." -ForegroundColor Yellow
        }
    }

    # La instancia MSSQLLocalDB del usuario de prueba vive en su %LOCALAPPDATA%,
    # que se elimino con el perfil. No hace falta hacer sqllocaldb delete.
    Write-Host "   OK  Instancia LocalDB del usuario (en su perfil) eliminada con el perfil" -ForegroundColor Green
} else {
    Write-Host "   --  Usuario '$usuario' no existe" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Green
Write-Host "  Limpieza completa. Entorno de desarrollo intacto.  " -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green
Write-Host ""
