# Troubleshooting

Use the subsystem boundary to narrow failures before changing configuration.

## Receiver

```bash
systemctl --user status cometen-irl-alerts.service
journalctl --user -u cometen-irl-alerts.service -n 100 --no-pager
```

## Heartbeat

```bash
systemctl --user status cometen-irl-heartbeat.service
journalctl --user -u cometen-irl-heartbeat.service -n 100 --no-pager
```

## Browser Audio

```bash
systemctl --user status cometen-irl-browser-audio.service
journalctl --user -u cometen-irl-browser-audio.service -n 100 --no-pager
```

## PipeWire/Bluetooth

```bash
wpctl status
pw-cli ls Node
bluetoothctl
```

The blue status LED refers to the configured Bluetooth audio device. It is not tied to one specific speaker model.

## Video input

The yellow LED represents the currently available video source. Supported source types include local device inputs and RTMP.

For USB/V4L2 hardware issues:

```bash
sudo journalctl -kf -n 0 | grep -Ei 'uvc|usb|video|v4l2|xhci|disconnect|reset|error'
```

For RTMP input, also check whether an external publisher is connected to TCP port `1935` and inspect the root video-probe status.

```bash
ss -tn | grep ':1935'
cat /run/cometen-irl-video-status.json
```

An RTMP publisher disappearing can make the yellow LED report video-input loss even when no USB camera error exists.

## Root video probe

```bash
sudo systemctl status cometen-irl-video-probe.service --no-pager
cat /run/cometen-irl-video-status.json
```

The probe can report local device input or RTMP/network input separately.

## Online / internet state

The green LED represents BELABOX online/internet connectivity. Solid means internet connectivity is established and the configured relay is reachable. A fast blink means connectivity was lost after previously being online.

## Persistent journal

```bash
journalctl --list-boots
journalctl -b -1 -n 100 --no-pager
```

## Common separation rules

- Heartbeat online does not prove the SRT video path is healthy.
- A camera USB failure is not automatically a relay/network failure.
- RTMP source loss is not automatically a USB/video-device failure.
- Browser Audio service active does not mean the Browser Audio master state is ON.
- PipeWire node IDs can change after reconnect/reboot.
- A second automatic OBS scene switcher can cause false-looking watchdog failures.

See [[Status-LEDs]] for the physical LED meanings.

For detailed guides, start at:

https://github.com/la1ona/CometenIRLSystem/blob/main/docs/README.md
