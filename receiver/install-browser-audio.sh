#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SERVICE_DIR="${XDG_CONFIG_HOME:-${HOME}/.config}/systemd/user"
SERVICE_NAME="cometen-irl-browser-audio.service"
TEMPLATE="${SCRIPT_DIR}/cometen-irl-browser-audio-user.service"
CONFIG_PATH="${SCRIPT_DIR}/config.json"

if [[ ! -f "${CONFIG_PATH}" ]]; then
  echo "Missing ${CONFIG_PATH}"
  echo "Copy config.example.json to config.json and configure the receiver first."
  exit 1
fi

if [[ ! -f "${TEMPLATE}" ]]; then
  echo "Missing service template ${TEMPLATE}"
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
  echo "browser_audio_enabled is not enabled in config.json."
  echo "Run this first:"
  echo "  python3 configure-browser-audio.py"
  exit 1
fi

missing=()
for command in python3 systemctl pw-cli wpctl xvfb-run xauth; do
  if ! command -v "${command}" >/dev/null 2>&1; then
    missing+=("${command}")
  fi
done

if ((${#missing[@]} > 0)); then
  echo "Missing required system components:"
  printf '  - %s\n' "${missing[@]}"
  echo
  echo "On Ubuntu/BELABOX Jammy, these are normally installed with:"
  echo "  sudo apt install -y xvfb xauth"
  echo
  echo "Install only the missing packages. Do not perform a full system upgrade just for this module."
  exit 1
fi

configured_browser="$(
  python3 - "${CONFIG_PATH}" <<'PY'
import json
import os
import sys
from pathlib import Path

path = Path(sys.argv[1])
config = json.loads(path.read_text(encoding="utf-8"))
value = str(config.get("browser_audio_browser", "auto")).strip()
if value and value.lower() != "auto":
    print(os.path.expandvars(os.path.expanduser(value)))
PY
)"

browser=""
if [[ -n "${configured_browser}" && -x "${configured_browser}" ]]; then
  browser="${configured_browser}"
else
  for candidate in chromium chromium-browser google-chrome-stable google-chrome; do
    if command -v "${candidate}" >/dev/null 2>&1; then
      browser="$(command -v "${candidate}")"
      break
    fi
  done
fi

if [[ -z "${browser}" ]]; then
  echo "No suitable Chromium/Chrome runtime was found."
  echo
  echo "Ubuntu 22.04/Jammy uses Chromium as a snap transition package, so 'chromium'"
  echo "is not a normal apt package on this ARM64 image."
  echo "Do not install chromium-bsu; it is not the web browser required here."
  echo
  echo "Use the local Playwright Chromium runtime provided for Cometen IRL System Browser Audio:"
  echo "  sudo apt install -y python3-venv"
  echo "  bash install-browser-runtime.sh"
  echo
  echo "Then run this installer again."
  exit 1
fi

mkdir -p "${SERVICE_DIR}"

sed "s|@RECEIVER_DIR@|${SCRIPT_DIR}|g" \
  "${TEMPLATE}" \
  > "${SERVICE_DIR}/${SERVICE_NAME}"

systemctl --user daemon-reload
systemctl --user enable --now "${SERVICE_NAME}"

echo
echo "Cometen IRL System Browser Audio is installed."
echo "Browser: ${browser}"
echo
echo "Status:"
echo "  systemctl --user status ${SERVICE_NAME}"
echo
echo "Log:"
echo "  journalctl --user -u ${SERVICE_NAME} -f"
echo
echo "Then test with 'Play test alert' in the Sound Alerts Dashboard."
