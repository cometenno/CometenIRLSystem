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

Testet i komplett kjede per 8. august 2026:

- HTTPS-sending fra Streamer.bot
- lagring og uthenting gjennom PHP/MySQL-relay
- Python-receiver med polling og kvittering
- lokal WAV-avspilling gjennom PipeWire og Bluetooth
- Follow-event fra CometenWebAdmin til receiver
- manuell IRL-test
- stille PipeWire-keepalive
- valg av aktiv PipeWire sink med `wpctl set-default`
- Radxa ROCK 5B+ / BELABOX med Realtek `rtk_btusb`
- automatisk WPS200-tilkobling etter boot
- headless oppstart av PipeWire, WirePlumber og receiver uten SSH-login
- alert fra CometenWebAdmin etter kald boot uten manuell innlogging

## BELABOX ROCK 5B+ - egen guide

Det verifiserte BELABOX-oppsettet er dokumentert separat:

[`docs/BELABOX_ROCK5B_HEADLESS_NO.md`](docs/BELABOX_ROCK5B_HEADLESS_NO.md)

Guiden dokumenterer hele løsningen vi måtte bruke på BELABOX-imagen, inkludert:

- Realtek RTL8852BE / USB ID `13d3:3572`
- `rtk_btusb` i stedet for generisk `btusb`
- firmware-symlinker for `rtl8852bu_fw` og `rtl8852bu_config`
- PipeWire og WirePlumber
- WirePlumber 0.4.8 headless/logind-fiks
- WPS200 system-watchdog
- `user@1000.service` og `loginctl enable-linger`
- stille `pw-play`-keepalive
- kaldstarttest uten SSH-login

## CometenWebAdmin-integrasjon

Den kanoniske alert-overlayfila ligger i det separate private prosjektet:

```text
la1ona/cometenWebAdmin
└── alerts/alerts.html
```

Dette prosjektet inneholder IRL-integrasjonen og dokumentasjonen:

```text
integration/cometenwebadmin/irl-forward.js
integration/cometenwebadmin/README_NO.md
integration/cometenwebadmin/VERSION
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
