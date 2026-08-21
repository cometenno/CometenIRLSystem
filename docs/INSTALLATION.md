# Installation

This is the canonical setup and update guide for Cometen IRL Alerts.

Last updated: 21 August 2026.

## System layout

```text
Streamer.bot / CometenWebAdmin on streaming PC
        |
        | HTTPS
        v
PHP/MySQL relay on web host
        |
        +------------------------------+
        |                              |
        | alerts / control             | heartbeat/status
        v                              v
Python receiver on ROCK 5B+      receiver status
        |
        v
PipeWire / Bluetooth / local WAV files

BELABOX Cloud ingest telemetry
        |
        v
IRLAlertsController in Streamer.bot
        |
        +--> BELABOX SRT
        +--> IRL - SIGNAL MISTET
```

Browser Audio is an additional BELABOX-side module that launches headless Chromium instances and routes Browser Source audio to the same PipeWire/Bluetooth sink.

## 1. Requirements

### Streaming PC

- Windows
- Streamer.bot
- OBS Studio
- OBS WebSocket enabled and connected to Streamer.bot
- internet access
- optional CometenWebAdmin integration

### Web host

- HTTPS
- PHP 8+
- MySQL or MariaDB with InnoDB
- file upload/FTP or equivalent
- database administration tool such as phpMyAdmin

### ROCK 5B+/BELABOX

- Python 3
- Git
- systemd user services
- PipeWire
- WirePlumber
- `pw-play`
- `pw-cat`
- Bluetooth stack if using a Bluetooth speaker

Useful checks:

```bash
python3 --version
git --version
pw-play --version
pw-cat --version
wpctl --version
bluetoothctl --version
```

On regular Debian/Ubuntu systems, common packages are:

```bash
sudo apt update
sudo apt install -y git python3 pipewire-bin wireplumber bluez
```

BELABOX images may differ from stock Ubuntu/Debian. Avoid unnecessary full-system upgrades on a working BELABOX installation.

## 2. Clone the repository

```bash
cd ~
git clone https://github.com/la1ona/CometenIRLAlerts.git
cd CometenIRLAlerts
```

Update later with:

```bash
cd ~/CometenIRLAlerts
git pull
```

Local secrets must remain in gitignored files:

```text
receiver/config.json
relay/config.php
```

## 3. Create the relay database

Create a MySQL/MariaDB database and import:

```text
relay/database.sql
```

The schema creates the event queue, control-result and receiver-status tables used by the system.

With phpMyAdmin:

1. select the target database
2. choose Import
3. select `relay/database.sql`
4. run the import

## 4. Generate sender and receiver tokens

Generate two different long random tokens:

```bash
python3 -c "import secrets; print(secrets.token_urlsafe(48))"
python3 -c "import secrets; print(secrets.token_urlsafe(48))"
```

Use one as `sender_token` and the other as `receiver_token`.

Never put these tokens in GitHub, screenshots or public chat.

## 5. Install the relay on the web host

Upload the PHP files from `relay/` to a dedicated HTTPS directory, for example:

```text
https://example.com/CometenIRLAlerts_Relay
```

The relay directory should contain at least:

```text
acknowledge.php
bootstrap.php
control_result.php
health.php
heartbeat.php
poll.php
push.php
receiver_status.php
config.php
```

Copy `config.example.php` to `config.php` on the web host and set the database credentials and tokens.

Example structure:

```php
<?php

declare(strict_types=1);

return [
    'database' => [
        'dsn' => 'mysql:host=localhost;dbname=DATABASE;charset=utf8mb4',
        'username' => 'DATABASE_USER',
        'password' => 'DATABASE_PASSWORD',
    ],

    'sender_token' => 'LONG_SENDER_TOKEN',
    'receiver_token' => 'LONG_RECEIVER_TOKEN',

    'event_ttl_seconds' => 90,
    'lease_seconds' => 30,
    'receiver_offline_seconds' => 90,
];
```

Test:

```text
https://example.com/CometenIRLAlerts_Relay/health.php
```

Expected response: JSON with `ok=true`.

