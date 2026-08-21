#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SERVICE_DIR="${XDG_CONFIG_HOME:-${HOME}/.config}/systemd/user"
CONFIG_PATH="${SCRIPT_DIR}/config.json"

ALERT_SERVICE="cometen-irl-alerts.service"
ALERT_TEMPLATE="${SCRIPT_DIR}/cometen-irl-alerts-user.service"
HEARTBEAT_SERVICE="cometen-irl-heartbeat.service"
HEARTBEAT_TEMPLATE="${SCRIPT_DIR}/cometen-irl-heartbeat-user.service"

if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 was not found."
  exit 1
fi

if ! command -v systemctl >/dev/null 2>&1; then
  echo "systemctl was not found."
  exit 1
fi

if [[ ! -f "${CONFIG_PATH}" ]]; then
  echo "Missing ${CONFIG_PATH}"
  echo "Copy config.example.json to config.json and configure it first."
  exit 1
fi

for template in "${ALERT_TEMPLATE}" "${HEARTBEAT_TEMPLATE}"; do
  if [[ ! -f "${template}" ]]; then
    echo "Missing service template ${template}"
    exit 1
  fi
done

python3 -m json.tool "${CONFIG_PATH}" >/dev/null

mkdir -p "${SERVICE_DIR}"

sed "s|@RECEIVER_DIR@|${SCRIPT_DIR}|g" \
  "${ALERT_TEMPLATE}" \
  > "${SERVICE_DIR}/${ALERT_SERVICE}"

sed "s|@RECEIVER_DIR@|${SCRIPT_DIR}|g" \
  "${HEARTBEAT_TEMPLATE}" \
  > "${SERVICE_DIR}/${HEARTBEAT_SERVICE}"

systemctl --user daemon-reload
systemctl --user enable --now "${ALERT_SERVICE}"
systemctl --user enable --now "${HEARTBEAT_SERVICE}"

echo
echo "Cometen IRL System receiver services are installed."
echo
echo "Alert receiver:"
echo "  Status: systemctl --user status ${ALERT_SERVICE}"
echo "  Log:    journalctl --user -u ${ALERT_SERVICE} -f"
echo
echo "Heartbeat:"
echo "  Status: systemctl --user status ${HEARTBEAT_SERVICE}"
echo "  Log:    journalctl --user -u ${HEARTBEAT_SERVICE} -f"
echo
echo "For startup without an interactive login, run once:"
echo "sudo loginctl enable-linger ${USER}"
