# Oppsett i Streamer.bot

Sist oppdatert: 16. august 2026.

Streamer.bot-delen av Cometen IRL Alerts består nå av:

```text
CometenIRL_Setup.cs           oppretter manglende globals/defaults
CometenIRL_Send.cs            alert-sender
CometenIRL_RemoteControl.cs   remote control/status
CometenIRL_AdminControl.cs    IRL admin/chat control
CometenIRL_EndAutoStop.cs     Ending auto-stop helper
IRLAlertsController.cs        BELABOX/OBS watchdog-controller
```

Alle hører til samme Cometen IRL Alerts-modul.

## 1. Kjør Setup-action

Lag action:

```text
CometenIRL_Setup
```

Legg inn hele:

```text
streamerbot/CometenIRL_Setup.cs
```

Kompiler og kjør actionen én gang.

Setup v1.0:

- oppretter manglende persisted globals med standardverdier
- overskriver ikke globals som allerede finnes
- kan kjøres på nytt etter oppdateringer
- setter `CometenIRL_SetupVersion = 1.0`
- oppretter sender-token og BELABOX stream-ID som tomme dersom de mangler, slik at hemmeligheter ikke hardkodes
- starter Channel Points-automatikken som `false` på en fersk installasjon til reward-gruppene er konfigurert

Verifisert 16. august 2026: eksisterende verdier ble beholdt, og en slettet `CometenIRL_SetupVersion` ble opprettet igjen av Setup-actionen.

## 2. Installasjonsspesifikke globals

Etter Setup må disse fylles med korrekt verdi for installasjonen:

```text
CometenIRL_RelayUrl
CometenIRL_SenderToken
CometenIRL_BelaboxStreamId
```

Token og stream-ID skal ikke committes.

BELABOX stats-base har standard:

```text
CometenIRL_BelaboxStatsBaseUrl = http://eu.srt.belabox.net:8080
```

## 3. OBS-scener

Standard scenenavn fra Setup:

```text
CometenIRL_StartingSoonScene  = IRL - STARTING SOON
CometenIRL_DefaultReturnScene = BELABOX SRT
CometenIRL_FallbackScene      = IRL - SIGNAL MISTET
CometenIRL_BrbScene           = IRL - BRB
CometenIRL_EndingScene        = IRL - ENDING
```

Navnene kan endres via globals dersom en annen installasjon bruker andre OBS-scener.

## 4. Sender-action

Lag action for `streamerbot/CometenIRL_Send.cs`.

Senderen bruker:

```text
CometenIRL_RelayUrl
CometenIRL_SenderToken
```

Eksempler på eventtyper:

```text
follow
sub
resub
gifted
giftbomb
raid
bits
donation
youtubesub
```

## 5. Remote control

Bruk hele:

```text
streamerbot/CometenIRL_RemoteControl.cs
```

Dette dekker blant annet:

```text
!volum
!volNN
!volup
!voldown
!mute
!unmute
!irlstatus
!alerttest
```

Remote-responser følger persisted `CometenIRL_Language` (`no`/`en`).

Detaljer:

```text
docs/REMOTE_CONTROL_NO.md
```

## 6. Admin control

Bruk:

```text
streamerbot/CometenIRL_AdminControl.cs
```

Gjeldende testede versjon er **v1.3**.

Kommandoer:

```text
!irlstart
!irlgo
!irlbrb
!irlback
!irlend
!irlstop
!irlscene <alias>
!irlpoints on|off
!irllang no|en
```

Anbefalt rettighet er Broadcaster only.

`!irlgo`, `!irlback` og `!irlscene srt/live/go` setter aktiv IRL-livssyklus (`CometenIRL_IrlMode=true`) og armer watchdog.

Detaljer:

```text
docs/ADMIN_CONTROL_NO.md
```

## 7. Ending helper

Lag action:

```text
CometenIRL_EndAutoStop
```

med:

```text
streamerbot/CometenIRL_EndAutoStop.cs
```

Gjeldende helper er v1.1.

Anbefalt egen queue:

```text
IRL END
```

Standard Ending-tid:

```text
CometenIRL_EndingSeconds = 25
```

`!irlend` er verifisert til å vise Ending, stoppe OBS automatisk og returnere IrlMode til false.

## 8. Channel Points

Setup oppretter:

```text
CometenIRL_ManageRewards = false
CometenIRL_NormalRewardGroup = NORMAL
CometenIRL_IrlRewardGroup = IRL
```

Opprett/organiser reward-gruppene `NORMAL` og `IRL` i Streamer.bot. Rewards som skal fungere både normalt og på IRL kan stå utenfor disse gruppene.

Når gruppene er klare:

```text
CometenIRL_ManageRewards = true
```

Verifisert flyt:

```text
!irlstart
-> NORMAL disabled
-> IRL enabled

!irlend
-> etter auto-stop: IRL disabled
-> NORMAL enabled
```

Manuell override er verifisert med:

```text
!irlpoints on
!irlpoints off
```

## 9. BELABOX watchdog

`IRLAlertsController.cs` er gjeldende **v10**.

Viktige globals:

```text
CometenIRL_WatchdogLiveOnly = true
CometenIRL_WatchdogArmed
CometenIRL_BelaboxFailChecks = 2
CometenIRL_BelaboxQueryFailChecks = 3
CometenIRL_BelaboxRecoverChecks = 5
```

`CometenIRL_WatchdogLiveOnly=true` er produksjonsverifisert. Watchdog bruker BELABOX Cloud ingest-stats og `CPH.ObsSetScene()` som sceneautoritet.

Verifisert:

```text
signal OK       -> BELABOX SRT
signal borte    -> IRL - SIGNAL MISTET
signal stabilt  -> automatisk recovery
OBS offline     -> ingen scene-switch når LiveOnly=true
```

Detaljer:

```text
docs/WATCHDOG_HEARTBEAT_NO.md
```

## 10. Runtime/status globals

Setup oppretter også runtime/statusverdier som controlleren bruker:

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

Flere av disse skrives løpende av watchdog og kan derfor bli opprettet igjen automatisk dersom de slettes mens controlleren kjører.

## 11. CometenWebAdmin

Ved bruk av:

```text
integration/cometenwebadmin/irl-forward.js
```

skal samme alert ikke også sendes via en separat IRL-sub-action. Det gir doble varsler.

## 12. Status / videre installasjonspakke

Per 16. august 2026 er koden og Setup-actionen et fungerende utgangspunkt for en senere komplett Streamer.bot import/export-pakke.

Planlagt sluttmål:

```text
Import Streamer.bot-pakke
-> kjør CometenIRL_Setup
-> fyll inn relay/token/BELABOX stream-ID
-> kontroller OBS-scener og reward-grupper
-> ferdig
```

Selve komplette eksportpakken er **ikke laget ennå**.
