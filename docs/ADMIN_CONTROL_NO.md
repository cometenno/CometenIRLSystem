# IRL Admin Control - Streamer.bot

Denne modulen gir broadcaster/admin lokal kontroll over OBS-delen av Cometen IRL Alerts fra Twitch-chat.

Hovedfil:

```text
streamerbot/CometenIRL_AdminControl.cs
```

Gjeldende testede admin-kode er **v1.3**.

Ending-helper:

```text
streamerbot/CometenIRL_EndAutoStop.cs
```

Gjeldende helper er **v1.1**.

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

`CometenIRL_EndAutoStop` er kun intern helper og skal ikke ha chat-trigger.

## Normal IRL-flyt

```text
!irlstart
  -> CometenIRL_IrlMode = true
  -> watchdog av
  -> IRL - STARTING SOON
  -> IRL Channel Points-modus dersom automatikk er aktiv
  -> start OBS-stream

!irlgo
  -> CometenIRL_IrlMode = true
  -> BELABOX SRT
  -> watchdog på

!irlbrb
  -> watchdog av
  -> IRL - BRB

!irlback
  -> CometenIRL_IrlMode = true
  -> BELABOX SRT
  -> watchdog på

!irlend
  -> watchdog av
  -> IRL - ENDING
  -> CometenIRL_IrlMode holdes true mens Ending pågår
  -> credits/ending går i 25 sekunder som standard
  -> OBS stopper automatisk
  -> NORMAL Channel Points-modus gjenopprettes
  -> CometenIRL_IrlMode = false

!irlstop
  -> umiddelbar manuell stopp
  -> NORMAL Channel Points-modus gjenopprettes
  -> CometenIRL_IrlMode = false
```

`!irlstop` beholdes som nød/manuell stopp selv om vanlig avslutning gjøres med `!irlend`.

## Automatisk Ending

Standard:

```text
CometenIRL_EndingSeconds = 25
```

Verdien clamped til 5-120 sekunder. Manglende, 0 eller negativ verdi behandles som standard 25 sekunder.

Ending-helperen kjøres asynkront med:

```text
CPH.RunAction("CometenIRL_EndAutoStop", false)
```

### Queue-oppsett

Lag action:

```text
CometenIRL_EndAutoStop
```

med hele `streamerbot/CometenIRL_EndAutoStop.cs`.

Sett helperen på en egen Streamer.bot queue, for eksempel:

```text
IRL END
```

Ikke bruk samme queue som `IRLAlertsController`/watchdog.

### Ending-sikkerhet

Helper v1.1 stopper bare når:

- `CometenIRL_EndPending` fortsatt er true
- `CometenIRL_EndSequence` fortsatt er samme sekvens
- OBS fortsatt er koblet til
- aktiv scene fortsatt er `IRL - ENDING`

Helperen er ikke lenger avhengig av `CometenIRL_IrlMode` som precondition.

OBS sin separate `Tools -> Output Timer` ble funnet aktiv med 30 sekunder under feilsøking. Dette var uavhengig av Cometen IRL Alerts og ble deaktivert.

**Avbrytelse av Ending med `!irlend` etterfulgt av `!irlback` skal støttes av koden, men denne kombinasjonen er ikke markert som endelig retestet etter v1.3/v1.1.**

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

Default er `no`.

Bytt manuelt med:

```text
!irllang no
!irllang en
```

Språket endres aldri automatisk ved start, stopp, restart eller scenebytte.

Samme global leses av:

```text
CometenIRL_AdminControl
CometenIRL_RemoteControl
CometenIRL_EndAutoStop
```

## Watchdog armed-state

Persisted global:

```text
CometenIRL_WatchdogArmed
```

```text
true  = watchdog kan styre fallback/recovery
false = telemetry oppdateres, men watchdog får ikke bytte scene
```

`IRLAlertsController v10` bruker denne separat fra `CometenIRL_WatchdogLiveOnly`.

Starting Soon, BRB og Ending disarmer watchdog. `!irlgo`, `!irlback` og `!irlscene srt/live/go` armer den igjen.

## Scene-aliaser

```text
soon   -> IRL - STARTING SOON, watchdog av
srt    -> BELABOX SRT, IrlMode true, watchdog på
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

## Channel Points

Persisted globals:

```text
CometenIRL_ManageRewards
CometenIRL_NormalRewardGroup = NORMAL
CometenIRL_IrlRewardGroup = IRL
```

På ferdig konfigurert installasjon:

```text
CometenIRL_ManageRewards = true
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

Rewards som skal fungere i begge modi kan stå utenfor disse gruppene.

## Setup-action

Felles installasjonsaction:

```text
CometenIRL_Setup
```

Kilde:

```text
streamerbot/CometenIRL_Setup.cs
```

Setup v1.0 oppretter manglende persisted globals med standardverdier og beholder eksisterende verdier. `CometenIRL_SetupVersion` brukes som setup-versjon.

På fersk installasjon er `CometenIRL_ManageRewards=false` som sikker standard til reward-gruppene er konfigurert. Sender-token og BELABOX stream-ID skal aldri hardkodes i repoet.

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
- `!irlgo` - BELABOX SRT + watchdog armed fungerer og setter IrlMode true
- `!irlbrb` - BRB + watchdog disarmed fungerer
- `!irlback` - retur til BELABOX SRT + watchdog armed fungerer og setter IrlMode true
- v1.1 timing-fiks for OBS scene-confirmation fungerer
- v1.3 IrlMode-livssyklus fungerer
- `!irlend` - Ending + auto-stop med EndAutoStop v1.1 fungerer
- etter auto-stop blir IrlMode false
- `!irllang en` og `!irllang no` fungerer persistent
- `!irlstatus` følger valgt språk i EN og NO
- `!irlpoints on` og `!irlpoints off` fungerer manuelt
- automatisk Channel Points-bytte ved `!irlstart` og `!irlend` fungerer med `CometenIRL_ManageRewards=true`
- `CometenIRL_Setup v1.0` er kjørt og verifisert til å beholde eksisterende globals og gjenopprette `CometenIRL_SetupVersion` når den mangler

Gjenstår praktisk verifisering:

- Ending-cancel-test etter endelig v1.3/v1.1 (`!irlend`, deretter `!irlback` før timeout)
- øvrige remote-control-responser på begge språk (volum, mute/unmute og alert-test)
- komplett Streamer.bot eksport/import-pakke er planlagt, men ikke laget ennå
