#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
TARGET_USER="${SUDO_USER:-${USER}}"
RULE_PATH="/etc/udev/rules.d/60-cometen-gpio.rules"
VIDEO_TEMPLATE="${SCRIPT_DIR}/cometen-irl-video-probe.service"
VIDEO_SERVICE="cometen-irl-video-probe.service"
VIDEO_SERVICE_PATH="/etc/systemd/system/${VIDEO_SERVICE}"

if ! id "${TARGET_USER}" >/dev/null 2>&1; then
  echo "Fant ikke bruker: ${TARGET_USER}"
  exit 1
fi

if [[ ! -f "${VIDEO_TEMPLATE}" ]]; then
  echo "Mangler service-malen ${VIDEO_TEMPLATE}"
  exit 1
fi

if [[ ! -f "${SCRIPT_DIR}/video_signal_probe.py" ]]; then
  echo "Mangler ${SCRIPT_DIR}/video_signal_probe.py"
  exit 1
fi

echo "Installerer GPIO-støtte for Cometen IRL Alerts..."
sudo apt update
sudo apt install -y gpiod python3-libgpiod

if ! getent group gpio >/dev/null 2>&1; then
  sudo groupadd --system gpio
fi

sudo usermod -aG gpio "${TARGET_USER}"

sudo tee "${RULE_PATH}" >/dev/null <<'EOF'
SUBSYSTEM=="gpio", KERNEL=="gpiochip*", GROUP="gpio", MODE="0660"
EOF

sudo udevadm control --reload-rules
sudo udevadm trigger --subsystem-match=gpio || true

if compgen -G "/dev/gpiochip*" >/dev/null; then
  sudo chgrp gpio /dev/gpiochip*
  sudo chmod 0660 /dev/gpiochip*
fi

sed "s|@RECEIVER_DIR@|${SCRIPT_DIR}|g" "${VIDEO_TEMPLATE}" \
  | sudo tee "${VIDEO_SERVICE_PATH}" >/dev/null
sudo systemctl daemon-reload
sudo systemctl enable --now "${VIDEO_SERVICE}"

echo
echo "GPIO-støtte er installert."
echo "Bruker ${TARGET_USER} er lagt i gruppen gpio."
echo "Video-probe er installert som root-tjeneste: ${VIDEO_SERVICE}"
echo
echo "Status video-probe:"
echo "  sudo systemctl status ${VIDEO_SERVICE} --no-pager"
echo "  cat /run/cometen-irl-video-status.json"
echo
echo "På BELABOX anbefales én reboot etter første GPIO-installasjon / gruppeendring:"
echo "  sudo reboot"
echo
echo "Hvis GPIO-gruppen allerede var aktiv, kan LED-receiveren restartes direkte:"
echo "  systemctl --user restart cometen-irl-alerts.service"
echo
echo "Pinnene kan kontrolleres med:"
echo "  gpiofind PIN_32"
echo "  gpiofind PIN_36"
echo "  gpiofind PIN_38"
echo "  gpiofind PIN_40"
