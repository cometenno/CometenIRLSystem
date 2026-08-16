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

for template in "${ALERT_TEMPLATE}" "${HEARTBEAT_TEMPLATE}"; do
  if [[ ! -f "${template}" ]]; then
    echo "Mangler service-malen ${template}"
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
echo "Cometen IRL Alerts er installert som brukertjenester."
echo
echo "Alert receiver:"
echo "  Status: systemctl --user status ${ALERT_SERVICE}"
echo "  Logg:   journalctl --user -u ${ALERT_SERVICE} -f"
echo
echo "Heartbeat:"
echo "  Status: systemctl --user status ${HEARTBEAT_SERVICE}"
echo "  Logg:   journalctl --user -u ${HEARTBEAT_SERVICE} -f"
echo
echo "For oppstart uten innlogging, kjør én gang:"
echo "sudo loginctl enable-linger ${USER}"
