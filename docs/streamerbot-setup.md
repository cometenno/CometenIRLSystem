# Oppsett i Streamer.bot

Sist oppdatert: 16. august 2026.

Streamer.bot-delen består av:

```text
CometenIRL_Send.cs            alert-sender
CometenIRL_RemoteControl.cs   remote control/status
IRLAlertsController.cs        BELABOX/OBS watchdog-controller
```

Alle tre hører til samme Cometen IRL Alerts-modul.

## 1. Relay-globals

Opprett som **Persisted Global Variables**:

```text
CometenIRL_RelayUrl
CometenIRL_SenderToken
```

Relay URL er basemappen, for eksempel:

```text
https://DITT-DOMENE/CometenIRLAlerts_Relay
```

Ikke legg `/push.php` i globalen.

## 2. Sender-action

Lag action:

```text
Cometen IRL Notifications - Send
```

Legg inn `Execute C# Code` og lim inn hele:

```text
streamerbot/CometenIRL_Send.cs
```

Kompiler.

## 3. Test sender

Lag en midlertidig action med:

```text
eventType = test
message = Cometen IRL test
```

Kjør deretter:

```text
Cometen IRL Notifications - Send
```

`test.wav` skal spilles én gang på receiveren.

## 4. Virkelige eventtyper

Eksempler:

```text
Twitch Follow       -> follow
Twitch Sub          -> sub
Twitch Re-Sub       -> resub
Twitch Gift Sub     -> giftsub/gifted
Twitch Gift Bomb    -> giftbomb
Twitch Raid         -> raid
Twitch Cheer        -> bits
Donation            -> donation
YouTube Sub         -> youtubesub
Channel Point       -> channelpoint
```

Senderen leser vanlige Streamer.bot-argumenter og kan også få eksplisitte argumenter som:

```text
eventType
message
sound
priority
amount
```

## 5. Remote control

Bruk hele:

```text
streamerbot/CometenIRL_RemoteControl.cs
```

Dette dekker IRL-volum, mute/unmute, status og alerttest gjennom samme relay.

Detaljer:

```text
docs/REMOTE_CONTROL_NO.md
```

## 6. BELABOX watchdog-globals

Watchdog bruker BELABOX Cloud ingest-stats.

Obligatorisk lokal/global stream-ID:

```text
CometenIRL_BelaboxStreamId
```

Stream-ID skal ikke hardkodes eller committes.

For dagens EU-ingest:

```text
CometenIRL_BelaboxStatsBaseUrl = http://eu.srt.belabox.net:8080
```

Scener:

```text
CometenIRL_FallbackScene = IRL - SIGNAL MISTET
CometenIRL_DefaultReturnScene = BELABOX SRT
```

Testmodus:

```text
CometenIRL_WatchdogLiveOnly = false
```

Produksjon, når controlleren er godkjent:

```text
CometenIRL_WatchdogLiveOnly = true
```

Statusglobals som kan brukes videre:

```text
CometenIRL_BelaboxConnected
CometenIRL_BelaboxBitrate
CometenIRL_BelaboxRtt
CometenIRL_BelaboxDroppedPackets
CometenIRL_BelaboxState
```

Disse skal gjenbrukes av senere `!irlstatus`, LED-status og diagnostikk.

## 7. Watchdog-action

`IRLAlertsController.cs` kjøres fra én sentral Streamer.bot-action, typisk med repetisjon omtrent én gang per sekund under test.

Controlleren skal være eneste sceneautoritet for denne funksjonen. Ikke kjør NOALBS eller en annen automatisk scene-switcher parallelt.

OBS-scener:

```text
BELABOX SRT
IRL - SIGNAL MISTET
```

### Status per 16. august 2026

Watchdog er fortsatt test/development. Ingestdeteksjon og fallback er bekreftet, men pending/recovery-state skal ferdigstilles før `CometenIRL_WatchdogLiveOnly=true` regnes som produksjonsklar.

Detaljer:

```text
docs/WATCHDOG_HEARTBEAT_NO.md
```

## 8. Viktig om scene-bekreftelse

OBS WebSocket-scenebytte skjer asynkront. `SetCurrentProgramScene` kan være sendt korrekt selv om `ObsGetCurrentScene` fortsatt viser gammel scene noen millisekunder etterpå.

Controlleren skal derfor kontrollere sceneendringen på et senere tick og ikke kreve bekreftelse i samme millisekund.

## 9. CometenWebAdmin

Ved bruk av:

```text
integration/cometenwebadmin/irl-forward.js
```

skal samme alert ikke også sendes via en separat IRL-sub-action. Det gir doble varsler.

## 10. Feilsøking

Streamer.bot-logg:

```text
CometenIRL
```

Ved BELABOX-watchdog kontroller spesielt:

```text
stats source
connected
bitrate
current scene
fallback/recovery
```

Heartbeat-429 håndteres på ROCK 5B+/relay-siden og er ikke en Streamer.bot-feil.
