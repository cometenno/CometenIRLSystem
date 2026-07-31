# Cometen IRL Alerts

Cometen IRL Alerts er en lettvekts returkanal for varsler under IRL-streaming.

Systemet sender alerts fra **Streamer.bot** på streaming-PC-en til en HTTPS-relay på webhotellet. En Linux-enhet ved IRL-riggen henter eventene og spiller lokale WAV-filer gjennom en Bluetooth-høyttaler.

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
PipeWire + Bluetooth-høyttaler
```

BELABOX fortsetter å håndtere video, lydopptak, bonding og SRT/SRTLA. Alertsystemet kjører separat.

## Status

Følgende er implementert og testet:

- HTTPS sender fra Streamer.bot
- PHP/MySQL-relay med tokenkontroll
- Python-receiver med polling og kvittering
- Lokale WAV-filer
- Bluetooth-avspilling gjennom PipeWire
- Integrert stille keepalive som holder høyttaleren våken
- Automatisk restart av receiver og keepalive
- Sentral CometenWebAdmin-integrasjon
- Follow, Sub, Resub, Gifted Sub, Gift Bomb, Bits, Donation, Raid og YouTube Sub

Bekreftet baseline: 31. juli 2026.

## Dokumentasjon

Komplett norsk installasjonsguide:

- [Installasjon - Cometen IRL Alerts](docs/INSTALLASJON_NO.md)

CometenWebAdmin-integrasjon:

- [Sentral alertintegrasjon](integration/cometenwebadmin/README_NO.md)

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

Forventet oppstart:

```text
Cometen IRL Alert Receiver started
Relay: https://dittdomene.no/CometenIRLAlerts_Relay
Audio keepalive started through PipeWire
```

## Autostart

Bruk user-systemd fordi PipeWire og Bluetooth kjører i brukerens lydsesjon:

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

Bruk alltid forskjellige sender- og receiver-token, og roter dem straks dersom de blir eksponert.
