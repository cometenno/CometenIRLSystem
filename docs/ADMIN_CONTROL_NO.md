# IRL Admin Control - Streamer.bot

Denne modulen gir broadcaster/admin lokal kontroll over OBS-delen av Cometen IRL Alerts fra chat.

Fil:

```text
streamerbot/CometenIRL_AdminControl.cs
```

Actionen går **direkte mot OBS og Twitch i Streamer.bot**. Den går ikke gjennom BELABOX-relayen, fordi start/stopp av OBS, scenebytte og Channel Point-grupper tilhører streaming-PC-en.

## Kommandoer

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

Anbefalt rettighet i Streamer.bot: **Broadcaster only**. Særlig `!irlstart` og `!irlstop` skal ikke være åpne for vanlig chat.

## Oppførsel

### `!irlstart`

Sekvens:

```text
watchdog DISARMED
-> IRL - STARTING SOON
-> eventuelt Channel Points til IRL-modus
-> OBS Start Streaming
```

Watchdog blir bevisst holdt av mens Starting Soon kjører, slik at manglende BELABOX-feed ikke kan sende OBS til `IRL - SIGNAL MISTET` før IRL-delen faktisk skal begynne.

Chatrespons ved suksess:

```text
IRL: Starting Soon - stream startet. Bruk !irlgo når du er klar.
```

### `!irlgo`

```text
-> BELABOX SRT
-> watchdog ARMED
```

Når watchdog er armed, får `IRLAlertsController` igjen myndighet til å bytte mellom:

```text
BELABOX SRT
<->
IRL - SIGNAL MISTET
```

### `!irlbrb`

```text
watchdog DISARMED
-> IRL - BRB
```

### `!irlback`

```text
-> BELABOX SRT
-> watchdog ARMED
```

### `!irlend`

```text
watchdog DISARMED
-> IRL - ENDING
```

Streamen stoppes ikke. `!irlstop` brukes når ending er ferdig.

### `!irlstop`

```text
watchdog DISARMED
-> OBS Stop Streaming
-> eventuelt Channel Points tilbake til normal modus
```

Hvis OBS fortsatt rapporterer live etter stop-kommandoen, blir normal reward-modus ikke aktivert automatisk.

## Manuelt scenebytte

`!irlscene` bruker whitelist/aliaser, ikke fritekst-scenavn.

```text
!irlscene soon
!irlscene srt
!irlscene brb
!irlscene end
!irlscene signal
```

Aliasene betyr:

```text
soon   -> IRL - STARTING SOON, watchdog av
srt    -> BELABOX SRT, watchdog på
brb    -> IRL - BRB, watchdog av
end    -> IRL - ENDING, watchdog av
signal -> IRL - SIGNAL MISTET, watchdog av
```

Dette hindrer at chat kan sende OBS til et vilkårlig scenenavn.

## Watchdog armed-state

Ny persisted global:

```text
CometenIRL_WatchdogArmed
```

Verdier:

```text
true  = watchdog kan styre fallback/recovery
false = watchdog oppdaterer fortsatt BELABOX-telemetri, men får ikke bytte scene
```

Hvis globalen ikke finnes, bruker `IRLAlertsController v10` standard `true` for bakoverkompatibilitet.

`CometenIRL_WatchdogLiveOnly` og `CometenIRL_WatchdogArmed` har forskjellige roller:

```text
WatchdogLiveOnly = skal watchdog være aktiv når OBS ikke streamer?
WatchdogArmed    = får watchdog lov til å styre scene akkurat nå?
```

Eksempel produksjon:

```text
CometenIRL_WatchdogLiveOnly = true
```

Under selve IRL-streamen styrer admin-kommandoene `WatchdogArmed` automatisk.

## Scenenavn som globals

Standardnavn:

```text
CometenIRL_StartingSoonScene   = IRL - STARTING SOON
CometenIRL_DefaultReturnScene  = BELABOX SRT
CometenIRL_FallbackScene       = IRL - SIGNAL MISTET
CometenIRL_BrbScene            = IRL - BRB
CometenIRL_EndingScene         = IRL - ENDING
```

Globalene er valgfrie. Hvis de mangler, brukes navnene over.

## Channel Points

Automatisk reward-bytte er laget inn, men er med vilje avslått som standard til gruppene er ferdig satt opp.

```text
CometenIRL_ManageRewards = false
```

Når Channel Point-gruppene er klare:

```text
CometenIRL_ManageRewards = true
CometenIRL_NormalRewardGroup = NORMAL
CometenIRL_IrlRewardGroup = IRL
```

IRL-modus:

```text
NORMAL -> disabled
IRL    -> enabled
```

Normal modus:

```text
IRL    -> disabled
NORMAL -> enabled
```

Rewards som skal finnes i begge oppsett kan ligge i en tredje gruppe som ikke berøres av admin-kontrollen.

### Manuell override

Disse fungerer selv om `CometenIRL_ManageRewards=false`:

```text
!irlpoints on
!irlpoints off
```

De er ment som admin-test/nød-override.

## Oppsett i Streamer.bot

Lag én Action:

```text
CometenIRL_AdminControl
```

Legg inn én `Execute C# Code` med **hele**:

```text
streamerbot/CometenIRL_AdminControl.cs
```

Lag kommandoene og legg `Command Triggered` for alle på samme action.

Anbefalt command mode:

```text
!irlstart   Exact
!irlgo      Exact
!irlbrb     Exact
!irlback    Exact
!irlend     Exact
!irlstop    Exact
!irlscene   Start
!irlpoints  Start
```

`!irlscene` og `!irlpoints` må bruke en modus som gir argumentet etter kommandoen i `%rawInput%`.

## Teststatus

Implementert 16. august 2026.

**Ikke testgodkjent ennå.** Før funksjonen markeres produksjonsklar skal følgende verifiseres i Streamer.bot:

```text
1. C#-filen kompilerer.
2. !irlscene soon/srt/brb/end virker.
3. WatchdogArmed hindrer fallback på Starting Soon/BRB/Ending.
4. !irlgo / !irlback armer watchdog igjen.
5. !irlstart starter OBS korrekt på Starting Soon.
6. !irlstop stopper OBS og lar watchdog være disarmed.
7. Channel Point-grupper testes når de er opprettet.
```
