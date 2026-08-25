#!/usr/bin/env bash
# Despliegue de AdminLicencias.Api en Ubuntu
# Uso: sudo bash deploy/ubuntu/deploy-api.sh
#
# ANTES del primer deploy con endurecimiento 1.3, asegurate de tener en
# /etc/schpos/license-api.env al menos:
#   ApiSecurity__AdminApiKey=...
#   ApiSecurity__PosApiKey=...
#   Licensing__SecretKey=...   # misma AES que SCHPOS LicenseManager
# Ver license-api.env.example

set -euo pipefail

REPO_DIR="${REPO_DIR:-/var/www/SchettiniGestion}"
PUBLISH_DIR="${PUBLISH_DIR:-/var/www/SchettiniGestion/publish}"
SERVICE_NAME="${SERVICE_NAME:-schettinigestion-api}"
ENV_FILE="${ENV_FILE:-/etc/schpos/license-api.env}"

echo "==> Repo:    $REPO_DIR"
echo "==> Publish: $PUBLISH_DIR"
echo "==> Env:     $ENV_FILE"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: falta $ENV_FILE — copiá deploy/ubuntu/license-api.env.example y completá secretos."
  exit 1
fi

if ! grep -q 'Licensing__SecretKey=.\+' "$ENV_FILE"; then
  echo "ERROR: Licensing__SecretKey vacío o ausente en $ENV_FILE (la API no arranca en Production)."
  exit 1
fi

if ! grep -q 'ApiSecurity__AdminApiKey=.\+' "$ENV_FILE"; then
  echo "ERROR: ApiSecurity__AdminApiKey vacío o ausente en $ENV_FILE."
  exit 1
fi

if ! grep -q 'ApiSecurity__PosApiKey=.\+' "$ENV_FILE"; then
  echo "ERROR: ApiSecurity__PosApiKey vacío o ausente (obligatorio para /validate en Production)."
  exit 1
fi

cd "$REPO_DIR"
git pull origin main

echo "==> Stopping $SERVICE_NAME (libera DLL/PDB bloqueados)"
systemctl stop "$SERVICE_NAME" || true
sleep 1

mkdir -p /var/lib/schpos-licenses/dp-keys

dotnet publish "$REPO_DIR/AdminLicencias.Api/AdminLicencias.Api.csproj" \
  -c Release -r linux-x64 --self-contained false \
  -p:DebugType=None -p:DebugSymbols=false \
  -o "$PUBLISH_DIR"

chown -R www-data:www-data "$PUBLISH_DIR" /var/lib/schpos-licenses
systemctl start "$SERVICE_NAME"
sleep 2
systemctl --no-pager status "$SERVICE_NAME"

echo ""
echo "Listo. Panel: https://licencias.schpos.com.ar/"
echo "Verificá: curl -sI https://licencias.schpos.com.ar/ | head -5"
echo "          curl -s https://licencias.schpos.com.ar/api/version"
echo "          curl -s https://licencias.schpos.com.ar/actualizaciones.json | head -20"
