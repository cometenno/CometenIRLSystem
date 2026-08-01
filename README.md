# Cometen IRL Alerts

Cometen IRL Alerts er en lettvekts returkanal for varsler under IRL-streaming.

Systemet sender alerts fra Streamer.bot på streaming-PC-en til en HTTPS-relay på webhotellet. En Linux-enhet ved IRL-riggen henter eventene og spiller lokale WAV-filer gjennom en PipeWire/Bluetooth-lydutgang.

## Arkitektur

```text
Twitch / YouTube / CometenWebAdmin
        |
        v
Streamer.bot på streaming-PC
        |
        | HTTPS POST
        v
PHP/MySQL-relay på webhotell
        |
        | HTTPS polling
        v
Raspberry Pi / ROCK 5B+ ved BELABOX
        |
        v
PipeWire + Bluetooth-høyttaler eller headset
```

BELABOX håndterer video, lydopptak, bonding og SRT/SRTLA. Alertsystemet kjører separat.

## Bekreftet funksjon

Testet i komplett kjede per 1. august 2026:

- HTTPS-sending fra Streamer.bot
- lagring og uthenting gjennom PHP/MySQL-relay
- Python-receiver med polling og kvittering
- lokal WAV-avspilling gjennom PipeWire og Bluetooth
- Follow-event fra CometenWebAdmin til receiver
- manuell IRL-test
- stille PipeWire-keepalive
- valg av aktiv PipeWire sink med `wpctl set-default`

## CometenWebAdmin-integrasjon

Prosjektet inneholder en komplett integrasjonskopi:

```text
integration/cometenwebadmin/alerts.html
integration/cometenwebadmin/irl-forward.js
integration/cometenwebadmin/README_NO.md
```

Versjon 19.9 retter:

- stale browser-state ved å bruke `universal_alert_webadmin_v2_settings`
- settings-innlasting via `CWA - Alerts Status`
- mottak av `ALERTS_SETTINGS`
- IRL master- og per-alert-innstillinger

OFF/ON-kontrollene trenger siste praktiske test etter at v19.9-filene er lagt inn.

## Repo-oppsett

```text
streamerbot/                  Streamer.bot C#-sender
relay/                        PHP/MySQL-relay
receiver/                     Python-receiver, lydfiler og user-systemd
integration/cometenwebadmin/  Sentral videresending fra eksisterende alerts
docs/                         Installasjon og dokumentasjon
```

## Rask receiver-test

```bash
cd ~/CometenIRLAlerts
git pull
cd receiver
python3 receiver.py config.json
```

## Bytte PipeWire-lydutgang

```bash
wpctl status
wpctl set-default SINK_ID
```

Start receiveren på nytt dersom den allerede kjørte da lydutgangen ble endret.

## Autostart

```bash
cd ~/CometenIRLAlerts/receiver
bash install-user-service.sh
sudo loginctl enable-linger "$USER"
```

Status og logg:

```bash
systemctl --user status cometen-irl-alerts.service
journalctl --user -u cometen-irl-alerts.service -f
```

## Sikkerhet

Virkelige token, databasepassord og lokale konfigurasjonsfiler skal aldri legges i GitHub.

Hold disse private:

```text
relay/config.php
receiver/config.json
```
