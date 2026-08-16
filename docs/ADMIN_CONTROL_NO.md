# IRL Admin Control - Streamer.bot

Denne modulen gir broadcaster/admin lokal kontroll over OBS-delen av Cometen IRL Alerts fra Twitch-chat.

Hovedfil:

```text
streamerbot/CometenIRL_AdminControl.cs
```

Ending-helper:

```text
streamerbot/CometenIRL_EndAutoStop.cs
```

Admin-kontrollen går direkte mot OBS/Twitch i Streamer.bot. Den går ikke gjennom BELABOX-relayen.

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
!irllang no|en
```

Anbefalt rettighet: **Broadcaster only**.

Alle kommandoene over peker til samme action:

```text
CometenIRL_AdminControl
```

`CometenIRL_EndAutoStop` er kun en intern helper og skal ikke ha chat-trigger.

## Normal IRL-flyt

```text
!irlstart
  -> watchdog av
  -> IRL - STARTING SOON
  -> start OBS-stream

!irlgo
  -> BELABOX SRT
  -> watchdog på

!irlbrb
  -> watchdog av
  -> IRL - BRB

!irlback
  -> BELABOX SRT
  -> watchdog på

!irlend
  -> watchdog av
  -> IRL - ENDING
  -> credits/ending får gå i 25 sekunder som standard
  -> OBS stopper automatisk

!irlstop
  -> umiddelbar manuell stopp
```

`!irlstop` beholdes som nød/manuell stopp selv om vanlig avslutning nå skal gjøres med `!irlend`.

## Automatisk Ending

`!irlend` krever ikke lenger en ekstra `!irlstop`.

Standard:

```text
CometenIRL_EndingSeconds = 25
```

Verdien er valgfri og clamped til 5-120 sekunder. Hvis globalen mangler brukes 25 sekunder.

Ending-helperen kjøres med:

```text
CPH.RunAction("CometenIRL_EndAutoStop", false)
```

slik at admin-actionen kan returnere umiddelbart.

### Viktig queue-oppsett

Lag action:

```text
CometenIRL_EndAutoStop
```

med hele `streamerbot/CometenIRL_EndAutoStop.cs`.

Sett denne actionen på en **egen Streamer.bot queue**, for eksempel:

```text
IRL END
```

Ikke bruk samme queue som `IRLAlertsController`/watchdog. Helperen venter i opptil 25 sekunder og skal derfor ikke blokkere watchdog-køen.

### Avbryte Ending

Hvis en ny IRL-livssykluskommando brukes før tiden går ut, annulleres pending auto-stop. Eksempel:

```text
!irlend
!irlback
```

Da skal streamen fortsette og watchdog armeres igjen.

Helperen sjekker også at OBS fortsatt står på `IRL - ENDING` før den stopper streamen. Hvis scenen er endret manuelt, avbrytes auto-stop.

## Språk - persistent NO/EN

Felles persisted global:

```text
CometenIRL_Language
```

Gyldige verdier:

```text
no
en
```

Default når globalen mangler:

```text
no
```

Bytt manuelt med:

```text
!irllang no
!irllang en
```

Språket **endres aldri automatisk** ved start, stopp, restart eller scenebytte. Når engelsk er satt, forblir systemet engelsk til `!irllang no` brukes, og motsatt.

`!irllang` uten parameter viser aktivt språk.

Samme global leses av:

```text
CometenIRL_AdminControl
CometenIRL_RemoteControl
CometenIRL_EndAutoStop
```

Dermed følger admin-responser, status, volum, mute/unmute og alert-test samme valgte språk.

## Watchdog armed-state

Persisted global:

```text
CometenIRL_WatchdogArmed
```

```text
true  = watchdog kan styre fallback/recovery
false = telemetry oppdateres, men watchdog får ikke bytte scene
```

`IRLAlertsController v10` bruker denne separat fra:

```text
CometenIRL_WatchdogLiveOnly
```

Starting Soon, BRB og Ending disarmer watchdog. `!irlgo` og `!irlback` armer den igjen.

## Scene-aliaser

```text
!irlscene soon
!irlscene srt
!irlscene brb
!irlscene end
!irlscene signal
```

```text
soon   -> IRL - STARTING SOON, watchdog av
srt    -> BELABOX SRT, watchdog på
brb    -> IRL - BRB, watchdog av
end    -> IRL - ENDING + automatisk stopp
signal -> IRL - SIGNAL MISTET, watchdog av
```

## Scenenavn som globals

```text
CometenIRL_StartingSoonScene  = IRL - STARTING SOON
CometenIRL_DefaultReturnScene = BELABOX SRT
CometenIRL_FallbackScene      = IRL - SIGNAL MISTET
CometenIRL_BrbScene           = IRL - BRB
CometenIRL_EndingScene        = IRL - ENDING
```

Hvis de mangler, brukes verdiene over.

## Channel Points

Automatisk reward-bytte finnes, men er avslått til reward-gruppene er ferdige:

```text
CometenIRL_ManageRewards = false
```

Når klart:

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

Manuell override:

```text
!irlpoints on
!irlpoints off
```

## Streamer.bot command mode

```text
!irlstart   Exact
!irlgo      Exact
!irlbrb     Exact
!irlback    Exact
!irlend     Exact
!irlstop    Exact
!irlscene   Start
!irlpoints  Start
!irllang    Start
```

## Teststatus 16. august 2026

Praktisk verifisert:

- `!irlstart` - Starting Soon + OBS start fungerer
- `!irlgo` - BELABOX SRT + watchdog armed fungerer
- `!irlbrb` - BRB + watchdog disarmed fungerer
- `!irlback` - retur til BELABOX SRT + watchdog armed fungerer
- v1.1 timing-fiks for OBS scene-confirmation er verifisert
- `!irlend` - Ending-scenen vises, watchdog disarmes og OBS stopper automatisk etter ca. 25 sekunder
- `!irllang en` og `!irllang no` - persistent språkbytte fungerer
- `!irlstatus` følger valgt språk i både EN- og NO-modus

Gjenstår praktisk verifisering:

- cancellation av pending Ending med `!irlback`
- øvrige remote-control-responser på begge språk (volum, mute/unmute og alert-test)
- Channel Point-grupper når de er opprettet
