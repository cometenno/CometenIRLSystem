# BELABOX watchdog og heartbeat - Cometen IRL Alerts

Denne guiden dokumenterer status-, heartbeat- og scene-watchdog-delen av Cometen IRL Alerts.

Sist oppdatert: 16. august 2026.

## Formål

Modulen har to separate statusmekanismer:

1. **BELABOX Cloud ingest-watchdog** på streaming-PC-en. Denne ser på faktisk SRT-ingeststatus og brukes for scene-failover i OBS.
2. **Heartbeat fra ROCK 5B+ til CometenIRLAlerts-relayen**. Denne forteller om selve boksen/receiveren er online og brukes kun som ekstra diagnostikk.

Heartbeat skal **ikke** brukes som primær beslutning for OBS-scener. En BELABOX kan være online selv om kamera/GStreamer/SRT har falt ut.

---

## 1. Arkitektur

```text
BELABOX / ROCK 5B+
    |
    | SRT/SRTLA
    v
BELABOX Cloud ingest
    |
    | stats JSON
    v
Streamer.bot / IRLAlertsController
    |
    +--> BELABOX SRT
    |
    +--> IRL - SIGNAL MISTET

BELABOX / ROCK 5B+
    |
    | HTTPS heartbeat hvert 30. sekund
    v
relay/heartbeat.php
    |
    v
irl_receiver_status
    |
    v
relay/receiver_status.php
```

---

## 2. Heartbeat

Filer:

```text
receiver/heartbeat.py
receiver/cometen-irl-heartbeat-user.service
relay/heartbeat.php
relay/receiver_status.php
```

Heartbeat bruker samme `relay_base_url` og `receiver_token` som alert-receiveren.

Standardverdier i `receiver/config.json`:

```json
{
  "heartbeat_receiver_id": "belabox",
  "heartbeat_interval_seconds": 30,
  "heartbeat_timeout_seconds": 5
}
```

### Hvorfor 30 sekunder

Før 16. august 2026 ble heartbeat sendt hvert sekund. Webhotellet/nginx begynte da å svare med:

```text
HTTP 429 Too Many Requests
```

`heartbeat.php` og `bootstrap.php` har ingen intern 429-rate-limit. 429-svaret kom derfor fra webhotellet/proxyen foran PHP.

Ny standard er 30 sekunder. Dersom relayen fortsatt svarer 429, bruker `heartbeat.py` `Retry-After` når headeren finnes, ellers minst 60 sekunders backoff før neste forsøk.

### Offline-grense

`receiver_status.php` bruker `receiver_offline_seconds` fra relayens `config.php`.

Anbefalt og dokumentert verdi:

```php
'receiver_offline_seconds' => 90,
```

Dette tilsvarer tre tapte 30-sekunders heartbeats før receiveren rapporteres offline.

### Kontrollere heartbeat

```bash
systemctl --user status cometen-irl-heartbeat.service
```

```bash
journalctl --user -u cometen-irl-heartbeat.service -n 30 --no-pager
```

Forventet oppstart:

```text
Cometen IRL heartbeat started: receiver_id=belabox interval=30.0s
```

429-fiksen ble verifisert 16. august 2026 med tjenesten aktiv i flere minutter uten nye 429-feil.

---

## 3. BELABOX Cloud ingest-watchdog

Watchdog-prinsippet er hentet fra samme type ingest-telemetri som NOALBS bruker: scenevalg skal baseres på **faktisk publisher/bitrate på ingest-serveren**, ikke på OBS Media Source state.

OBS Media Source state ble forkastet som primær signalindikator fordi OBS kan fortsette å rapportere en SRT/FFmpeg-kilde som aktiv selv om transporten er borte eller siste bilde står frosset.

### Bekreftet statsformat

BELABOX Cloud returnerer blant annet:

```json
{
  "publishers": {
    "STREAM_ID": {
      "connected": true,
      "bitrate": 1800,
      "rtt": 20,
      "dropped_pkts": 0
    }
  }
}
```

For den nåværende EU-ingesten skal stats-kilden være:

