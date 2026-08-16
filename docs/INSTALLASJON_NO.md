# Installasjon - Cometen IRL Alerts

Dette er den kanoniske installasjonsveiledningen for Cometen IRL Alerts.

Sist oppdatert: 16. august 2026.

Systemet består av fire deler:

```text
Streamer.bot / CometenWebAdmin på streaming-PC
        |
        | HTTPS
        v
PHP/MySQL-relay på webhotell
        |
        +-----------------------------+
        |                             |
        | alerts / kontroll           | heartbeat/status
        v                             v
Python receiver på ROCK 5B+      irl_receiver_status
        |
        v
PipeWire / Bluetooth / lokale WAV-filer

BELABOX Cloud SRT-stats
        |
        v
IRLAlertsController i Streamer.bot
        |
        +--> BELABOX SRT
        +--> IRL - SIGNAL MISTET
```

BELABOX-video, SRT/SRTLA, alerts, heartbeat og OBS-watchdog ligger i samme prosjekt, men har tydelig separerte roller.

- **Alert-receiver** leverer lyd og fjernkontroll.
- **Heartbeat** forteller om ROCK 5B+/receiveren er online.
- **BELABOX ingest-watchdog** ser på faktisk SRT-ingeststatus og brukes for OBS-failover.

Heartbeat er diagnostikk og skal ikke brukes som eneste signal for scenevalg.

---

# 1. Krav

## Streaming-PC

- Windows
- Streamer.bot
- OBS Studio med OBS WebSocket aktivt
- Internett
- CometenWebAdmin dersom sentral alertintegrasjon skal brukes

## Webhotell

- HTTPS
- PHP 8 eller nyere
- MySQL eller MariaDB med InnoDB
- filopplasting/FTP
- databaseverktøy, for eksempel phpMyAdmin

## ROCK 5B+ / Linux-enhet

- Python 3
- Git
- systemd med user services
- PipeWire
- WirePlumber
- `pw-play`
- `pw-cat`
- Bluetooth dersom Bluetooth-høyttaler brukes

Kontroller:

```bash
python3 --version
git --version
pw-play --version
pw-cat --version
wpctl --version
bluetoothctl --version
```

På vanlig Debian/Ubuntu kan nødvendige pakker typisk installeres med:

```bash
sudo apt update
sudo apt install -y git python3 pipewire-bin wireplumber bluez
```

BELABOX-imaget kan avvike fra vanlig Debian/Ubuntu. Ikke kjør unødvendige systemoppgraderinger på en fungerende BELABOX uten at dette er planlagt.

---

# 2. Klon repoet

```bash
cd ~
git clone https://github.com/la1ona/CometenIRLAlerts.git
cd CometenIRLAlerts
```

Repoet er privat, så GitHub-tilgang må være konfigurert.

Senere oppdatering:

```bash
cd ~/CometenIRLAlerts
git pull
```

Lokale hemmeligheter skal ligge i filer som ikke committes:

```text
receiver/config.json
relay/config.php
```

---

# 3. Opprett database

Opprett MySQL/MariaDB-databasen og importer:

```text
relay/database.sql
```

SQL-filen oppretter tabellene som relayen trenger, inkludert alert-events og receiver/heartbeat-status.

Via phpMyAdmin:

1. Velg databasen.
2. Velg `Importer`.
3. Velg `relay/database.sql`.
4. Kjør importen.

---

# 4. Lag sender- og receiver-token

Bruk to forskjellige lange token.

```bash
python3 -c "import secrets; print(secrets.token_urlsafe(48))"
python3 -c "import secrets; print(secrets.token_urlsafe(48))"
```

Det ene brukes som `sender_token`, det andre som `receiver_token`.

Token skal aldri legges i GitHub, skjermbilder eller offentlig chat.

---

# 5. Installer relay på webhotellet

Last opp PHP-filene i `relay/` til en egen HTTPS-mappe, for eksempel:

```text
https://dittdomene.no/CometenIRLAlerts_Relay
```

Relay-mappen skal minst ha:

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

Kopier `config.example.php` til `config.php` på webhotellet.

Eksempel:

```php
<?php

declare(strict_types=1);

return [
    'database' => [
        'dsn' => 'mysql:host=localhost;dbname=DIN_DATABASE;charset=utf8mb4',
        'username' => 'DIN_DATABASEBRUKER',
        'password' => 'DITT_DATABASEPASSORD',
    ],

    'sender_token' => 'DITT_LANGE_SENDER_TOKEN',
    'receiver_token' => 'DITT_LANGE_RECEIVER_TOKEN',

    'event_ttl_seconds' => 90,
    'lease_seconds' => 30,

    // Heartbeat sendes normalt hvert 30. sekund.
    // Tre tapte heartbeat gir offline.
    'receiver_offline_seconds' => 90,
];
```

Test:

```text
https://dittdomene.no/CometenIRLAlerts_Relay/health.php
```

Forventet svar er JSON med `ok=true`.

## Viktig om HTTP 429

