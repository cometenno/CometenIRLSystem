# Installation

The full canonical installation guide is maintained here:

https://github.com/la1ona/CometenIRLAlerts/blob/main/docs/INSTALLATION.md

## High-level setup order

1. Create/import the MySQL/MariaDB relay database.
2. Generate separate sender and receiver tokens.
3. Upload the `relay/` PHP files to an HTTPS web-host directory.
4. Create private `relay/config.php` on the web host.
5. Clone `CometenIRLAlerts` on the ROCK 5B+/BELABOX.
6. Create private `receiver/config.json`.
7. Pair/configure the Bluetooth speaker and verify PipeWire output.
8. Install receiver + heartbeat user services.
9. Install Browser Audio runtime/supervisor if Browser Source audio is required.
10. Create Streamer.bot globals/actions/commands.
11. Configure OBS scenes and BELABOX ingest watchdog globals.
12. Test alert delivery, remote control, Browser Audio and failover/recovery in that order.

## BELABOX update

```bash
cd ~/CometenIRLAlerts
git pull
cd receiver
bash install-user-service.sh
systemctl --user restart cometen-irl-alerts.service
systemctl --user restart cometen-irl-heartbeat.service
```

If Browser Audio changed:

```bash
systemctl --user restart cometen-irl-browser-audio.service
```

## Web-host update

A BELABOX `git pull` does not update the relay on the web host. Changed PHP/SQL relay files must be uploaded separately.

Never overwrite production `relay/config.php` with an example file.

## Private files

Never commit:

```text
relay/config.php
receiver/config.json
```
