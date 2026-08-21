# Streamer.bot setup

Streamer.bot is the control plane on the streaming PC. It sends alert/control events to the relay, controls OBS locally, runs the BELABOX ingest watchdog and exposes the Twitch chat commands.

## Required persistent globals

Core relay globals:

```text
CometenIRL_RelayUrl
CometenIRL_SenderToken
```

Watchdog/admin globals commonly used:

```text
CometenIRL_BelaboxStreamId
CometenIRL_BelaboxStatsBaseUrl
CometenIRL_FallbackScene
CometenIRL_DefaultReturnScene
CometenIRL_StartingSoonScene
CometenIRL_BrbScene
CometenIRL_EndingScene
CometenIRL_EndingSeconds
CometenIRL_WatchdogLiveOnly
CometenIRL_WatchdogArmed
CometenIRL_IrlMode
CometenIRL_Language
CometenIRL_ManageRewards
CometenIRL_NormalRewardGroup
CometenIRL_IrlRewardGroup
```

Never hardcode sender tokens or BELABOX stream IDs in repository files.

## Core actions

### 1. Alert sender

Action name:

```text
Cometen IRL Notifications - Send
```

Code:

```text
streamerbot/CometenIRL_Send.cs
```

Purpose: send normal alert events through the relay to BELABOX.

### 2. Remote Control

Action name:

```text
CometenIRL_RemoteControl
```

Code:

```text
streamerbot/CometenIRL_RemoteControl.cs
```

Purpose: volume, mute/unmute, status and test alert.

### 3. Browser Audio control

Action name:

```text
CometenIRL_BrowserAudioControl
```

Code:

```text
streamerbot/CometenIRL_BrowserAudioControl.cs
```

Purpose: manage named Browser Audio sources on BELABOX.

Create one Twitch command:

```text
!irlaudio
```

Use **Starts With** so the remainder of the command is available as input.

Recommended permission: Broadcaster + Moderators.

### 4. IRL Admin Control

Action name:

```text
CometenIRL_AdminControl
```

Code:

```text
streamerbot/CometenIRL_AdminControl.cs
```

Purpose: local OBS start/go/BRB/back/end/stop, scene aliases, Channel Points and persistent language.

Recommended permission: Broadcaster only.

### 5. Ending helper

Action name:

```text
CometenIRL_EndAutoStop
```

Code:

```text
streamerbot/CometenIRL_EndAutoStop.cs
```

Do not give this action a Twitch command trigger.

Put it on a dedicated queue such as:

```text
IRL END
```

Do not use the same queue as the watchdog.

### 6. BELABOX ingest watchdog

Action/controller:

```text
IRLAlertsController
```

Code:

```text
streamerbot/IRLAlertsController.cs
```

Purpose: read BELABOX ingest telemetry and control automatic OBS fallback/recovery.

This must be the only automatic signal-loss scene authority.

### 7. IRL setup action

Action name:

```text
CometenIRL_Setup
```

Code:

```text
streamerbot/CometenIRL_Setup.cs
```

Run once on initial setup and again after setup-schema changes. It creates missing persistent globals while preserving existing values.

## Twitch command mapping

Recommended:

```text
!irlstart   Exact -> CometenIRL_AdminControl
!irlgo      Exact -> CometenIRL_AdminControl
!irlbrb     Exact -> CometenIRL_AdminControl
!irlback    Exact -> CometenIRL_AdminControl
!irlend     Exact -> CometenIRL_AdminControl
!irlstop    Exact -> CometenIRL_AdminControl
!irlscene   Starts With -> CometenIRL_AdminControl
!irlpoints  Starts With -> CometenIRL_AdminControl
!irllang    Starts With -> CometenIRL_AdminControl

!irlaudio   Starts With -> CometenIRL_BrowserAudioControl
```

Remote-control commands may be mapped individually to `CometenIRL_RemoteControl`:

```text
!irlstatus
!alerttest
!volum
!vol
!volup
!voldown
!mute
!unmute
```

See [Commands](COMMANDS.md) for the full syntax.

## OBS connection

`CometenIRL_AdminControl` expects the configured OBS connection to be available in Streamer.bot.

Verify:

- OBS WebSocket is enabled
- Streamer.bot is connected to OBS
- scene names match the persistent globals
- the BELABOX SRT scene exists
- Starting Soon, BRB, Ending and Signal Lost scenes exist if those functions are enabled

## Normal IRL workflow

```text
!irlstart
  -> Starting Soon
  -> watchdog disarmed
  -> optional IRL Channel Points mode
  -> OBS stream starts

!irlgo
  -> BELABOX SRT
  -> watchdog armed

!irlbrb
  -> BRB
  -> watchdog disarmed

!irlback
  -> BELABOX SRT
  -> watchdog armed

!irlend
  -> Ending
  -> watchdog disarmed
  -> delayed auto-stop
  -> normal rewards restored
  -> IRL mode off
```

`!irlstop` remains available as the immediate/manual stop path.

## Watchdog test mode

For offline testing:

```text
CometenIRL_WatchdogLiveOnly = false
```

For production:

```text
CometenIRL_WatchdogLiveOnly = true
```

When `true`, the watchdog does not automatically switch OBS scenes while OBS is not streaming.

`CometenIRL_WatchdogArmed` is separate. Admin commands disarm the watchdog on Starting Soon/BRB/Ending and arm it again on BELABOX SRT.

## Channel Point groups

Optional automatic group switching uses:

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

Rewards that should always remain available should not be placed in either managed group.

## Language

Persistent global:

```text
CometenIRL_Language
```

Values:

```text
no
en
```

Change manually with:

```text
!irllang no
!irllang en
```

The language does not change automatically during start/stop/reboot.

## URL Guard

Use one global Twitch Chat Message action for URL filtering.

It should:

- allow ordinary URLs from configured trusted roles
- delete ordinary URLs from other users
- delete URL-bearing command messages after the event has been captured

Do not run two URL Guard actions in parallel.

See [Chat URL Guard](CHAT_URL_GUARD.md).

## Queue separation

Recommended:

- watchdog/control logic on its normal queue
- `CometenIRL_EndAutoStop` on a dedicated queue

Do not let a delayed Ending helper block watchdog processing.

## Verification checklist

1. `CometenIRL_Setup` completes.
2. `!irlstatus` returns a BELABOX result.
3. volume/mute/test commands work.
4. `!irlstart` starts OBS on Starting Soon.
5. `!irlgo` selects BELABOX SRT and arms watchdog.
6. `!irlbrb`/`!irlback` work.
7. `!irlend` switches to Ending and auto-stops OBS.
8. watchdog fallback/recovery works in test mode.
9. watchdog works with `WatchdogLiveOnly=true` during a real stream.
10. Browser Audio status/on/off works and private URL commands are removed from chat.

## Related documentation

- [Commands](COMMANDS.md)
- [OBS Admin Control](OBS_ADMIN_CONTROL.md)
- [Remote Control](REMOTE_CONTROL.md)
- [Browser Audio](BROWSER_AUDIO.md)
- [Watchdog and Heartbeat](WATCHDOG_HEARTBEAT.md)
