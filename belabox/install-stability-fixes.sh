#!/usr/bin/env bash
set -euo pipefail

if (( EUID != 0 )); then
    echo "Run with sudo: sudo bash belabox/install-stability-fixes.sh"
    exit 1
fi

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
STAMP=$(date +%Y%m%d-%H%M%S)

FAN_SRC="$SCRIPT_DIR/cometen-fan-control"
FAN_DST=/usr/local/sbin/cometen-fan-control
JOURNAL_SRC="$SCRIPT_DIR/99-cometen-persistent-journal.conf"
JOURNAL_DIR=/etc/systemd/journald.conf.d
JOURNAL_DST="$JOURNAL_DIR/99-cometen-persistent-journal.conf"

[[ -f "$FAN_SRC" ]] || { echo "Missing $FAN_SRC"; exit 1; }
[[ -f "$JOURNAL_SRC" ]] || { echo "Missing $JOURNAL_SRC"; exit 1; }

# Preserve the currently installed fan controller before replacing it.
if [[ -f "$FAN_DST" ]]; then
    cp -a "$FAN_DST" "${FAN_DST}.bak-${STAMP}"
    echo "Fan backup: ${FAN_DST}.bak-${STAMP}"
fi

install -m 0755 "$FAN_SRC" "$FAN_DST"

install -d -m 0755 "$JOURNAL_DIR"
install -m 0644 "$JOURNAL_SRC" "$JOURNAL_DST"
install -d -m 2755 /var/log/journal
systemd-tmpfiles --create --prefix /var/log/journal

# These restarts do not restart belaUI, networking or the stream pipeline.
systemctl restart systemd-journald
journalctl --flush || true
systemctl restart cometen-fan.service

printf '\n=== VERIFY ===\n'
systemctl is-active cometen-fan.service
printf 'journald Storage: '
grep -h '^Storage=' /etc/systemd/journald.conf /etc/systemd/journald.conf.d/*.conf 2>/dev/null | tail -1 || true
printf 'journal directory: '
[[ -d /var/log/journal ]] && echo OK || echo MISSING
printf 'SoC temp: '
awk '{printf "%.1f C\n", $1/1000}' /sys/class/thermal/thermal_zone0/temp
printf 'fan state: '
cat /sys/class/thermal/cooling_device0/cur_state
printf 'journal disk use: '
journalctl --disk-usage
