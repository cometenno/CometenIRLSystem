#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
TARGET_USER="${SUDO_USER:-${USER}}"
RULE_PATH="/etc/udev/rules.d/60-cometen-gpio.rules"
VIDEO_TEMPLATE="${SCRIPT_DIR}/cometen-irl-video-probe.service"
VIDEO_SERVICE="cometen-irl-video-probe.service"
VIDEO_SERVICE_PATH="/etc/systemd/system/${VIDEO_SERVICE}"

if ! id "${TARGET_USER}" >/dev/null 2>&1; then
  echo "User not found: ${TARGET_USER}"
  exit 1
fi

if [[ ! -f "${VIDEO_TEMPLATE}" ]]; then
  echo "Missing service template ${VIDEO_TEMPLATE}"
  exit 1
fi

if [[ ! -f "${SCRIPT_DIR}/video_signal_probe.py" ]]; then
  echo "Missing ${SCRIPT_DIR}/video_signal_probe.py"
  exit 1
fi

echo "Installing GPIO support for Cometen IRL System..."
sudo apt update
sudo apt install -y gpiod python3-libgpiod v4l-utils

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
sudo systemctl enable "${VIDEO_SERVICE}"
# Always restart on install/update so the running Python process loads the
# newest video_signal_probe.py code from the repository.
sudo systemctl restart "${VIDEO_SERVICE}"

echo
echo "GPIO support is installed."
echo "User ${TARGET_USER} was added to the gpio group."
echo "The video probe is installed/restarted as root service: ${VIDEO_SERVICE}"
echo
echo "Video probe status:"
echo "  sudo systemctl status ${VIDEO_SERVICE} --no-pager"
echo "  cat /run/cometen-irl-video-status.json"
echo
echo "On BELABOX, one reboot is recommended after the first GPIO installation/group change:"
echo "  sudo reboot"
echo
echo "If gpio group membership was already active, restart the receiver directly:"
echo "  systemctl --user restart cometen-irl-alerts.service"
echo
echo "GPIO pins can be checked with:"
echo "  gpiofind PIN_32"
echo "  gpiofind PIN_36"
echo "  gpiofind PIN_38"
echo "  gpiofind PIN_40"
