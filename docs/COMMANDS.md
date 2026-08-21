# IRL chat command reference

This is the complete user-facing chat command reference for Cometen IRL System.

Permissions depend on the command group. Keep command permissions in Streamer.bot aligned with the code-level checks described below.

## Status and speaker control

Implemented by `streamerbot/CometenIRL_RemoteControl.cs`.

```text
!irlstatus
!alerttest
!volum 0-100
!vol 0-100
!vol75
!volum75
!volup
!voldown
!mute
!unmute
```

Meaning:

- `!irlstatus` - returns system, temperature, fan, Bluetooth/audio sink, video, encoder, Wi-Fi and uptime status
- `!alerttest` - plays the configured test alert on the BELABOX speaker
- `!volum 75` / `!vol 75` / `!vol75` / `!volum75` - sets speaker volume to 75%
- `!volup` - increases volume by the configured step
- `!voldown` - decreases volume by the configured step
- `!mute` - mutes the BELABOX audio sink
- `!unmute` - unmutes the BELABOX audio sink

## IRL stream/OBS admin control

Implemented by `streamerbot/CometenIRL_AdminControl.cs`.

Recommended permission: **Broadcaster only**.

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

Meaning:

- `!irlstart` - switch to Starting Soon, disarm watchdog, optionally switch Channel Points to IRL mode, start OBS streaming
- `!irlgo` - switch to BELABOX SRT and arm watchdog
- `!irlbrb` - switch to BRB and disarm watchdog
- `!irlback` - return to BELABOX SRT and arm watchdog
- `!irlend` - switch to Ending, disarm watchdog and start automatic delayed OBS stop
- `!irlstop` - immediate/manual stop and restore normal mode
- `!irlpoints on` - force IRL Channel Point group mode
- `!irlpoints off` - restore normal Channel Point group mode
- `!irllang no` - persistent Norwegian IRL responses
- `!irllang en` - persistent English IRL responses

### Scene aliases

```text
!irlscene soon
!irlscene srt
!irlscene brb
!irlscene end
!irlscene signal
```

Accepted aliases:

```text
soon     starting, start
srt      live, go
brb      pause
end      ending
signal   lost
```

## Browser Audio

Implemented by `streamerbot/CometenIRL_BrowserAudioControl.cs`.

Recommended permission: **Broadcaster + Moderators**.

```text
!irlaudio status
!irlaudio on
!irlaudio off
!irlaudio restart

!irlaudio add <name> <Browser Source URL>
!irlaudio remove <name>
!irlaudio delete <name>
!irlaudio del <name>

!irlaudio <name> status
!irlaudio <name> on
!irlaudio <name> off
!irlaudio <name> restart
```

Examples:

```text
!irlaudio status
!irlaudio add blerp https://PRIVATE_BROWSER_SOURCE_URL
!irlaudio blerp off
!irlaudio blerp on
!irlaudio remove blerp
```

`!irlaudio add` deletes the original Twitch message after Streamer.bot has captured it. The private URL must never be echoed back in the confirmation message.

### Master versus source state

`!irlaudio off` turns the Browser Audio master state off while leaving the supervisor service active so chat can turn it back on.

A status line such as:

```text
IRL Audio: OFF | service active | soundalerts ON
```

means:

- master Browser Audio is OFF
- the supervisor service is still running
- `soundalerts` is configured as an enabled source and will run again when the master is turned on

## Command modes in Streamer.bot

Recommended command matching:

```text
!irlstart   Exact
!irlgo      Exact
!irlbrb     Exact
!irlback    Exact
!irlend     Exact
!irlstop    Exact
!irlscene   Starts With
!irlpoints  Starts With
!irllang    Starts With
!irlaudio   Starts With
```

Remote-control commands may be separate exact/starts-with commands depending on the local Streamer.bot setup.

## URL safety

Commands that contain URLs should be treated as sensitive. The URL Guard should delete URL-bearing command messages after Streamer.bot has received the event. This applies to Browser Audio add commands and can also protect other systems such as `!sr <url>`.

See [Chat URL Guard](CHAT_URL_GUARD.md).
