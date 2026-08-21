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

## Video/USB hardware

```bash
sudo journalctl -kf -n 0 | grep -Ei 'uvc|usb|video|v4l2|xhci|disconnect|reset|error'
```

## Root video probe

```bash
sudo systemctl status cometen-irl-video-probe.service --no-pager
cat /run/cometen-irl-video-status.json
```

## Persistent journal

```bash
journalctl --list-boots
journalctl -b -1 -n 100 --no-pager
```

## Common separation rules

- Heartbeat online does not prove the SRT video path is healthy.
- A camera USB failure is not automatically a relay/network failure.
- Browser Audio service active does not mean the Browser Audio master state is ON.
- PipeWire node IDs can change after reconnect/reboot.
- A second automatic OBS scene switcher can cause false-looking watchdog failures.

For detailed guides, start at:

https://github.com/la1ona/CometenIRLAlerts/blob/main/docs/README.md
