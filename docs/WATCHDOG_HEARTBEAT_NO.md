# BELABOX watchdog og heartbeat - Cometen IRL Alerts

Denne guiden dokumenterer status-, heartbeat- og scene-watchdog-delen av Cometen IRL Alerts.

Sist oppdatert: 16. august 2026.

## Formål

Systemet har to separate statusmekanismer:

1. **BELABOX Cloud ingest-watchdog** på streaming-PC-en. Denne ser på faktisk SRT-ingeststatus og brukes for scene-failover i OBS.
2. **Heartbeat fra ROCK 5B+ til CometenIRLAlerts-relayen**. Denne forteller om selve boksen/receiveren er online og brukes som ekstra diagnostikk.

Heartbeat skal ikke brukes som primær beslutning for OBS-scener. En BELABOX kan være online selv om kamera/GStreamer/SRT har falt ut.

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
    +--> IRL - SIGNAL MISTET

Twitch admin chat
    |
    v
CometenIRL_AdminControl
    |
    +--> OBS start/stop
    +--> Starting Soon / BRB / Ending
    +--> CometenIRL_WatchdogArmed

BELABOX / ROCK 5B+
    |
    | HTTPS heartbeat hvert 30. sekund
    v
relay/heartbeat.php
    |
    v
irl_receiver_status
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

Standardverdier:

```json
{
  "heartbeat_receiver_id": "belabox",
  "heartbeat_interval_seconds": 30,
  "heartbeat_timeout_seconds": 5
}
```

Før 16. august 2026 ble heartbeat sendt hvert sekund. Webhotellet/nginx svarte da med `HTTP 429 Too Many Requests`.

Ny standard er 30 sekunder. Ved 429 bruker `heartbeat.py` `Retry-After` når headeren finnes, ellers minst 60 sekunders backoff.

Relayens anbefalte offline-grense:

```php
'receiver_offline_seconds' => 90,
```

Kontroll:

```bash
systemctl --user status cometen-irl-heartbeat.service
journalctl --user -u cometen-irl-heartbeat.service -n 30 --no-pager
```

429-fiksen ble verifisert 16. august 2026 med tjenesten aktiv i flere minutter uten nye 429-feil.

---

## 3. BELABOX Cloud ingest-watchdog

Watchdog-prinsippet bruker faktisk publisher/bitrate på ingest-serveren, ikke OBS Media Source state.

OBS Media Source state ble forkastet som primær signalindikator fordi OBS kan fortsette å rapportere en SRT/FFmpeg-kilde som aktiv selv om transporten er borte eller siste bilde står frosset.

Bekreftet statsformat inneholder blant annet:

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

For gjeldende EU-ingest:

```text
CometenIRL_BelaboxStatsBaseUrl = http://eu.srt.belabox.net:8080
```

Stream-ID skal aldri hardkodes i GitHub. Den ligger som persisted global:

```text
CometenIRL_BelaboxStreamId
```

Scener:

```text
CometenIRL_FallbackScene = IRL - SIGNAL MISTET
CometenIRL_DefaultReturnScene = BELABOX SRT
```

---

## 4. LiveOnly og Armed

Det finnes nå to separate gates.

### `CometenIRL_WatchdogLiveOnly`

```text
false = watchdog kan styre scene selv når OBS ikke streamer - testmodus
true  = watchdog sceneautoritet bare når OBS faktisk streamer - produksjon
```

`true` er live-verifisert og anbefalt produksjonsverdi.

### `CometenIRL_WatchdogArmed`

Lagt til i **IRLAlertsController v10**:

```text
true  = watchdog kan utføre fallback/recovery
false = BELABOX-telemetrien oppdateres fortsatt, men watchdog får ikke bytte scene
```

Hvis globalen mangler, bruker v10 standard `true` for bakoverkompatibilitet.

Denne staten styres av `CometenIRL_AdminControl`:

```text
!irlstart -> false
!irlgo    -> true
!irlbrb   -> false
!irlback  -> true
!irlend   -> false
!irlstop  -> false
```

Dette gjør at Starting Soon, BRB og Ending kan være live uten at en manglende BELABOX-feed kaster OBS over på `IRL - SIGNAL MISTET`.

Detaljer:

```text
docs/ADMIN_CONTROL_NO.md
```

---

## 5. OBS-scener og failover

