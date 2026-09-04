#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
HELPER_SRC="${SCRIPT_DIR}/cometen-irl-admin-helper"
HELPER_DST="/usr/local/sbin/cometen-irl-admin-helper"
SUDOERS_DST="/etc/sudoers.d/cometen-irl-admin-helper"
RUN_USER="${SUDO_USER:-user}"

if [[ ! -f "$HELPER_SRC" ]]; then
    echo "Missing helper: $HELPER_SRC" >&2
    exit 1
fi

if ! id "$RUN_USER" >/dev/null 2>&1; then
    echo "User '$RUN_USER' does not exist" >&2
    exit 1
fi

echo "Installing Cometen IRL BELABOX admin helper for user ${RUN_USER}..."

sudo install -m 0755 "$HELPER_SRC" "$HELPER_DST"

TMP="$(mktemp)"
cat >"$TMP" <<EOF
# Cometen IRL Web Admin - allow only the validated helper to run as root.
${RUN_USER} ALL=(root) NOPASSWD: ${HELPER_DST} *
EOF

if command -v visudo >/dev/null 2>&1; then
    sudo visudo -cf "$TMP" >/dev/null
fi
sudo install -m 0440 "$TMP" "$SUDOERS_DST"
rm -f "$TMP"

echo
echo "Helper test:"
sudo -u "$RUN_USER" sudo -n "$HELPER_DST" status

echo
echo "Installed:"
echo "  $HELPER_DST"
echo "  $SUDOERS_DST"
echo
echo "Restart the user receiver after updating receiver/admin_control.py and run_receiver.py:"
echo "  systemctl --user restart cometen-irl-alerts.service"
