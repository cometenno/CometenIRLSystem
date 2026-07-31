#!/usr/bin/env bash
set -euo pipefail

SERVICE_NAME="cometen-irl-alerts.service"
INSTALL_DIR="/opt/cometen-irl-alerts"

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this uninstaller with sudo: sudo ./uninstall.sh"
  exit 1
fi

systemctl disable --now "${SERVICE_NAME}" 2>/dev/null || true
rm -f "/etc/systemd/system/${SERVICE_NAME}"
systemctl daemon-reload

echo "Service removed."
echo "Configuration and sounds remain in ${INSTALL_DIR}."
echo "Delete that directory manually only when you no longer need it."
