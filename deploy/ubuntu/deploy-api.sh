#!/usr/bin/env bash
# Despliegue rápido de AdminLicencias.Api en Ubuntu
# Uso: sudo bash deploy/ubuntu/deploy-api.sh

set -euo pipefail

REPO_DIR="${REPO_DIR:-/var/www/SchettiniGestion}"
PUBLISH_DIR="${PUBLISH_DIR:-/var/www/SchettiniGestion/publish}"
SERVICE_NAME="${SERVICE_NAME:-schettinigestion-api}"

echo "==> Repo:    $REPO_DIR"
echo "==> Publish: $PUBLISH_DIR"

cd "$REPO_DIR"
git pull origin main

echo "==> Stopping $SERVICE_NAME (libera DLL/PDB bloqueados)"
systemctl stop "$SERVICE_NAME" || true
sleep 1

dotnet publish "$REPO_DIR/AdminLicencias.Api/AdminLicencias.Api.csproj" \
  -c Release -r linux-x64 --self-contained false \
  -p:DebugType=None -p:DebugSymbols=false \
  -o "$PUBLISH_DIR"

chown -R www-data:www-data "$PUBLISH_DIR" /var/lib/schpos-licenses
systemctl start "$SERVICE_NAME"
systemctl --no-pager status "$SERVICE_NAME"

echo ""
echo "Listo. Panel: https://licencias.schpos.com.ar/"
echo "Verificá: curl -sI https://licencias.schpos.com.ar/ | head -5"
