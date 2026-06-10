# Copia el ejecutable WPF Release y todas las DLL al subdirectorio SalidaDespliegue (listo para probar o zip).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "SchettiniGestion.WPF\bin\Release"
$dst = Join-Path $root "SalidaDespliegue"
if (-not (Test-Path $src)) { throw "No existe $src. Compilá Release del proyecto WPF primero." }
if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
New-Item -ItemType Directory -Path $dst | Out-Null
Copy-Item -Path (Join-Path $src "*") -Destination $dst -Recurse
Write-Host "Listo: $dst ($((Get-ChildItem $dst -File).Count) archivos)"
