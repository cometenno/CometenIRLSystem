# Receiver setup

The receiver runs on the ROCK 5B+/BELABOX and is responsible for polling the relay, playing local alert audio, executing supported remote-control actions, reporting status and returning control results.

## Files

Primary files:

```text
receiver/receiver.py
receiver/run_receiver.py
receiver/config.example.json
receiver/install-user-service.sh
```

Private configuration:

```text
receiver/config.json
```

`config.json` is gitignored and must never be committed.

## Repository path

Fresh installations use:

```text
~/CometenIRLSystem
```

Existing installations created before the repository rename may still use:

```text
~/CometenIRLAlerts
```

Keep the existing path unless you deliberately migrate and reinstall services that contain absolute paths.

## Create local configuration

Fresh installation:

```bash
cd ~/CometenIRLSystem/receiver
cp config.example.json config.json
nano config.json
```

Use `~/CometenIRLAlerts/receiver` instead on an existing pre-rename checkout.

At minimum configure:

- relay base URL
- receiver token
- sounds directory/default sound
- heartbeat identity/interval
- PipeWire sink matching
- any Browser Audio fields in use

Validate:

```bash
python3 -m json.tool config.json >/dev/null && echo "config.json OK"
```

## Local sound files

Store alert files under:

```text
receiver/sounds/
```

Recommended format:

- WAV
- PCM
- 16-bit
- 44.1 or 48 kHz

Test locally:

```bash
pw-play sounds/test.wav
```

## PipeWire and Bluetooth

Check current audio graph:

```bash
wpctl status
```

The receiver resolves a configured Audio/Sink match dynamically rather than depending permanently on a single PipeWire node number.

This matters because node IDs can change after reconnect/reboot.

See [BELABOX Headless Setup](BELABOX_HEADLESS.md) for Bluetooth pairing and headless audio routing.

## Audio keepalive

The receiver can keep the Bluetooth/PipeWire audio path alive using `pw-cat`.

The tested pattern is based on:

```text
pw-cat --playback --rate=48000 --channels=2 --format=s16 -
```

Do not add unsupported options such as `--raw` without testing against the PipeWire version installed on BELABOX.

## Install user services

Use the project installer from the active checkout:

```bash
cd ~/CometenIRLSystem/receiver
bash install-user-service.sh
```

Existing pre-rename installations may use `~/CometenIRLAlerts/receiver` instead.

This installs/updates the compatibility service names:

```text
cometen-irl-alerts.service
cometen-irl-heartbeat.service
```

These service names are intentionally retained after the project rename.

Enable lingering once:

```bash
sudo loginctl enable-linger "$USER"
```

## Service status

Receiver:

```bash
systemctl --user status cometen-irl-alerts.service
journalctl --user -u cometen-irl-alerts.service -n 100 --no-pager
```

Heartbeat:

```bash
systemctl --user status cometen-irl-heartbeat.service
journalctl --user -u cometen-irl-heartbeat.service -n 100 --no-pager
```

## Supported receiver responsibilities

The receiver handles:

- alert event playback
- volume set/up/down
- mute/unmute
- expanded IRL status
- test alert
- Browser Audio configuration/control actions
- event acknowledgements
- control result publishing

It does **not** act as the automatic OBS scene switcher. The BELABOX ingest watchdog on the streaming PC owns automatic signal-loss/recovery scene changes.

## Expanded status

`run_receiver.py` installs the expanded status wrapper used by `!irlstatus`.

Typical output includes:

```text
IRL: SYS OK | 51C Fan0/4 | soundcore Select 4 Go OK n33 | VIDEO ... | ENC ... | WiFi ... | Up ...
```

The exact node number is dynamic and is only diagnostic.

## Browser Audio

Browser Audio is a separate supervisor service in the same project:

```text
cometen-irl-browser-audio.service
```

It launches headless Chromium processes for configured Browser Sources and routes audio through the selected PipeWire sink.

See [Browser Audio](BROWSER_AUDIO.md).

## Update workflow

Fresh post-rename checkout:

```bash
cd ~/CometenIRLSystem
git pull
cd receiver
bash install-user-service.sh
systemctl --user restart cometen-irl-alerts.service
systemctl --user restart cometen-irl-heartbeat.service
```

Existing pre-rename checkout:

```bash
cd ~/CometenIRLAlerts
git pull
cd receiver
bash install-user-service.sh
systemctl --user restart cometen-irl-alerts.service
systemctl --user restart cometen-irl-heartbeat.service
```

Restart Browser Audio separately if its code changed:

```bash
systemctl --user restart cometen-irl-browser-audio.service
```

## Troubleshooting

Useful commands:

```bash
wpctl status
pw-cli ls Node
systemctl --user status cometen-irl-alerts.service
journalctl --user -u cometen-irl-alerts.service -n 100 --no-pager
```

For video/USB hardware issues:

```bash
sudo journalctl -kf -n 0 | grep -Ei 'uvc|usb|video|v4l2|xhci|disconnect|reset|error'
```

Keep audio/receiver failures separate from camera/USB failures while diagnosing the system.

## Related documentation

- [Installation](INSTALLATION.md)
- [Remote Control](REMOTE_CONTROL.md)
- [Browser Audio](BROWSER_AUDIO.md)
- [Status LEDs](STATUS_LEDS.md)
