# BELABOX ROCK 5B+ - Headless Bluetooth Alerts

This page documents the tested headless Bluetooth/audio setup used on the Cometen BELABOX with Radxa ROCK 5B+.

Goal:

```text
Power on BELABOX
    |
    v
BlueZ + Realtek Bluetooth driver
    |
    v
PipeWire + WirePlumber
    |
    v
WPS200 connects automatically
    |
    v
CometenIRLAlerts receiver + silent keepalive
    |
    v
Follow/Sub/etc. events from relay play locally
```

The important requirement is that normal IRL use must not depend on a local login or SSH session.

## Tested platform

The original verified setup used:

- Radxa ROCK 5B+
- BELABOX image `20250915-a84acea`
- Ubuntu 22.04.5 LTS
- kernel `5.10.160-belabox`
- WirePlumber `0.4.8-4`
- PipeWire 0.3.x
- Realtek RTL8852BE Bluetooth side on USB, USB ID `13d3:3572`
- vendor driver `rtk_btusb`
- Bluetooth speaker originally documented as WPS200
- BELABOX user `user`, UID 1000

Usernames, UIDs, MAC addresses and paths must be adjusted on other installations.

## 1. Install audio/Bluetooth packages

```bash
sudo apt update
sudo apt install -y \
  pipewire \
  pipewire-bin \
  pipewire-audio-client-libraries \
  wireplumber \
  libspa-0.2-bluetooth \
  bluez
```

Enable services:

```bash
sudo systemctl enable --now bluetooth
systemctl --user enable --now pipewire.service wireplumber.service
sudo loginctl enable-linger user
```

Verify linger:

```bash
loginctl show-user user -p Linger
```

Expected:

```text
Linger=yes
```

## 2. Realtek RTL8852BE - use `rtk_btusb`

On the tested BELABOX image, generic `btusb` attached to the controller but Bluetooth scanning did not find devices. The BELABOX kernel also includes Radxa/Realtek `rtk_btusb`, which worked correctly.

### Firmware names

`rtk_btusb` expects firmware files in the firmware root without `.bin`:

```text
rtl8852bu_fw
rtl8852bu_config
```

The files existed under `/lib/firmware/rtl_bt/`, so permanent symlinks were created:

```bash
sudo ln -sf /lib/firmware/rtl_bt/rtl8852bu_fw.bin \
  /lib/firmware/rtl8852bu_fw

sudo ln -sf /lib/firmware/rtl_bt/rtl8852bu_config.bin \
  /lib/firmware/rtl8852bu_config
```

`rtl8852bu_config.bin` may itself already be a symlink to `rtl8761bu_config.bin`; that was normal on the tested image.

### Prevent generic `btusb` from taking the controller

```bash
sudo tee /etc/modprobe.d/belabox-bluetooth.conf >/dev/null <<'EOF'
blacklist btusb
EOF
```

Reboot:

```bash
sudo reboot
```

Verify driver binding:

```bash
readlink -f /sys/class/bluetooth/hci0/device/driver
lsmod | grep -E 'rtk_btusb|btusb'
```

Expected driver path on the tested setup:

```text
/sys/bus/usb/drivers/rtk_btusb
```

## 3. Pair and trust the speaker

```bash
bluetoothctl
```

Then:

```text
power on
scan on
pair <SPEAKER_MAC>
trust <SPEAKER_MAC>
connect <SPEAKER_MAC>
scan off
quit
```

Verify:

```bash
bluetoothctl info <SPEAKER_MAC> | grep -E 'Paired|Trusted|Connected'
```

Expected while the speaker is on:

```text
Paired: yes
Trusted: yes
Connected: yes
```

## 4. WirePlumber 0.4.8 headless BlueZ behavior

### Symptom

Before the headless fix, Bluetooth, `user@1000`, PipeWire, WirePlumber and the reconnect watchdog all started at boot, but the speaker would not connect until someone logged in over SSH.

Typical errors:

```text
Failed to connect: org.bluez.Error.Failed br-connection-profile-unavailable
```

and:

```text
a2dp-sink profile connect failed ... Protocol not available
```

At SSH login, WirePlumber registered A2DP endpoints and the next reconnect attempt succeeded.

### Disable logind requirement for the BlueZ monitor

Create:

```bash
mkdir -p ~/.config/wireplumber/bluetooth.lua.d

cat > ~/.config/wireplumber/bluetooth.lua.d/80-disable-logind.lua <<'EOF'
bluez_monitor.properties["with-logind"] = false
EOF
```

Copy the distro BlueZ monitor locally instead of editing `/usr/share`:

```bash
cp /usr/share/wireplumber/bluetooth.lua.d/30-bluez-monitor.lua \
   ~/.config/wireplumber/bluetooth.lua.d/30-bluez-monitor.lua
```

Patch the `load_optional_module("logind")` line:

```bash
python3 - <<'PY'
from pathlib import Path

p = Path.home() / ".config/wireplumber/bluetooth.lua.d/30-bluez-monitor.lua"
s = p.read_text()

old = '  load_optional_module("logind")'
new = '''  if bluez_monitor.properties["with-logind"] then
    load_optional_module("logind")
  end'''

if old not in s:
    raise SystemExit("Expected logind line not found; no change made")

p.write_text(s.replace(old, new, 1))
PY
```