```text
http://eu.srt.belabox.net:8080
```

Ikke hardkod stream-ID i GitHub. Stream-ID skal ligge som persistent global i Streamer.bot.

### Streamer.bot globals

Obligatorisk:

```text
CometenIRL_BelaboxStreamId
```

Anbefalt eksplisitt stats-URL:

```text
CometenIRL_BelaboxStatsBaseUrl = http://eu.srt.belabox.net:8080
```

Scener:

```text
CometenIRL_FallbackScene = IRL - SIGNAL MISTET
CometenIRL_DefaultReturnScene = BELABOX SRT
```

Test/produksjon:

```text
CometenIRL_WatchdogLiveOnly = false
```

betyr at watchdog får kjøre selv om OBS ikke streamer. Dette brukes under test.

```text
CometenIRL_WatchdogLiveOnly = true
```

betyr at watchdog bare er aktiv når OBS faktisk streamer. Dette er anbefalt produksjonsinnstilling og er live-verifisert.

Controller v8/v9 leser denne globalen robust både når Streamer.bot har lagret den som ekte bool og når den er lagret som teksten `"true"` / `"false"`.

Statusverdier som controlleren skriver:

```text
CometenIRL_BelaboxConnected
CometenIRL_BelaboxBitrate
CometenIRL_BelaboxRtt
CometenIRL_BelaboxDroppedPackets
CometenIRL_BelaboxState
CometenIRL_BelaboxFailCount
CometenIRL_BelaboxRecoverCount
CometenIRL_BelaboxQueryFailCount
CometenIRL_SrtFallbackActive
CometenIRL_SrtReturnScene
```

Disse er beregnet for `!irlstatus`, diagnostikk og videre IRL-integrasjon.

---

## 4. OBS-scener og scenebytte

Bekreftede scenenavn:

```text
Normal/IRL-video: BELABOX SRT
Fallback:         IRL - SIGNAL MISTET
```

Watchdog oppfører seg slik:

```text
BELABOX publisher frisk
        |
        v
BELABOX SRT
        |
        | 2 bekreftede dårlige målinger
        | connected=false / bitrate=0
        v
IRL - SIGNAL MISTET
        |
        | 5 stabile gode målinger
        v
BELABOX SRT / tidligere scene
```

### Viktig funn om OBS API

Tidligere versjoner brukte:

```text
CPH.ObsSendRaw("SetCurrentProgramScene", ...)
```

Signal-deteksjonen fungerte, men OBS-scenen ble ikke byttet pålitelig. Dette ble isolert ved en separat Streamer.bot-test: samme OBS-tilkobling og samme scenenavn byttet umiddelbart når `CPH.ObsSetScene()` ble brukt.

Fra **IRLAlertsController v9** brukes derfor:

```text
CPH.ObsSetScene(sceneName, 0)
```

for både fallback og recovery. Controlleren venter kort og leser deretter aktiv scene for å bekrefte byttet.

`ObsSendRaw()` skal ikke tas tilbake som sceneautoritet uten ny begrunnelse/test.

---

## 5. Verifisert status - IRLAlertsController v9

**Produksjonsverifisert 16. august 2026.**

Først ble følgende testet flere ganger manuelt via BELABOX admin mens OBS ikke streamet og `CometenIRL_WatchdogLiveOnly=false`:

```text
Starttilstand:
BELABOX feed PÅ
OBS scene = BELABOX SRT

Stop i BELABOX admin:
BELABOX Cloud -> connected=false / bitrate=0
watchdog -> IRL - SIGNAL MISTET

Start i BELABOX admin:
BELABOX Cloud -> connected=true / bitrate>0
5 stabile gode målinger
watchdog -> BELABOX SRT
```

Deretter ble `CometenIRL_WatchdogLiveOnly=true` verifisert:

```text
OBS ikke streaming:
BELABOX feed av -> ingen automatisk scenebytte

OBS streaming:
BELABOX feed av -> IRL - SIGNAL MISTET
BELABOX feed på -> automatisk tilbake til BELABOX SRT
```

