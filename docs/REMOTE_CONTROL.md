# Remote Control

Remote Control uses the same HTTPS relay/receiver path as normal IRL alerts:

```text
Twitch chat -> Streamer.bot -> HTTPS relay -> BELABOX receiver
                                      ^              |
                                      +-- result -----+
```

The receiver performs the command and returns a confirmed result to Twitch chat through the relay.

## Streamer.bot action

Action:

```text
CometenIRL_RemoteControl
```

Code:

```text
streamerbot/CometenIRL_RemoteControl.cs
```

Required persistent globals:

```text
CometenIRL_RelayUrl
CometenIRL_SenderToken
```

## Commands

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

### Status

```text
!irlstatus
```

Returns the expanded receiver status, typically including:

- system state
- temperature
- fan state
- matched PipeWire/Bluetooth sink
- video state
- encoder state
- Wi-Fi state
- uptime

Example shape:

```text
IRL: SYS OK | 51C Fan0/4 | soundcore Select 4 Go OK n33 | VIDEO ... | ENC ... | WiFi ... | Up ...
```

PipeWire node IDs are dynamic and are only diagnostic.

### Volume

Set absolute volume:

```text
!volum 75
!vol 75
!vol75
!volum75
```

Increase/decrease:

```text
!volup
!voldown
```

### Mute

```text
!mute
!unmute
```

### Test alert

```text
!alerttest
```

Plays the configured receiver test sound through the BELABOX audio path.

## Result handling

Remote Control creates a unique control event ID, sends the event to `push.php`, then polls `control_result.php` for the receiver result.

Examples:

```text
IRL: volume set to 30%
IRL: soundcore Select 4 Go muted
IRL: test alert played on soundcore Select 4 Go
```

If BELABOX does not confirm in time:

```text
IRL: no confirmation from BELABOX.
```

The exact text may be localized according to the persistent IRL language setting.

## Security

The receiver accepts only a hardcoded control-action set. Remote Control does not expose arbitrary shell execution.

Tokens remain in Streamer.bot globals and the receiver private configuration; they must not be committed.

## Browser Audio control

Browser Audio uses a separate Streamer.bot action but the same relay/result architecture:

```text
CometenIRL_BrowserAudioControl
```

See [Browser Audio](BROWSER_AUDIO.md).

## Verification status

The core remote-control path, including volume, mute/unmute, `!irlstatus` and `!alerttest`, has been tested end-to-end on the production-style BELABOX setup.

## Related documentation

- [Commands](COMMANDS.md)
- [Receiver setup](receiver-setup.md)
- [Browser Audio](BROWSER_AUDIO.md)