Heartbeat skal **ikke** sendes hvert sekund. Det ga `429 Too Many Requests` fra nginx/webhotellet under test.

Ny standard:

```text
heartbeat_interval_seconds = 30
receiver_offline_seconds = 90
```

`heartbeat.py` har i tillegg backoff ved 429 og bruker `Retry-After` dersom serveren sender headeren.

---

# 6. Konfigurer receiver på ROCK 5B+

```bash
cd ~/CometenIRLAlerts/receiver
cp config.example.json config.json
nano config.json
```

Minimum:

```json
{
  "relay_base_url": "https://dittdomene.no/CometenIRLAlerts_Relay",
  "receiver_token": "DITT_RECEIVER_TOKEN",
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

Den faktiske `config.example.json` inneholder også lydmapping, remote control og LED-oppsett. Behold feltene du bruker.

Valider JSON:

```bash
python3 -m json.tool config.json >/dev/null && echo "config.json OK"
```

---

# 7. Installer lydfiler og test PipeWire

Legg lokale WAV-filer i:

```text
receiver/sounds/
```

Anbefalt:

- WAV
- PCM
- 16-bit
- 44,1 eller 48 kHz

Test:

```bash
cd ~/CometenIRLAlerts/receiver
pw-play sounds/test.wav
```

Keepalive bruker:

```text
pw-cat --playback --rate=48000 --channels=2 --format=s16 -
```

Ikke legg til `--raw`; enkelte PipeWire-versjoner i BELABOX-oppsettet støtter ikke det valget.

---

# 8. Bluetooth

```bash
bluetoothctl
```

Deretter:

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

Kontroller sink:

```bash
wpctl status
```

Sett standard sink ved behov:

```bash
wpctl set-default SINK_ID
```

Det verifiserte headless BELABOX/ROCK 5B+-oppsettet er dokumentert i:

```text
docs/BELABOX_ROCK5B_HEADLESS_NO.md
```

---

# 9. Installer user services

Bruk **user-systemd**, ikke den eldre system-service-installasjonen.

Installer nå begge tjenestene med én kommando:

```bash
cd ~/CometenIRLAlerts/receiver
bash install-user-service.sh
```

Skriptet installerer og starter:

```text
cometen-irl-alerts.service
cometen-irl-heartbeat.service
```

Aktiver linger én gang:

```bash
sudo loginctl enable-linger "$USER"
```

Kontroller alert receiver:

```bash
systemctl --user status cometen-irl-alerts.service
journalctl --user -u cometen-irl-alerts.service -n 30 --no-pager
```

Kontroller heartbeat:

```bash
systemctl --user status cometen-irl-heartbeat.service
journalctl --user -u cometen-irl-heartbeat.service -n 30 --no-pager
```

Forventet heartbeat-linje:

```text
Cometen IRL heartbeat started: receiver_id=belabox interval=30.0s
```

---

# 10. Oppdater en eksisterende ROCK 5B+-installasjon

```bash
cd ~/CometenIRLAlerts
git pull
cd receiver
bash install-user-service.sh
systemctl --user restart cometen-irl-alerts.service
systemctl --user restart cometen-irl-heartbeat.service
```

Kontroller deretter begge loggene.

Hvis webhotellets `receiver_status.php` eller andre relay-filer er endret i repoet, må de oppdaterte PHP-filene også lastes opp til webhotellet. `git pull` på ROCK 5B+ oppdaterer ikke webhotellet.

---

# 11. Streamer.bot sender

Opprett persistente globals:

```text
CometenIRL_RelayUrl
CometenIRL_SenderToken
```

Relay-URL er basemappen, uten `/push.php`.

Lag action:

```text
Cometen IRL Notifications - Send
```

Lim inn hele:

```text
streamerbot/CometenIRL_Send.cs
```

Kompiler.

Test med `eventType=test` eller `eventType=follow` og kjør sender-actionen.

Receiveren skal hente eventen, spille lokal WAV og kvittere den.

---

# 12. Remote control

Fjernkontrollen ligger i:

```text
streamerbot/CometenIRL_RemoteControl.cs
```

Den bruker samme relay og støtter blant annet volum/status/testfunksjoner. Detaljer:

```text
docs/REMOTE_CONTROL_NO.md
```

Remote control skal være en del av samme Cometen IRL Alerts-oppsett, ikke et separat system.

---

# 13. CometenWebAdmin-integrasjon

Sentral videresending ligger i:

```text
integration/cometenwebadmin/irl-forward.js
```

Detaljer:

```text
integration/cometenwebadmin/README_NO.md
```

Når `irl-forward.js` brukes, skal den samme alerten ikke også sende IRL-event separat fra en annen action. Det vil gi doble alerts.

---

# 14. BELABOX Cloud scene-watchdog

Watchdog ligger under:

```text
streamerbot/IRLAlertsController.cs
```

Gjeldende produksjonsverifiserte controller er **v9**.

Målet er:

```text
signal OK       -> BELABOX SRT
signal borte    -> IRL - SIGNAL MISTET
signal stabilt  -> tilbake til BELABOX SRT / tidligere scene
```

Den bruker BELABOX Cloud ingest-stats i stedet for OBS Media Source state.

For EU-ingesten er korrekt stats-base:

```text
http://eu.srt.belabox.net:8080
```

Stream-ID skal **ikke** hardkodes i GitHub.

Streamer.bot globals:

```text
CometenIRL_BelaboxStreamId
CometenIRL_BelaboxStatsBaseUrl = http://eu.srt.belabox.net:8080
CometenIRL_FallbackScene = IRL - SIGNAL MISTET
CometenIRL_DefaultReturnScene = BELABOX SRT
```

Testmodus:

```text
CometenIRL_WatchdogLiveOnly = false
```

Produksjon/live-only:

```text
CometenIRL_WatchdogLiveOnly = true
```

Controlleren aksepterer denne globalen både som ekte bool og som teksten `"true"` / `"false"`.

I produksjon skal `true` brukes. Da er watchdog-sceneautoriteten deaktivert når OBS ikke streamer og aktiv når OBS faktisk streamer.

## OBS-scenebytte

**v9 bruker `CPH.ObsSetScene()` for både fallback og recovery.**

Tidligere `ObsSendRaw("SetCurrentProgramScene", ...)` ble fjernet som sceneautoritet etter at signal-deteksjonen fungerte, men scenebyttet ikke ble utført pålitelig. En separat test-action viste at `CPH.ObsSetScene()` byttet til `IRL - SIGNAL MISTET` umiddelbart med samme OBS-tilkobling og samme scenenavn.

Ikke erstatt `ObsSetScene()` med `ObsSendRaw()` uten ny eksplisitt test.

## Status per 16. august 2026

**IRLAlertsController v9 er produksjonsverifisert.**

Bekreftet flere ganger i testmodus:

```text
BELABOX feed PÅ
-> BELABOX SRT

