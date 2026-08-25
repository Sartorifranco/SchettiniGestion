Prerequisitos del instalador SchettiniGestion
=============================================

Los archivos grandes NO van en Git. El script de build los descarga automaticamente:

  .\Instalador\download-prerequisites.ps1

O se descargan solos al ejecutar:

  .\Instalador\build-release.ps1 -BuildInstaller

Archivos esperados (generados localmente):
  - SqlLocalDB.msi
  - ndp48-x86-x64-allos-enu.exe   (.NET Framework 4.8 offline)

La licencia NO va en el Setup. El cliente activa al abrir SCHPOS.