Restart WirePlumber:

```bash
systemctl --user restart wireplumber.service
```

## 5. Bluetooth reconnect watchdog

The tested setup uses a small system service that:

- starts with BlueZ and `user@1000.service`
- waits for PipeWire/WirePlumber startup
- restarts WirePlumber through the user's systemd manager when needed
- reconnects the trusted speaker if disconnected
- continues monitoring during use

Example watchdog script:

```bash
sudo tee /usr/local/sbin/cometen-wps200-watchdog >/dev/null <<'EOF'
#!/bin/bash

MAC="<SPEAKER_MAC>"

sleep 3
systemctl --user --machine=user@.host restart wireplumber.service || true
sleep 3

while true; do
    if ! /usr/bin/bluetoothctl info "$MAC" 2>/dev/null | /usr/bin/grep -q "Connected: yes"; then
        /usr/bin/bluetoothctl power on >/dev/null 2>&1 || true
        /usr/bin/timeout 8 /usr/bin/bluetoothctl connect "$MAC" || true
    fi
    sleep 5
done
EOF

sudo chmod +x /usr/local/sbin/cometen-wps200-watchdog
```

Systemd unit:

```bash
sudo tee /etc/systemd/system/cometen-wps200.service >/dev/null <<'EOF'
[Unit]
Description=Cometen WPS200 Bluetooth Auto Connect
Wants=bluetooth.service user@1000.service
After=bluetooth.service user@1000.service

[Service]
Type=simple
ExecStart=/usr/local/sbin/cometen-wps200-watchdog
Restart=always
RestartSec=3

[Install]
WantedBy=multi-user.target
EOF
```

Enable:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now cometen-wps200.service
```

Check:

```bash
sudo systemctl status cometen-wps200.service --no-pager
```

## 6. Install CometenIRLAlerts user services

```bash
cd ~/CometenIRLAlerts/receiver
bash ./install-user-service.sh
sudo loginctl enable-linger user
```

Check:

```bash
systemctl --user is-enabled pipewire.service
systemctl --user is-enabled wireplumber.service
systemctl --user is-enabled cometen-irl-alerts.service
```

All should be enabled.

## 7. Silent audio keepalive

On the tested BELABOX version, piping raw silence through `pw-cat` was not reliable in every configuration. The verified headless workaround was a real silent PCM WAV file looped through `pw-play`.

Create `receiver/sounds/keepalive.wav`:

```bash
cd ~/CometenIRLAlerts/receiver

python3 - <<'PY'
import wave
from pathlib import Path

path = Path("sounds/keepalive.wav")
rate = 48000
seconds = 30
channels = 2
sample_width = 2

with wave.open(str(path), "wb") as w:
    w.setnchannels(channels)
    w.setsampwidth(sample_width)
    w.setframerate(rate)
    w.writeframes(b"\x00" * rate * seconds * channels * sample_width)

print(path)
PY
```

Example local receiver config:

```json
{
  "audio_keepalive_enabled": true,
  "audio_keepalive_command": "bash -c 'while true; do pw-play /home/user/CometenIRLAlerts/receiver/sounds/keepalive.wav || sleep 1; done'",
  "audio_keepalive_input": "/dev/zero",
  "audio_keepalive_restart_seconds": 5
}
```

Do not commit the real `receiver/config.json`.

Restart receiver after changing keepalive configuration:

```bash
systemctl --user restart cometen-irl-alerts.service
```

## 8. PipeWire sink and volume

Check:

```bash
wpctl status
```

Sink IDs are dynamic and must not be hardcoded permanently.

Set volume using the current sink ID if needed:

```bash
wpctl set-volume <SINK_ID> 0.45
```

The exact preferred level is installation-specific.

## 9. Verify a true headless boot

The meaningful test is without SSH/login:

1. reboot or fully power-cycle BELABOX
2. do not log in locally or over SSH
3. wait roughly 30-60 seconds
4. verify the speaker reconnects automatically
5. send a test alert from Streamer.bot/CometenWebAdmin
6. verify the alert plays locally

If the test fails, log in afterward and inspect boot logs:

```bash
sudo journalctl -b -u cometen-wps200.service --no-pager
sudo journalctl -b -u user@1000.service --no-pager
sudo journalctl -b _UID=1000 --no-pager | \
  grep -iE 'wireplumber|bluez|logind|seat|bluetooth'
```

A common pre-fix error was:

```text
br-connection-profile-unavailable
```

## 10. Expected final boot flow

```text
Power on BELABOX
-> Bluetooth controller binds to rtk_btusb
-> user@1000 starts without login
-> PipeWire/WirePlumber start
-> reconnect watchdog connects speaker
-> silent keepalive keeps audio path active
-> CometenIRLAlerts receiver polls relay
-> Streamer.bot/WebAdmin alerts play on the speaker
```

No SSH login should be required for normal IRL use.

## Related documentation

- [Installation](INSTALLATION.md)
- [Receiver setup](receiver-setup.md)
- [Browser Audio](BROWSER_AUDIO.md)