### Heartbeat rate limit

Do not send heartbeat every second. A 1-second interval caused HTTP 429 responses during testing.

Recommended production values:

```text
heartbeat_interval_seconds = 30
receiver_offline_seconds = 90
```

The heartbeat client also backs off when the server returns 429 and uses `Retry-After` when provided.

## 6. Configure the BELABOX receiver

```bash
cd ~/CometenIRLAlerts/receiver
cp config.example.json config.json
nano config.json
```

Minimum relay/receiver fields:

```json
{
  "relay_base_url": "https://example.com/CometenIRLAlerts_Relay",
  "receiver_token": "RECEIVER_TOKEN",
  "poll_interval_seconds": 0.75,
  "request_timeout_seconds": 10,
  "batch_size": 5,
  "heartbeat_receiver_id": "belabox",
  "heartbeat_interval_seconds": 30,
  "heartbeat_timeout_seconds": 5,
  "sounds_directory": "sounds",
  "default_sound": "test.wav"
}
```

Keep any additional audio, remote-control, Browser Audio and LED fields required by the current `config.example.json`.

Validate the JSON:

```bash
python3 -m json.tool config.json >/dev/null && echo "config.json OK"
```

## 7. Install local sounds and test PipeWire

Put WAV files in:

```text
receiver/sounds/
```

Recommended format:

- WAV
- PCM
- 16-bit
- 44.1 or 48 kHz

Test:

```bash
cd ~/CometenIRLAlerts/receiver
pw-play sounds/test.wav
```

The keepalive path uses `pw-cat`. Do not add `--raw` unless the installed PipeWire version explicitly supports it.

## 8. Bluetooth speaker

Pair and trust the speaker with `bluetoothctl`:

```bash
bluetoothctl
```

Typical commands:

```text
power on
agent on
default-agent
scan on
pair XX:XX:XX:XX:XX:XX
trust XX:XX:XX:XX:XX:XX
connect XX:XX:XX:XX:XX:XX
quit
```

Check PipeWire:

```bash
wpctl status
```

Set the desired default sink if required:

```bash
wpctl set-default SINK_ID
```

See [BELABOX Headless Setup](BELABOX_HEADLESS.md) for the tested ROCK 5B+/PipeWire/Bluetooth workflow.

## 9. Install receiver and heartbeat user services

Use user-systemd, not the older system-wide service approach:

```bash
cd ~/CometenIRLAlerts/receiver
bash install-user-service.sh
```

This installs/starts:

```text
cometen-irl-alerts.service
cometen-irl-heartbeat.service
```

Enable lingering once so user services may survive logout/reboot:

```bash
sudo loginctl enable-linger "$USER"
```

Check receiver:

```bash
systemctl --user status cometen-irl-alerts.service
journalctl --user -u cometen-irl-alerts.service -n 50 --no-pager
```

Check heartbeat:

```bash
systemctl --user status cometen-irl-heartbeat.service
journalctl --user -u cometen-irl-heartbeat.service -n 50 --no-pager
```

## 10. Install Browser Audio runtime

Browser Audio on Jammy ARM64 uses a local Playwright Chromium runtime.

Install dependencies:

```bash
sudo apt install -y xvfb xauth python3-venv
```

Install the local browser runtime:

```bash
cd ~/CometenIRLAlerts/receiver
bash install-browser-runtime.sh
```

The installer deliberately uses a runtime-local temporary directory because `/tmp` can be a small tmpfs on BELABOX even when the main filesystem has plenty of free space.

Do not install `chromium-bsu`; it is not the Chromium browser.

Configure the first private Browser Source locally:

```bash
python3 configure-browser-audio.py
```

Then install/start the supervisor:

```bash
bash install-browser-audio.sh
```

Check:

```bash
systemctl --user status cometen-irl-browser-audio.service
journalctl --user -u cometen-irl-browser-audio.service -n 100 --no-pager
```

Never paste private Browser Source URLs into GitHub or public chat.

See [Browser Audio](BROWSER_AUDIO.md).

## 11. Streamer.bot globals

