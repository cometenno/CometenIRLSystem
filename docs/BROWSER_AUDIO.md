# Browser Audio

Browser Audio lets the ROCK 5B+/BELABOX open private Browser Source URLs in headless Chromium and play the web audio directly through PipeWire to the IRL Bluetooth speaker.

The production-tested Sound Alerts path is:

```text
Sound Alerts -> OBS Browser Source on streaming PC -> stream audio
            -> BELABOX Browser Audio -> PipeWire -> Bluetooth speaker
```

The same Sound Alerts test event was confirmed to play in both places.

## Multi-source design

Browser Audio supports multiple named sources:

```text
soundalerts
blerp
other-source
```

Each source runs in its own Chromium process/profile. One source can therefore be stopped or restarted without taking down the others.

## Main files

```text
receiver/browser_audio.py
receiver/configure-browser-audio.py
receiver/install-browser-runtime.sh
receiver/install-browser-audio.sh
receiver/cometen-irl-browser-audio-user.service
streamerbot/CometenIRL_BrowserAudioControl.cs
```

Private URLs live only in:

```text
receiver/config.json
```

This file is gitignored.

## First-time runtime installation on Jammy ARM64

Install dependencies:

```bash
sudo apt install -y xvfb xauth python3-venv
```

Install the local Playwright Chromium runtime:

```bash
cd ~/CometenIRLAlerts/receiver
bash install-browser-runtime.sh
```

The runtime is installed under the user's local data directory, normally:

```text
~/.local/share/cometen-irl-browser-audio/
```

The installer forces temporary download/extraction files into a runtime-local temp directory because `/tmp` may be a small tmpfs on BELABOX even when `/` has hundreds of GB free.

Do not install `chromium-bsu`; it is not the Chromium web browser.

## Configure the first source locally

Default source name:

```text
soundalerts
```

Run:

```bash
python3 configure-browser-audio.py
```

Paste the private Browser Source URL directly into the SSH prompt.

Do not paste the URL into:

- GitHub
- Discord
- public Twitch chat
- screenshots

Configure another named source locally:

```bash
python3 configure-browser-audio.py --name blerp
```

List configured source names/status without exposing URLs:

```bash
python3 configure-browser-audio.py --list
```

## Install/start Browser Audio supervisor

```bash
bash install-browser-audio.sh
```

Check service:

```bash
systemctl --user status cometen-irl-browser-audio.service
```

Logs:

```bash
journalctl --user -u cometen-irl-browser-audio.service -f
```

## Configuration model

Example structure:

```json
"browser_audio_enabled": true,
"browser_audio_sources": [
  {
    "name": "soundalerts",
    "url": "PRIVATE_URL",
    "enabled": true,
    "generation": 0
  },
  {
    "name": "blerp",
    "url": "PRIVATE_URL",
    "enabled": true,
    "generation": 0
  }
]
```

The legacy `browser_audio_url` field may remain for backward compatibility with an older single-source setup.

Source names are restricted to lowercase/number/underscore/hyphen style names and the current admin path limits the number of sources.

## Twitch chat control

Streamer.bot action:

```text
CometenIRL_BrowserAudioControl
```

Code:

```text
streamerbot/CometenIRL_BrowserAudioControl.cs
```

Create one Twitch command:

```text
!irlaudio
```

Use **Starts With**.

Recommended permission: **Broadcaster + Moderators**.

### Master commands

```text
!irlaudio status
!irlaudio on
!irlaudio off
!irlaudio restart
```

### Add/remove sources

```text
!irlaudio add <name> <Browser Source URL>
!irlaudio remove <name>
!irlaudio delete <name>
!irlaudio del <name>
```

Example:

```text
!irlaudio add blerp https://PRIVATE_BROWSER_SOURCE_URL
```

The original Twitch command message is deleted after Streamer.bot captures it. The confirmation must not repeat the URL.

Expected confirmation:

```text
IRL Audio: blerp added
```

The current receiver may return localized Norwegian text depending on the active implementation/language path; the important rule is that the URL is never echoed.

### Per-source commands

```text
!irlaudio <name> status
!irlaudio <name> on
!irlaudio <name> off
!irlaudio <name> restart
```

Examples:

```text
!irlaudio soundalerts status
!irlaudio soundalerts off
!irlaudio soundalerts on
!irlaudio soundalerts restart

!irlaudio blerp off
!irlaudio blerp on
```

## Master state versus source state

`!irlaudio off` turns off the Browser Audio master state but deliberately leaves the systemd supervisor active so chat can turn it back on.

Therefore a status such as:

```text
IRL Audio: OFF | service active | soundalerts ON
```

means:

- Browser Audio master is OFF
- the supervisor service is still running
- `soundalerts` is configured as an enabled source and will start when master is ON again

## Local source management without Twitch

Add/update:

```bash
python3 configure-browser-audio.py --name blerp
```

Disable one source:

```bash
python3 configure-browser-audio.py --disable-source blerp
```

Enable one source:

```bash
python3 configure-browser-audio.py --enable blerp
```

Restart one source:

```bash
python3 configure-browser-audio.py --restart-source blerp
```

Remove one source:

```bash
python3 configure-browser-audio.py --remove blerp
```

Disable master:

```bash
python3 configure-browser-audio.py --disable
```

## Audio routing

The supervisor resolves the configured PipeWire Audio/Sink match and sets the target sink before starting Chromium.

PipeWire node IDs can change after reconnect/reboot, so matching by configured speaker/device text is preferred to permanently hardcoding one node number.

Check:

```bash
wpctl status
```

## Security behavior

Private Browser Source URLs can contain access tokens.

Rules:

- store them only in local `receiver/config.json`
- never commit them
- never log the full URL
- never echo them to Twitch confirmations
- delete URL-bearing chat commands after Streamer.bot captures the event
- rotate/regenerate a Browser Source URL if it was exposed publicly

The URL travels through the existing HTTPS relay only as a short-lived control event when added from chat.

## Verified status - 21 August 2026

Verified on the actual ROCK 5B+/BELABOX setup:

- Playwright Chromium runtime on Jammy ARM64
- Browser Audio supervisor active under user-systemd
- Sound Alerts playback through BELABOX to the Bluetooth speaker
- simultaneous Sound Alerts playback in OBS and on BELABOX
- `!irlaudio status`
- master `!irlaudio off` / `!irlaudio on` command path and status transition
- adding a second named source through chat
- automatic deletion of the URL-bearing `!irlaudio add` message
- status reporting with multiple named sources
- per-source `off` / `on` command path

Still worth independently verifying before calling another provider production-tested:

- actual audio playback from a genuinely separate second provider/source URL
- provider-specific behavior when multiple clients connect to the same provider account/widget

## Troubleshooting

Service status:

```bash
systemctl --user status cometen-irl-browser-audio.service
```

Logs:

```bash
journalctl --user -u cometen-irl-browser-audio.service -n 100 --no-pager
```

Audio graph:

```bash
wpctl status
```

If Chromium fails to launch, capture the exact missing shared-library or process error before installing broad dependency sets.

## Related documentation

- [Installation](INSTALLATION.md)
- [Commands](COMMANDS.md)
- [Remote Control](REMOTE_CONTROL.md)
- [Receiver setup](receiver-setup.md)
