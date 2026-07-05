#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Crea una cuenta local de Windows aislada para probar el instalador de SchettiniGestion
    sin tocar el entorno de desarrollo.

    LocalDB es per-usuario: la cuenta nueva tendra una instancia MSSQLLocalDB propia,
    completamente separada de la del desarrollador.

.USO
    1. Ejecutar este script COMO ADMINISTRADOR
    2. Anotar la contrasena que imprime
    3. Cambiar de usuario (NO cerrar sesion actual: Win+L -> "Otro usuario" o boton de usuario)
    4. Iniciar sesion con "test-schettini"
    5. Instalar y probar SchettiniGestion-Setup-*.exe
    6. Volver a la sesion de desarrollo
    7. Ejecutar limpiar-usuario-prueba.ps1 para eliminar la cuenta
#>

$usuario    = "test-schettini"
$password   = "Test1234!"   # contrasena simple para entorno de prueba

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "  Creando usuario de prueba: $usuario               " -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

# Crear usuario local
$secPass = ConvertTo-SecureString $password -AsPlainText -Force
$existing = Get-LocalUser -Name $usuario -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "   El usuario '$usuario' ya existe. Restableciendo contrasena..." -ForegroundColor Yellow
    Set-LocalUser -Name $usuario -Password $secPass
} else {
    New-LocalUser -Name $usuario -Password $secPass `
        -FullName "Test SchettiniGestion" `
        -Description "Cuenta temporal para probar el instalador" `
        -PasswordNeverExpires $true | Out-Null
    Write-Host "   OK  Usuario '$usuario' creado" -ForegroundColor Green
}

# Agregar al grupo Administradores (el instalador requiere admin)
Add-LocalGroupMember -Group "Administradores" -Member $usuario -ErrorAction SilentlyContinue
if ($LASTEXITCODE -eq 0 -or $?) {
    Write-Host "   OK  Agregado al grupo Administradores" -ForegroundColor Green
}

# Copiar el instalador al escritorio del nuevo usuario para que sea facil de encontrar
$instaladorSrc = Get-ChildItem "C:\schpos\SchettiniGestion\Instalador\Output" -Filter "*.exe" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($instaladorSrc) {
    $escritorioTest = "C:\Users\$usuario\Desktop"
    # El escritorio del usuario se crea al primer login; lo creamos ahora para poder copiar
    if (-not (Test-Path $escritorioTest)) { New-Item -ItemType Directory -Path $escritorioTest -Force | Out-Null }
    Copy-Item $instaladorSrc.FullName $escritorioTest -Force
    Write-Host "   OK  Instalador copiado al escritorio de '$usuario': $($instaladorSrc.Name)" -ForegroundColor Green
} else {
    Write-Host "   --  No se encontro un .exe en Instalador\Output. Copialo manualmente." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Green
Write-Host "  LISTO. Pasos para probar:                          " -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Usuario  : $usuario" -ForegroundColor White
Write-Host "  Password : $password" -ForegroundColor White
Write-Host ""
Write-Host "  1. Presiona Win+L  (bloquear pantalla)" -ForegroundColor Yellow
Write-Host "  2. Click en 'Otro usuario' (esquina inferior izquierda)" -ForegroundColor Yellow
Write-Host "  3. Inicia sesion con las credenciales de arriba" -ForegroundColor Yellow
Write-Host "  4. En el escritorio encontraras el instalador .exe listo" -ForegroundColor Yellow
Write-Host "  5. Cuando termines, cierra sesion de '$usuario'" -ForegroundColor Yellow
Write-Host "  6. Vuelve a tu sesion de desarrollo" -ForegroundColor Yellow
Write-Host "  7. Ejecuta limpiar-usuario-prueba.ps1 para borrar todo" -ForegroundColor Yellow
Write-Host ""
Write-Host "  IMPORTANTE: NO cierres tu sesion de desarrollo," -ForegroundColor Cyan
Write-Host "  usa cambio rapido de usuario (Fast User Switch)." -ForegroundColor Cyan
Write-Host ""
