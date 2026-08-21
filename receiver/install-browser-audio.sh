#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SERVICE_DIR="${XDG_CONFIG_HOME:-${HOME}/.config}/systemd/user"
SERVICE_NAME="cometen-irl-browser-audio.service"
TEMPLATE="${SCRIPT_DIR}/cometen-irl-browser-audio-user.service"
CONFIG_PATH="${SCRIPT_DIR}/config.json"

missing=()

for command in python3 systemctl pw-cli wpctl xvfb-run xauth; do
  if ! command -v "${command}" >/dev/null 2>&1; then
    missing+=("${command}")
  fi
done

browser=""
for candidate in chromium chromium-browser google-chrome-stable google-chrome; do
  if command -v "${candidate}" >/dev/null 2>&1; then
    browser="$(command -v "${candidate}")"
    break
  fi
done

if [[ -z "${browser}" ]]; then
  missing+=("chromium/chrome")
fi

if [[ ! -f "${CONFIG_PATH}" ]]; then
  echo "Mangler ${CONFIG_PATH}"
  echo "Kopier config.example.json til config.json og konfigurer receiveren først."
  exit 1
fi

if [[ ! -f "${TEMPLATE}" ]]; then
  echo "Mangler service-malen ${TEMPLATE}"
  exit 1
fi

python3 -m json.tool "${CONFIG_PATH}" >/dev/null

browser_enabled="$(
  python3 - "${CONFIG_PATH}" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
config = json.loads(path.read_text(encoding="utf-8"))
print("true" if bool(config.get("browser_audio_enabled", False)) else "false")
PY
)"

if [[ "${browser_enabled}" != "true" ]]; then
  echo "browser_audio_enabled er ikke aktivert i config.json."
  echo "Kjør først:"
  echo "  python3 configure-browser-audio.py"
  exit 1
fi

if ((${#missing[@]} > 0)); then
  echo "Mangler nødvendige komponenter:"
  printf '  - %s\n' "${missing[@]}"
  echo
  echo "På Debian/Ubuntu/BELABOX-image med apt er typisk kommando:"
  echo "  sudo apt update"
  echo "  sudo apt install -y xvfb xauth chromium"
  echo
  echo "Installer bare manglende pakker. Ikke kjør full systemoppgradering."
  exit 1
fi

mkdir -p "${SERVICE_DIR}"

sed "s|@RECEIVER_DIR@|${SCRIPT_DIR}|g" \
  "${TEMPLATE}" \
  > "${SERVICE_DIR}/${SERVICE_NAME}"

systemctl --user daemon-reload
systemctl --user enable --now "${SERVICE_NAME}"

echo
echo "Cometen IRL Browser Audio er installert."
echo "Browser: ${browser}"
echo
echo "Status:"
echo "  systemctl --user status ${SERVICE_NAME}"
echo
echo "Logg:"
echo "  journalctl --user -u ${SERVICE_NAME} -f"
echo
echo "Test deretter 'Play test alert' i Sound Alerts Dashboard."