Live-testen ble også kjørt i BELABOX bredbåndsmodus og fungerte som forventet.

Bekreftet:

- stats fra BELABOX Cloud registrerer faktisk signalstatus
- `connected=false` / `bitrate=0` gir fallback etter konfigurert antall dårlige checks
- OBS bytter til `IRL - SIGNAL MISTET`
- når feed kommer tilbake, teller controlleren stabile gode målinger
- OBS går automatisk tilbake til `BELABOX SRT`
- hele fallback -> recovery-syklusen er testet flere ganger
- scenebytte fungerer med `CPH.ObsSetScene()`
- `WatchdogLiveOnly=true` blokkerer watchdog-sceneautoritet når OBS ikke streamer
- samme live-only-innstilling aktiverer fallback/recovery korrekt under faktisk OBS-streaming

### Produksjonsstatus

IRLAlertsController v9 er **produksjonsverifisert** for gjeldende BELABOX/OBS-oppsett.

Anbefalt produksjonsinnstilling:

```text
CometenIRL_WatchdogLiveOnly = true
```

Bruk `false` kun når scenelogikken skal kunne testes mens OBS ikke streamer.

---

## 6. USB/Facecam-funn fra nattest

Nattesten 16. august 2026 viste at reelle bitrate-bortfall korrelerte med feil fra USB-videokilden, blant annet:

```text
uvcvideo: Non-zero status (-71) in video completion handler
uvcvideo: Failed to resubmit video URB (-1)
gstreamer error from v4l2src0
```

Det ble også observert en eksplisitt kernelmelding:

```text
usb usb8-port1: Cannot enable. Maybe the USB cable is bad?
usb 8-1: USB disconnect
```

Kameraet ble deretter registrert på nytt som Elgato Facecam.

Dette betyr at watchdog i disse tilfellene reagerte på et **reelt input-/USB-bortfall**, ikke et falskt SRT-nettverksproblem.

Autosuspend ble testet ved å sette:

```bash
echo on | sudo tee /sys/bus/usb/devices/8-1/power/control
```

Feilene fortsatte, så autosuspend alene forklarte ikke problemet.

Videre USB/kameratest avventes til annet kamera/kabel kan testes.

---

## 7. Feilsøking

### Watchdog registrerer tap, men OBS bytter ikke

Kontroller globals mens feed er av:

```text
CometenIRL_BelaboxConnected = False
CometenIRL_BelaboxBitrate = 0
CometenIRL_BelaboxState = offline
CometenIRL_BelaboxFailCount >= konfigurert FailChecks
```

Hvis disse stemmer, fungerer ingestdeteksjonen. Kontroller at Streamer.bot kjører **v9 eller nyere** og at `SwitchScene()` bruker `CPH.ObsSetScene()`.

### Heartbeat 429

Kontroller intervallet:

```bash
journalctl --user -u cometen-irl-heartbeat.service -n 20 --no-pager
```

Det skal stå:

```text
interval=30.0s
```

Hvis det står `1.0s`, kontroller `receiver/config.json` og restart tjenesten:

```bash
systemctl --user restart cometen-irl-heartbeat.service
```

### USB/GStreamer

Kun nye kernelhendelser:

```bash
sudo journalctl -kf -n 0 | grep -Ei 'uvc|usb|video|v4l2|xhci|disconnect|reset|error'
```

### Heartbeat-status

```bash
systemctl --user status cometen-irl-heartbeat.service
```

### Alert-receiver

```bash
systemctl --user status cometen-irl-alerts.service
```

### Oppdatering

```bash
cd ~/CometenIRLAlerts
git pull
systemctl --user daemon-reload
systemctl --user restart cometen-irl-alerts.service
systemctl --user restart cometen-irl-heartbeat.service
```

---

## 8. Designregel

Alle IRL-funksjoner skal samles under Cometen IRL Alerts. Watchdog, heartbeat, status, LED-er, volumkontroll, diagnostikk og senere IRL-funksjoner skal koordineres gjennom samme prosjekt. Det skal ikke bygges konkurrerende scene-watchdogs ved siden av `IRLAlertsController`.