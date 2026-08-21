# Architecture

Last updated: 22 August 2026.

Cometen IRL System is the central project for the IRL alert return path, BELABOX-side audio, Browser Audio, receiver health, remote control, physical status and OBS failover/recovery.

## End-to-end alert path

```text
Twitch / YouTube / CometenWebAdmin
       |
       v
Streamer.bot on the streaming PC
       |
       | HTTPS POST + sender token
       v
PHP/MySQL relay on the web host
       |
       | HTTPS polling + receiver token
       v
Python receiver on ROCK 5B+/BELABOX
       |
       v
PipeWire -> Bluetooth / local audio
```

The receiver acknowledges delivered events. The relay uses leases and TTLs so an unacknowledged event can be delivered again after its lease expires.

## Browser Audio path

```text
Sound Alerts / Blerp / other Browser Source
       |
       +--> OBS Browser Source on the streaming PC -> stream audio
       |
       +--> headless Chromium on BELABOX
                  |
                  v
             PipeWire
                  |
                  v
          Bluetooth speaker
```

Each configured Browser Audio source runs in its own Chromium process/profile. The supervisor can start, stop or restart a single source without stopping the others.

## Remote-control path

```text
Twitch chat
   |
   v
Streamer.bot
   |
   | HTTPS control event
   v
relay
   |
   v
BELABOX receiver
   |
   +--> PipeWire / local config / Browser Audio control
   |
   +--> result -> relay -> Streamer.bot -> Twitch chat
```

The receiver accepts only a hardcoded control-action set. Arbitrary shell commands are not exposed through the relay.

## Heartbeat/status

```text
ROCK 5B+/receiver
   |
   | HTTPS POST, normally every 30 seconds
   v
heartbeat.php
   |
   v
receiver status storage
   |
   v
receiver_status.php
```

Heartbeat is diagnostic. It must not be used as the sole signal for OBS scene selection.

## BELABOX video watchdog

```text
BELABOX / SRTLA
      |
      v
BELABOX Cloud ingest telemetry
      |
      | connected / bitrate / RTT / packet state
      v
IRLAlertsController in Streamer.bot
      |
      +--> BELABOX SRT
      +--> IRL - SIGNAL MISTET
```

The watchdog uses actual ingest telemetry, not only OBS Media Source state and not receiver heartbeat alone.

## Responsibility boundaries

### ROCK 5B+/BELABOX

- camera/video input and BELABOX encoder pipeline
- SRT/SRTLA and bonding/network transport
- local alert receiver
- local WAV playback
- Browser Audio Chromium processes
- PipeWire/Bluetooth output
- heartbeat
- physical status LEDs

### Streaming PC

- Streamer.bot event sender
- remote-control sender
- Browser Audio chat control
- OBS IRL admin control
- BELABOX ingest watchdog
- OBS scene failover/recovery
- CometenWebAdmin integration

### Web host

- alert/control queue
- event lease and acknowledgement
- control results
- heartbeat/receiver status

## Single scene authority

`IRLAlertsController` is the single automatic scene authority for signal loss/recovery.

Do not run NOALBS or another automatic scene switcher in parallel. Two independent scene authorities can fight over the active OBS scene and make recovery unpredictable.

The manual/admin module may intentionally switch scenes and arm/disarm the watchdog as part of the normal IRL workflow.

## Event delivery sequence

1. Streamer.bot creates a unique event ID.
2. The sender posts the event to `push.php`.
3. The relay stores the event with a TTL.
4. The receiver obtains a lease through `poll.php`.
5. The receiver performs the requested action or plays local audio.
6. The receiver acknowledges the event.
7. Control events may also write a result that Streamer.bot polls and sends back to Twitch chat.

## Security model

- HTTPS in production
- separate sender and receiver tokens
- no Twitch/YouTube credentials on the relay
- local audio files on BELABOX
- private configuration in gitignored files
- private Browser Source URLs stored only in `receiver/config.json`
- BELABOX stream ID never committed

Never commit:

```text
relay/config.php
receiver/config.json
```

## Compatibility note

The repository/project name is Cometen IRL System. Existing runtime identifiers such as `CometenIRL_*`, `IRLAlertsController` and `cometen-irl-alerts.service` remain intentionally unchanged for compatibility with deployed Streamer.bot actions and BELABOX services.

## Related documentation

- [Installation](INSTALLATION.md)
- [Module overview](MODULES.md)
- [Commands](COMMANDS.md)
- [Browser Audio](BROWSER_AUDIO.md)
- [Watchdog and Heartbeat](WATCHDOG_HEARTBEAT.md)
