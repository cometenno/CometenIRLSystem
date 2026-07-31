#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SERVICE_NAME="cometen-irl-alerts.service"
TEMPLATE="${SCRIPT_DIR}/cometen-irl-alerts-user.service"
SERVICE_DIR="${XDG_CONFIG_HOME:-${HOME}/.config}/systemd/user"
SERVICE_PATH="${SERVICE_DIR}/${SERVICE_NAME}"
CONFIG_PATH="${SCRIPT_DIR}/config.json"

if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 ble ikke funnet."
  exit 1
fi

if ! command -v systemctl >/dev/null 2>&1; then
  echo "systemctl ble ikke funnet."
  exit 1
fi

if [[ ! -f "${CONFIG_PATH}" ]]; then
  echo "Mangler ${CONFIG_PATH}"
  echo "Kopier config.example.json til config.json og fyll inn oppsettet først."
  exit 1
fi

if [[ ! -f "${TEMPLATE}" ]]; then
  echo "Mangler service-malen ${TEMPLATE}"
  exit 1
fi

mkdir -p "${SERVICE_DIR}"
sed "s|@RECEIVER_DIR@|${SCRIPT_DIR}|g" "${TEMPLATE}" > "${SERVICE_PATH}"

systemctl --user daemon-reload
systemctl --user enable --now "${SERVICE_NAME}"

echo
echo "Cometen IRL Alerts er installert som brukertjeneste."
echo "Status: systemctl --user status ${SERVICE_NAME}"
echo "Logg:   journalctl --user -u ${SERVICE_NAME} -f"
echo
echo "For oppstart uten innlogging, kjør én gang:"
echo "sudo loginctl enable-linger ${USER}"