Stop i BELABOX admin
-> connected=false / bitrate=0
-> IRL - SIGNAL MISTET

Start i BELABOX admin
-> connected=true / bitrate>0
-> 5 stabile gode checks
-> BELABOX SRT
```

Produksjonsmodus ble deretter verifisert med:

```text
CometenIRL_WatchdogLiveOnly = true
```

Bekreftet:

```text
OBS offline + BELABOX feed av
-> ingen automatisk scene-switch

OBS streaming + BELABOX feed av
-> IRL - SIGNAL MISTET

OBS streaming + BELABOX feed på igjen
-> automatisk tilbake til BELABOX SRT
```

Live-testen ble også kjørt i BELABOX bredbåndsmodus og fungerte som forventet.

Dermed er signal-loss, fallback, automatisk recovery og live-only-gating bekreftet fungerende i produksjonsoppsettet.

Full dokumentasjon:

```text
docs/WATCHDOG_HEARTBEAT_NO.md
```

---

# 15. USB/video-feilsøking

Nattest viste reelle videobortfall samtidig med blant annet:

```text
uvcvideo: Non-zero status (-71) in video completion handler
uvcvideo: Failed to resubmit video URB (-1)
gstreamer error from v4l2src0
```

Det ble også observert eksplisitt USB-disconnect på Elgato Facecam. Dette er et input/hardware-spor og er separat fra heartbeat/429-problemet.

Kun nye USB/video-hendelser:

```bash
sudo journalctl -kf -n 0 | grep -Ei 'uvc|usb|video|v4l2|xhci|disconnect|reset|error'
```

Videre fysisk kameratest avventes når annet kamera/kabel er tilgjengelig.

---

# 16. LED-status

LED-modulen ligger i receiver-delen og dokumenteres i:

```text
docs/STATUS_LEDS_NO.md
```

Statusverdier fra controller/receiver skal gjenbrukes av LED-systemet i stedet for å lage egne konkurrerende watchdogs.

Bekreftet LED-prinsipp per 16. august 2026:

```text
grønn = system/online
blå   = Bluetooth/WPS200
gul   = video-input finnes
rød   = BELABOX encoder/output kjører
```

Gul og rød er dermed separate: Stop i BELABOX admin skal slå av rød, mens gul fortsatt lyser dersom videokilden fortsatt finnes.

---

# 17. Sikkerhet

Ikke commit:

```text
relay/config.php
receiver/config.json
```

Ikke hardkod:

- sender-token
- receiver-token
- databasepassord
- BELABOX stream-ID

Ved tokenlekkasje skal tokenet roteres både på relay og klienten som bruker det.

---

# 18. Designregel for prosjektet

Cometen IRL Alerts er hovedmodulen for IRL-funksjoner. Alertlevering, remote control, heartbeat, LED-status, BELABOX/SRT-watchdog, OBS-failover og videre diagnostikk skal samles og koordineres her.

Det skal ikke kjøres en separat NOALBS-sceneautoritet parallelt med `IRLAlertsController`, fordi to automatiske scene-switchere kan konkurrere om OBS.