# Cometen IRL Alerts

Cometen IRL Alerts er hovedmodulen for IRL-varsler, fjernkontroll, status og BELABOX/OBS-watchdog.

## Arkitektur

```text
Twitch / YouTube / CometenWebAdmin
        |
        v
Streamer.bot på streaming-PC
        |
        | HTTPS
        v
PHP/MySQL-relay på webhotell
        |
        +---------------------------+
        |                           |
        | alerts / kontroll         | heartbeat/status
        v                           v
ROCK 5B+ receiver              receiver status
        |
        v
PipeWire / Bluetooth / lokale WAV-filer

BELABOX Cloud ingest-stats
        |
        v
IRLAlertsController
        |
        +--> BELABOX SRT
        +--> IRL - SIGNAL MISTET
```

## Bekreftet funksjon

- HTTPS-sending fra Streamer.bot
- PHP/MySQL relay med lease og kvittering
- Python receiver på ROCK 5B+
- lokal WAV-avspilling via PipeWire/Bluetooth
- CometenWebAdmin-integrasjon
- remote control for volum/status/test
- headless user-systemd-oppsett
- WPS200/Bluetooth-oppsett
- LED/statusmodul
- heartbeat fra ROCK 5B+ til relay
- heartbeat standardisert til 30 sekunder med 429-backoff
- receiver offline-grense på 90 sekunder
- BELABOX Cloud ingest-telemetri som kilde for video-watchdog
- OBS fallback til `IRL - SIGNAL MISTET`
- automatisk recovery tilbake til `BELABOX SRT` / tidligere scene
- live-only gating med `CometenIRL_WatchdogLiveOnly=true`

## Watchdog-status

**IRLAlertsController v9 er produksjonsverifisert 16. august 2026.**

Bekreftet både i offline testmodus og under faktisk OBS-streaming:

- `CometenIRL_WatchdogLiveOnly=false` lar watchdog kjøre mens OBS ikke streamer, for testing
- `CometenIRL_WatchdogLiveOnly=true` blokkerer scenebytte når OBS er offline
- under faktisk streaming gir `connected=false` / `bitrate=0` fallback til `IRL - SIGNAL MISTET`
- når BELABOX-ingest kommer tilbake og er stabil, går OBS automatisk tilbake til `BELABOX SRT` / tidligere scene
- scenebytte bruker `CPH.ObsSetScene()` for både fallback og recovery
- funksjonen er også verifisert under BELABOX bredbåndsmodus-test

Ikke kjør NOALBS eller annen automatisk scene-switcher parallelt med `IRLAlertsController`.

## Installer / oppdater

Kanonisk installasjonsguide:

[`docs/INSTALLASJON_NO.md`](docs/INSTALLASJON_NO.md)

Receiver + heartbeat installeres som user services med:

```bash
cd ~/CometenIRLAlerts/receiver
bash install-user-service.sh
sudo loginctl enable-linger "$USER"
```

Dette installerer:

```text
cometen-irl-alerts.service
cometen-irl-heartbeat.service
```

GPIO/status-LED og root video-probe installeres/oppdateres med:

```bash
cd ~/CometenIRLAlerts/receiver
bash install-gpio-leds.sh
```

## Viktige guider

- [`docs/INSTALLASJON_NO.md`](docs/INSTALLASJON_NO.md) - komplett installasjon
- [`docs/WATCHDOG_HEARTBEAT_NO.md`](docs/WATCHDOG_HEARTBEAT_NO.md) - watchdog, heartbeat, 429 og USB-funn
- [`docs/BELABOX_ROCK5B_HEADLESS_NO.md`](docs/BELABOX_ROCK5B_HEADLESS_NO.md) - headless ROCK 5B+/Bluetooth/PipeWire
- [`docs/REMOTE_CONTROL_NO.md`](docs/REMOTE_CONTROL_NO.md) - remote control
- [`docs/STATUS_LEDS_NO.md`](docs/STATUS_LEDS_NO.md) - LED-status
- [`docs/streamerbot-setup.md`](docs/streamerbot-setup.md) - Streamer.bot
- [`docs/relay-setup.md`](docs/relay-setup.md) - webrelay
- [`docs/receiver-setup.md`](docs/receiver-setup.md) - receiver

## Heartbeat

Standard i `receiver/config.json`:

```json
"heartbeat_receiver_id": "belabox",
"heartbeat_interval_seconds": 30,
"heartbeat_timeout_seconds": 5
```

Relay bruker:

```php
'receiver_offline_seconds' => 90,
```

1 sekund heartbeat ble forkastet fordi webhotellet/nginx svarte med `HTTP 429 Too Many Requests`.

## LED-status

Bekreftet prinsipp:

```text
grønn = system/online
blå   = Bluetooth/WPS200
gul   = video-input finnes
rød   = BELABOX encoder/output kjører
```

Gul og rød er separate. `Stop` i BELABOX admin kan derfor slå av rød mens gul fortsatt lyser dersom videokilden fortsatt finnes.

## Kjent hardware-spor

Nattest 16. august 2026 viste reelle USB-videobortfall fra den testede Elgato Facecam-kjeden, blant annet `uvcvideo -71`, URB-feil og eksplisitt USB disconnect. Dette behandles som kamera/kabel/USB-hardware-spor og er separat fra SRT-watchdog-logikken.

## Sikkerhet

Aldri commit:

```text
relay/config.php
receiver/config.json
```

Aldri hardkod:

- sender-token
- receiver-token
- databasepassord
- BELABOX stream-ID

## Designregel

Alertlevering, remote control, heartbeat, LED-status, BELABOX/SRT-watchdog, OBS-failover og videre diagnostikk skal samles og koordineres i dette prosjektet.
