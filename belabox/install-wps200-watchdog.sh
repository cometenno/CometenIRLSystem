#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
WATCHDOG_SRC="${SCRIPT_DIR}/cometen-wps200-watchdog"
SERVICE_SRC="${SCRIPT_DIR}/cometen-wps200.service"
WATCHDOG_DST="/usr/local/sbin/cometen-wps200-watchdog"
SERVICE_DST="/etc/systemd/system/cometen-wps200.service"
ENV_DST="/etc/default/cometen-wps200"

if [[ ! -f "$WATCHDOG_SRC" || ! -f "$SERVICE_SRC" ]]; then
    echo "Missing watchdog/service files in ${SCRIPT_DIR}" >&2
    exit 1
fi

find_wps200_mac() {
    local candidate info paired name
    local -A seen=()

    while read -r _ candidate; do
        [[ -n "${candidate:-}" ]] || continue
        [[ -z "${seen[$candidate]:-}" ]] || continue
        seen[$candidate]=1

        info="$(bluetoothctl info "$candidate" 2>/dev/null || true)"
        [[ -n "$info" ]] || continue

        name="$(sed -n -E 's/^[[:space:]]*(Name|Alias):[[:space:]]*(.*)$/\2/p' <<<"$info" | head -1)"
        paired="$(sed -n -E 's/^[[:space:]]*Paired:[[:space:]]*(yes|no).*$/\1/p' <<<"$info" | head -1)"

        if [[ "$name" == "WPS200" && "$paired" == "yes" ]]; then
            echo "$candidate"
            return 0
        fi
    done < <(
        {
            bluetoothctl paired-devices 2>/dev/null || true
            bluetoothctl devices Paired 2>/dev/null || true
            bluetoothctl devices 2>/dev/null || true
        } | awk 'NF >= 2 && $1 == "Device" {print $1, $2}'
    )

    return 1
}

MAC="$(find_wps200_mac || true)"

# Existing v2 install: reuse local environment file.
if [[ -z "$MAC" && -f "$ENV_DST" ]]; then
    MAC="$(sed -n 's/^WPS200_MAC=//p' "$ENV_DST" | head -1)"
fi

# Migration from the original watchdog, where the MAC was stored directly
# in /usr/local/sbin/cometen-wps200-watchdog.
if [[ -z "$MAC" && -f "$WATCHDOG_DST" ]]; then
    MAC="$(sed -n -E 's/^MAC="([^"]+)".*$/\1/p' "$WATCHDOG_DST" | head -1)"
fi

if [[ -z "$MAC" ]]; then
    echo "No paired WPS200 and no existing local WPS200 configuration were found." >&2
    echo "Check with: bluetoothctl devices && bluetoothctl info <address>" >&2
    exit 1
fi

echo "Installing Cometen WPS200 watchdog v2.1..."

sudo install -m 0755 "$WATCHDOG_SRC" "$WATCHDOG_DST"
sudo install -m 0644 "$SERVICE_SRC" "$SERVICE_DST"

sudo tee "$ENV_DST" >/dev/null <<EOF
# Local BELABOX Bluetooth settings - not stored in GitHub
WPS200_MAC=${MAC}
WPS200_NAME=WPS200
COMETEN_AUDIO_USER=user
WPS200_CHECK_SECONDS=5
WPS200_CONNECT_TIMEOUT=8
WPS200_POWER_CYCLE_AFTER=3
WPS200_BLUEZ_RESTART_AFTER=6
EOF

sudo systemctl daemon-reload
sudo systemctl enable cometen-wps200.service >/dev/null
sudo systemctl restart cometen-wps200.service

sleep 2

echo
echo "Status:"
sudo systemctl status cometen-wps200.service --no-pager | head -25

echo
echo "Live log:"
echo "  sudo journalctl -f -u cometen-wps200.service -u bluetooth.service"
