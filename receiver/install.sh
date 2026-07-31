#!/usr/bin/env bash
set -euo pipefail

INSTALL_DIR="/opt/cometen-irl-alerts"
SERVICE_NAME="cometen-irl-alerts.service"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
RUN_USER="${SUDO_USER:-${USER}}"

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this installer with sudo: sudo ./install.sh"
  exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 is required but was not found."
  exit 1
fi

install -d -m 0755 "${INSTALL_DIR}"
install -d -m 0755 "${INSTALL_DIR}/sounds"
install -m 0755 "${SCRIPT_DIR}/receiver.py" "${INSTALL_DIR}/receiver.py"
install -m 0644 "${SCRIPT_DIR}/config.example.json" "${INSTALL_DIR}/config.example.json"
install -m 0644 "${SCRIPT_DIR}/sounds/README.md" "${INSTALL_DIR}/sounds/README.md"

if [[ ! -f "${INSTALL_DIR}/config.json" ]]; then
  install -m 0600 "${SCRIPT_DIR}/config.example.json" "${INSTALL_DIR}/config.json"
fi

chown -R "${RUN_USER}:${RUN_USER}" "${INSTALL_DIR}"

sed \
  -e "s|@RUN_USER@|${RUN_USER}|g" \
  -e "s|@INSTALL_DIR@|${INSTALL_DIR}|g" \
  "${SCRIPT_DIR}/${SERVICE_NAME}" > "/etc/systemd/system/${SERVICE_NAME}"

chmod 0644 "/etc/systemd/system/${SERVICE_NAME}"
systemctl daemon-reload
systemctl enable "${SERVICE_NAME}"

echo
echo "Installed to ${INSTALL_DIR}"
echo "Service user: ${RUN_USER}"
echo
echo "Next steps:"
echo "  1. Edit ${INSTALL_DIR}/config.json"
echo "  2. Add WAV files to ${INSTALL_DIR}/sounds/"
echo "  3. Verify Bluetooth/audio output manually"
echo "  4. Start with: sudo systemctl start ${SERVICE_NAME}"
echo "  5. View logs with: journalctl -u ${SERVICE_NAME} -f"
