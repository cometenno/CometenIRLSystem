#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_PATH="${SCRIPT_DIR}/config.json"
RUNTIME_ROOT="${XDG_DATA_HOME:-${HOME}/.local/share}/cometen-irl-browser-audio"
VENV_DIR="${RUNTIME_ROOT}/venv"
BROWSERS_DIR="${RUNTIME_ROOT}/ms-playwright"
TMP_DIR="${RUNTIME_ROOT}/tmp"

if [[ ! -f "${CONFIG_PATH}" ]]; then
  echo "Missing ${CONFIG_PATH}"
  exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 was not found."
  exit 1
fi

if ! python3 -m venv --help >/dev/null 2>&1; then
  echo "python3-venv is missing. Install it first:"
  echo "  sudo apt install -y python3-venv"
  exit 1
fi

mkdir -p "${RUNTIME_ROOT}" "${BROWSERS_DIR}" "${TMP_DIR}"
chmod 700 "${RUNTIME_ROOT}" "${TMP_DIR}" || true

# BELABOX images can have a small tmpfs-backed /tmp even when / has hundreds of GB free.
# Force Playwright/pip temporary downloads and extraction to the runtime directory on /home.
export TMPDIR="${TMP_DIR}"
export TMP="${TMP_DIR}"
export TEMP="${TMP_DIR}"

if [[ ! -x "${VENV_DIR}/bin/python" ]]; then
  python3 -m venv "${VENV_DIR}"
fi

"${VENV_DIR}/bin/python" -m pip install --upgrade pip
"${VENV_DIR}/bin/python" -m pip install 'playwright>=1.57,<2'

PLAYWRIGHT_BROWSERS_PATH="${BROWSERS_DIR}" \
  "${VENV_DIR}/bin/python" -m playwright install chromium

browser_path="$(
  PLAYWRIGHT_BROWSERS_PATH="${BROWSERS_DIR}" \
    "${VENV_DIR}/bin/python" - <<'PY'
from playwright.sync_api import sync_playwright

with sync_playwright() as p:
    print(p.chromium.executable_path)
PY
)"

if [[ -z "${browser_path}" || ! -x "${browser_path}" ]]; then
  echo "Playwright Chromium was downloaded, but the executable was not found."
  exit 1
fi

python3 - "${CONFIG_PATH}" "${browser_path}" <<'PY'
import json
import os
import sys
from pathlib import Path

path = Path(sys.argv[1])
browser = sys.argv[2]
data = json.loads(path.read_text(encoding="utf-8"))
data["browser_audio_browser"] = browser

tmp = path.with_suffix(path.suffix + ".tmp")
tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
os.replace(tmp, path)
PY

echo
echo "Playwright Chromium is installed locally for Cometen IRL System Browser Audio."
echo "Browser: ${browser_path}"
echo "config.json was updated with the local browser path."
echo
echo "Next step:"
echo "  bash install-browser-audio.sh"
