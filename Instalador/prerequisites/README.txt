Prerequisitos del instalador SchettiniGestion
=============================================

Los archivos grandes NO van en Git. El script de build los descarga automaticamente:

  .\Instalador\download-prerequisites.ps1

O se descargan solos al ejecutar:

  .\Instalador\build-release.ps1 -BuildInstaller

Archivos esperados (generados localmente):
  - SqlLocalDB.msi
  - ndp48-x86-x64-allos-enu.exe   (.NET Framework 4.8 offline)

Opcional para testing sin pantalla de activacion:
  - licencia.key   (una linea con la clave Base64 de LicenseGenerator)

Generar licencia:
  dotnet run --project LicenseGenerator
