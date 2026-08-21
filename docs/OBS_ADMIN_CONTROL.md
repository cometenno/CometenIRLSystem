# OBS IRL Admin Control

The OBS IRL Admin Control module gives the broadcaster local control over the IRL stream workflow from Twitch chat.

It runs entirely on the streaming PC through Streamer.bot and OBS. It does not use the BELABOX relay for scene/start/stop operations.

## Main files

```text
streamerbot/CometenIRL_AdminControl.cs
streamerbot/CometenIRL_EndAutoStop.cs
streamerbot/CometenIRL_Setup.cs
```

Current tested admin code family includes the v1.3 admin control and v1.1 Ending helper.

## Recommended permissions

Broadcaster only.

## Commands

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

## Normal workflow

### `!irlstart`

```text
IRL mode = true
watchdog disarmed
scene -> IRL - STARTING SOON
optional Channel Points -> IRL mode
OBS stream starts
```

Use `!irlgo` when ready to switch to the live BELABOX feed.

### `!irlgo`

```text
scene -> BELABOX SRT
IRL mode = true
watchdog armed
```

### `!irlbrb`

```text
scene -> IRL - BRB
watchdog disarmed
```

### `!irlback`

```text
scene -> BELABOX SRT
IRL mode = true
watchdog armed
```

### `!irlend`

```text
watchdog disarmed
scene -> IRL - ENDING
Ending timer starts
OBS stops automatically after the configured delay
normal Channel Point group is restored
IRL mode = false
```

### `!irlstop`

Immediate/manual stop path:

```text
cancel pending Ending
watchdog disarmed
OBS stop
normal Channel Point group restored
IRL mode = false
```

Keep `!irlstop` available as the emergency/manual stop even when the normal workflow uses `!irlend`.

## Ending helper

Action:

```text
CometenIRL_EndAutoStop
```

Run it on a dedicated Streamer.bot queue, for example:

```text
IRL END
```

Do not place it on the same queue as the watchdog.

Default Ending duration:

```text
CometenIRL_EndingSeconds = 25
```

The helper validates that:

- an Ending is still pending
- the Ending sequence ID has not changed
- OBS is still connected
- the active scene is still the Ending scene

This prevents an old delayed helper from stopping the stream after the user has cancelled/changed the Ending workflow.

## Scene aliases

```text
soon     -> IRL - STARTING SOON, watchdog disarmed
srt      -> BELABOX SRT, watchdog armed
brb      -> IRL - BRB, watchdog disarmed
end      -> IRL - ENDING + auto-stop
signal   -> IRL - SIGNAL MISTET, watchdog disarmed
```

Additional aliases accepted by the current code:

```text
soon:   starting, start
srt:    live, go
brb:    pause
end:    ending
signal: lost
```

## Scene globals

```text
CometenIRL_StartingSoonScene
CometenIRL_DefaultReturnScene
CometenIRL_FallbackScene
CometenIRL_BrbScene
CometenIRL_EndingScene
```

Typical defaults:

```text
IRL - STARTING SOON
BELABOX SRT
IRL - SIGNAL MISTET
IRL - BRB
IRL - ENDING
```

## Watchdog armed state

Persistent global:

```text
CometenIRL_WatchdogArmed
```

Meaning:

```text
true  -> watchdog may perform automatic fallback/recovery
false -> watchdog telemetry may continue, but it must not change scenes
```

Starting Soon, BRB and Ending disarm the watchdog. `!irlgo`, `!irlback` and `!irlscene srt/live/go` arm it again.

This is separate from `CometenIRL_WatchdogLiveOnly`.

## Channel Point groups

Persistent globals:

```text
CometenIRL_ManageRewards
CometenIRL_NormalRewardGroup = NORMAL
CometenIRL_IrlRewardGroup = IRL
```

When automatic management is enabled:

```text
IRL mode:
  NORMAL disabled
  IRL enabled

Normal mode:
  IRL disabled
  NORMAL enabled
```

Manual override:

```text
!irlpoints on
!irlpoints off
```

Rewards that should remain available in both modes should be outside the managed groups.

## Persistent language

Global:

```text
CometenIRL_Language
```

Supported values:

```text
no
en
```

Commands:

```text
!irllang no
!irllang en
```

The language is persistent and does not change automatically when the IRL stream starts, stops or reboots.

## Setup action

Action:

```text
CometenIRL_Setup
```

Code:

```text
streamerbot/CometenIRL_Setup.cs
```

Run it during first-time setup and after setup-schema changes. It creates missing persistent globals while preserving values that already exist.

## OBS Output Timer warning

OBS has a separate built-in `Tools -> Output Timer` feature. If it is enabled at the same time as the project Ending helper, OBS can stop for reasons unrelated to Cometen IRL System.

Keep the OBS Output Timer disabled unless it is intentionally part of the workflow.

## Compatibility note

The repository/project branding is Cometen IRL System. Existing action, file and global identifiers using the `CometenIRL_` prefix are intentionally retained for compatibility.

## Verification status

Verified in the real setup:

- `!irlstart`
- `!irlgo`
- `!irlbrb`
- `!irlback`
- `!irlend` + automatic stop
- persistent IRL mode lifecycle
- persistent `!irllang no/en`
- `!irlpoints on/off`
- automatic Channel Point group switching when enabled
- setup action preserving existing globals

The Ending-cancel path should be rechecked after future admin/helper changes.

## Related documentation

- [Commands](COMMANDS.md)
- [Streamer.bot setup](streamerbot-setup.md)
- [Watchdog and Heartbeat](WATCHDOG_HEARTBEAT.md)
