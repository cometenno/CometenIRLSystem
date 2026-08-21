# Cometen IRL Alerts

Cometen IRL Alerts er hovedmodulen for IRL-varsler, fjernkontroll, status, Browser Audio og BELABOX/OBS-watchdog.

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

Browser Sources
        |
        +--> Sound Alerts --> OBS hjemme
        |                \-> Browser Audio [soundalerts] --+
        +--> Blerp --------> Browser Audio [blerp] --------+--> PipeWire --> Bluetooth

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
- remote-control-kommandoer for status, volum, mute/unmute og alert-test
- Sound Alerts Browser Source på ROCK 5B+ via lokal Playwright Chromium
- samtidig Sound Alerts-avspilling i OBS hjemme og på Bluetooth-høyttaleren via BELABOX

## Browser Audio

Den produksjonsverifiserte single-source-kjeden 21. august 2026 var:

```text
Sound Alerts -> OBS hjemme
Sound Alerts -> BELABOX -> Chromium -> PipeWire -> soundcore Select 4 Go
```

Samme test-alert ble hørt begge steder.

Browser Audio er nå utvidet med multi-source-supervisor. Hver Browser Source får egen Chromium-prosess og kan styres separat. Eksempel:

```text
!irlaudio status
!irlaudio add blerp <url>
!irlaudio blerp off
!irlaudio blerp on
!irlaudio remove blerp
```

Nye chatbaserte multi-source-funksjoner skal praktisk testgodkjennes før de markeres produksjonsverifisert.

Komplett guide:

[`docs/BROWSER_AUDIO_NO.md`](docs/BROWSER_AUDIO_NO.md)

## Twitch URL Guard

`streamerbot/Cometen_ChatUrlGuard.cs` kan kobles til Twitch Chat Message-triggeren.

- broadcaster/mod/VIP kan poste vanlige lenker
- URL-er fra andre brukere slettes
- URL-baserte kommandomeldinger slettes etter at Streamer.bot har mottatt eventet
- dette dekker blant annet `!sr <url>` og `!irlaudio add <navn> <url>`

Guide:

[`docs/CHAT_URL_GUARD_NO.md`](docs/CHAT_URL_GUARD_NO.md)

## Watchdog-status

**IRLAlertsController v9 ble produksjonsverifisert 16. august 2026.**

Bekreftet både i offline testmodus og under faktisk OBS-streaming:

- `CometenIRL_WatchdogLiveOnly=false` lar watchdog kjøre mens OBS ikke streamer, for testing
- `CometenIRL_WatchdogLiveOnly=true` blokkerer scenebytte når OBS er offline
- under faktisk streaming gir `connected=false` / `bitrate=0` fallback til `IRL - SIGNAL MISTET`
- når BELABOX-ingest kommer tilbake og er stabil, går OBS automatisk tilbake til `BELABOX SRT` / tidligere scene
- scenebytte bruker `CPH.ObsSetScene()` for både fallback og recovery
- funksjonen er også verifisert under BELABOX bredbåndsmodus-test

**v10** bygger videre på samme verifiserte watchdog og legger til `CometenIRL_WatchdogArmed`. Når denne er `false`, fortsetter BELABOX-telemetrien å oppdateres, men watchdog får ikke bytte scene. v10/admin-gating skal praktisk testgodkjennes før den markeres produksjonsverifisert.

Ikke kjør NOALBS eller annen automatisk scene-switcher parallelt med `IRLAlertsController`.

## IRL Admin Control

Admin-kontroll ligger i:

```text
streamerbot/CometenIRL_AdminControl.cs
```

Planlagte/implementerte admin-kommandoer omfatter blant annet:

```text
!irlstart
!irlgo
!irlbrb
!irlback
!irlend
!irlstop
!irlscene <alias>
!irlpoints on|off
```

Detaljer:

[`docs/ADMIN_CONTROL_NO.md`](docs/ADMIN_CONTROL_NO.md)

## Installer / oppdater

Kanonisk installasjonsguide:

[`docs/INSTALLASJON_NO.md`](docs/INSTALLASJON_NO.md)

Receiver + heartbeat installeres som user services med:

```bash
cd ~/CometenIRLAlerts/receiver
bash install-user-service.sh
sudo loginctl enable-linger "$USER"
```

GPIO/status-LED og root video-probe installeres/oppdateres med:

```bash
cd ~/CometenIRLAlerts/receiver
bash install-gpio-leds.sh
```

Browser Audio installeres som egen user service innen samme prosjekt. Se `docs/BROWSER_AUDIO_NO.md`.

## Viktige guider

- [`docs/INSTALLASJON_NO.md`](docs/INSTALLASJON_NO.md) - komplett installasjon
- [`docs/WATCHDOG_HEARTBEAT_NO.md`](docs/WATCHDOG_HEARTBEAT_NO.md) - watchdog, heartbeat, 429 og USB-funn
- [`docs/ADMIN_CONTROL_NO.md`](docs/ADMIN_CONTROL_NO.md) - IRL admin chat, OBS start/stopp, scene og Channel Points
- [`docs/BELABOX_ROCK5B_HEADLESS_NO.md`](docs/BELABOX_ROCK5B_HEADLESS_NO.md) - headless ROCK 5B+/Bluetooth/PipeWire
- [`docs/REMOTE_CONTROL_NO.md`](docs/REMOTE_CONTROL_NO.md) - remote control og `!irlaudio`
- [`docs/BROWSER_AUDIO_NO.md`](docs/BROWSER_AUDIO_NO.md) - Browser Source til BELABOX/Bluetooth
- [`docs/CHAT_URL_GUARD_NO.md`](docs/CHAT_URL_GUARD_NO.md) - automatisk sletting av URL-er i Twitch-chat
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

Aldri hardkod eller publiser:

- sender-token
- receiver-token
- databasepassord
- BELABOX stream-ID
- Browser Source-URL/token

## Designregel

Alertlevering, remote control, Browser Audio, URL-guard, heartbeat, LED-status, BELABOX/SRT-watchdog, OBS-failover, IRL admin-kontroll og videre diagnostikk skal samles og koordineres i dette prosjektet.
