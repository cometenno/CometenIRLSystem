# Arkitektur

Sist oppdatert: 16. august 2026.

Cometen IRL Alerts er hovedmodulen for IRL-returkanal, status og automatikk rundt BELABOX-oppsettet.

## Hovedflyt for alerts

```text
Twitch / YouTube / CometenWebAdmin
       |
       v
Streamer.bot på streaming-PC
       |
       | HTTPS POST + sender-token
       v
PHP/MySQL relay på webhotellet
       |
       | HTTPS polling + receiver-token
       v
Python receiver på ROCK 5B+
       |
       v
PipeWire / Bluetooth / lokal lyd
```

## Heartbeat/status

```text
ROCK 5B+
   |
   | HTTPS POST hvert 30. sekund
   v
heartbeat.php
   |
   v
irl_receiver_status
   |
   v
receiver_status.php
```

Heartbeat er kun diagnostikk. Standard offline-grense er 90 sekunder.

## BELABOX video-watchdog

```text
BELABOX / SRTLA
      |
      v
BELABOX Cloud ingest
      |
      | publisher / bitrate / RTT / dropped packets
      v
IRLAlertsController i Streamer.bot
      |
      +--> BELABOX SRT
      +--> IRL - SIGNAL MISTET
```

Scene-watchdog skal bruke faktisk ingest-telemetri, ikke bare OBS Media Source state og ikke heartbeat alene.

## Rollefordeling

BELABOX/ROCK 5B+ håndterer:

- videokilde
- GStreamer/belacoder
- SRT/SRTLA
- bonding/nettverk
- lokal alert-receiver
- heartbeat
- LED/status på boksen

Streaming-PC håndterer:

- Streamer.bot sender
- remote control
- BELABOX ingest-watchdog
- OBS scene-failover/recovery
- sentral IRL-status

Webhotellet håndterer:

- alert queue
- kvittering/lease
- control-resultater
- heartbeat og receiver-status

## Designregel

Det skal være **én sentral sceneautoritet**: `IRLAlertsController`.

Ikke kjør NOALBS eller en annen automatisk scene-switcher parallelt, fordi to systemer kan konkurrere om OBS-scenen.

Heartbeat, watchdog, remote control, LED-status og senere diagnostikk skal bygges videre i samme Cometen IRL Alerts-prosjekt.

## Levering av alert-events

1. Streamer.bot lager en unik event-ID.
2. Eventen sendes til `push.php`.
3. Relay lagrer eventen med TTL.
4. Receiver henter en lease via `poll.php`.
5. Receiver spiller lokal WAV.
6. Receiver kvitterer via `acknowledge.php`.
7. Ukvittert event kan leveres igjen etter lease-utløp.

## Sikkerhet

- HTTPS i produksjon
- forskjellige sender- og receiver-token
- ingen Twitch-/YouTube-token på relayen
- lydfiler lokalt på ROCK 5B+
- hemmeligheter i `relay/config.php` og `receiver/config.json`
- BELABOX stream-ID skal ikke committes

## Status

Alert-receiver, remote control og heartbeat er normal modulfunksjon.

BELABOX scene-watchdog er fortsatt test/development per 16. august 2026. Ingest/fallback fungerer, mens recovery/pending-state skal ferdigstilles før produksjonsmodus aktiveres.

Se:

```text
docs/INSTALLASJON_NO.md
docs/WATCHDOG_HEARTBEAT_NO.md
```
