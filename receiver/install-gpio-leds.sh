#!/usr/bin/env bash
set -euo pipefail

TARGET_USER="${SUDO_USER:-${USER}}"
RULE_PATH="/etc/udev/rules.d/60-cometen-gpio.rules"

if ! id "${TARGET_USER}" >/dev/null 2>&1; then
  echo "Fant ikke bruker: ${TARGET_USER}"
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

echo
echo "GPIO-støtte er installert."
echo "Bruker ${TARGET_USER} er lagt i gruppen gpio."
echo
echo "På BELABOX anbefales én reboot før LED-tjenesten testes:"
echo "  sudo reboot"
echo
echo "Etter reboot kan pinnene kontrolleres med:"
echo "  gpiofind PIN_32"
echo "  gpiofind PIN_36"
echo "  gpiofind PIN_38"
echo "  gpiofind PIN_40"