Create persistent globals:

```text
CometenIRL_RelayUrl
CometenIRL_SenderToken
```

`CometenIRL_RelayUrl` is the base relay directory without `/push.php`.

Additional globals are used by the admin/watchdog modules. See [Streamer.bot Setup](streamerbot-setup.md).

## 12. Streamer.bot actions

Core actions/files:

```text
Cometen IRL Notifications - Send
  -> streamerbot/CometenIRL_Send.cs

CometenIRL_RemoteControl
  -> streamerbot/CometenIRL_RemoteControl.cs

CometenIRL_BrowserAudioControl
  -> streamerbot/CometenIRL_BrowserAudioControl.cs

CometenIRL_AdminControl
  -> streamerbot/CometenIRL_AdminControl.cs

CometenIRL_EndAutoStop
  -> streamerbot/CometenIRL_EndAutoStop.cs

IRLAlertsController
  -> streamerbot/IRLAlertsController.cs
```

Compile each C# action in Streamer.bot and configure triggers/permissions as documented in [Streamer.bot Setup](streamerbot-setup.md).

## 13. CometenWebAdmin integration

Integration file:

```text
integration/cometenwebadmin/irl-forward.js
```

If this forwards an alert into IRL, do not also send the same alert through another action, or the receiver may get duplicates.

See [CometenWebAdmin integration](../integration/cometenwebadmin/README.md).

## 14. Watchdog setup

The watchdog reads BELABOX ingest telemetry and performs OBS failover/recovery.

Typical globals:

```text
CometenIRL_BelaboxStreamId
CometenIRL_BelaboxStatsBaseUrl
CometenIRL_FallbackScene = IRL - SIGNAL MISTET
CometenIRL_DefaultReturnScene = BELABOX SRT
CometenIRL_WatchdogLiveOnly = true
CometenIRL_WatchdogArmed = true/false
```

Do not commit the BELABOX stream ID.

See [Watchdog and Heartbeat](WATCHDOG_HEARTBEAT.md).

## 15. Status LEDs

Install/update the GPIO/status-LED component with the repository script documented in [Status LEDs](STATUS_LEDS.md).

## 16. Updating an existing BELABOX installation

Normal receiver update:

```bash
cd ~/CometenIRLAlerts
git pull
cd receiver
bash install-user-service.sh
systemctl --user restart cometen-irl-alerts.service
systemctl --user restart cometen-irl-heartbeat.service
```

If Browser Audio code changed:

```bash
systemctl --user restart cometen-irl-browser-audio.service
```

If the relay PHP files changed in GitHub, upload the changed relay files to the web host separately. `git pull` on BELABOX does not update the web host.

## 17. First functional tests

Recommended order:

1. `health.php` returns `ok=true`.
2. receiver service is active.
3. heartbeat service is active.
4. `pw-play sounds/test.wav` works locally.
5. a Streamer.bot test event reaches BELABOX.
6. `!irlstatus` returns a confirmed BELABOX result.
7. volume/mute/test controls work.
8. Sound Alerts Browser Audio plays on BELABOX.
9. the same Sound Alerts event plays in OBS and on BELABOX if dual playback is intended.
10. watchdog fallback/recovery is tested with OBS offline test mode first, then live-only mode.

## 18. Troubleshooting commands

Receiver logs:

```bash
journalctl --user -u cometen-irl-alerts.service -n 100 --no-pager
```

Browser Audio logs:

```bash
journalctl --user -u cometen-irl-browser-audio.service -n 100 --no-pager
```

Heartbeat logs:

```bash
journalctl --user -u cometen-irl-heartbeat.service -n 100 --no-pager
```

PipeWire:

```bash
wpctl status
```

USB/video kernel events:

```bash
sudo journalctl -kf -n 0 | grep -Ei 'uvc|usb|video|v4l2|xhci|disconnect|reset|error'
```

## Security checklist

Never commit or publish:

```text
relay/config.php
receiver/config.json
```

Never publish:

- sender token
- receiver token
- database password
- BELABOX stream ID
- Browser Source URL/token