Normal watchdog-syklus når Armed=true:

```text
BELABOX publisher frisk
        |
        v
BELABOX SRT
        |
        | 2 dårlige målinger
        | connected=false / bitrate=0
        v
IRL - SIGNAL MISTET
        |
        | 5 stabile gode målinger
        v
BELABOX SRT / tidligere scene
```

Tidligere versjoner brukte `CPH.ObsSendRaw("SetCurrentProgramScene", ...)`. Signal-deteksjonen fungerte, men scenebyttet gjorde ikke det pålitelig.

Fra v9 brukes:

```text
CPH.ObsSetScene(sceneName, 0)
```

for både fallback og recovery.

---

## 6. Verifisert status

### v9

**Produksjonsverifisert 16. august 2026.**

Bekreftet flere ganger i offline testmodus med `WatchdogLiveOnly=false`:

```text
BELABOX feed PÅ -> BELABOX SRT
Stop i BELABOX admin -> IRL - SIGNAL MISTET
Start i BELABOX admin -> automatisk tilbake til BELABOX SRT
```

Deretter live-verifisert med:

```text
CometenIRL_WatchdogLiveOnly = true
```

Resultat:

```text
OBS offline + feed av -> ingen scenebytte
OBS live + feed av    -> IRL - SIGNAL MISTET
feed tilbake          -> BELABOX SRT
```

Live-testen fungerte også i BELABOX bredbåndsmodus.

### v10

v10 beholder den verifiserte signal-/fallback-/recovery-logikken fra v9 og legger til `WatchdogArmed` for IRL admin-scener.

**Status per 16. august 2026: implementert, men den nye Armed/admin-integrasjonen er ikke praktisk testgodkjent ennå.**

Før v10 markeres produksjonsverifisert skal følgende testes:

```text
1. v10 kompilerer i Streamer.bot.
2. Armed=false lar Starting Soon/BRB/Ending stå urørt selv ved manglende feed.
3. Telemetri fortsetter å oppdateres mens Armed=false.
4. !irlgo / !irlback setter Armed=true og vanlig fallback/recovery fungerer igjen.
```

---

## 7. Status-globals

Controlleren skriver blant annet:

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

Disse brukes til diagnostikk og videre IRL-integrasjon.

---

## 8. USB/Facecam-funn

Nattesten 16. august 2026 viste at reelle bitrate-bortfall korrelerte med USB-videofeil:

```text
uvcvideo: Non-zero status (-71) in video completion handler
uvcvideo: Failed to resubmit video URB (-1)
gstreamer error from v4l2src0
```

Det ble også observert faktisk USB disconnect og meldingen:

```text
Cannot enable. Maybe the USB cable is bad?
```

Dette behandles som kamera/kabel/USB-hardware-spor og er separat fra SRT-watchdog-logikken.

Autosuspend ble testet og forklarte ikke feilen alene. Videre A/B-test avventes til annet kamera/kabel er tilgjengelig.

---

## 9. Feilsøking

### Signal registreres, men OBS bytter ikke

Kontroller:

```text
CometenIRL_WatchdogArmed = true
CometenIRL_BelaboxConnected = False
CometenIRL_BelaboxBitrate = 0
CometenIRL_BelaboxState = offline
CometenIRL_BelaboxFailCount >= konfigurert FailChecks
```

Hvis `WatchdogArmed=false`, er manglende scenebytte tilsiktet.

Kontroller også at controlleren bruker `CPH.ObsSetScene()`.

### Heartbeat 429

```bash
journalctl --user -u cometen-irl-heartbeat.service -n 20 --no-pager
```

Forventet heartbeat-intervall er 30 sekunder.

### USB/GStreamer

```bash
sudo journalctl -kf -n 0 | grep -Ei 'uvc|usb|video|v4l2|xhci|disconnect|reset|error'
```

### Tjenester

```bash
systemctl --user status cometen-irl-heartbeat.service
systemctl --user status cometen-irl-alerts.service
```

---

## 10. Designregel

Alle IRL-funksjoner skal samles under Cometen IRL Alerts. Watchdog, heartbeat, status, LED-er, volumkontroll, OBS-admin, Channel Point-modus og diagnostikk skal koordineres gjennom samme prosjekt.

Det skal ikke kjøres en separat NOALBS-sceneautoritet parallelt med `IRLAlertsController`.
