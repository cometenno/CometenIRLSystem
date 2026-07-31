# Cometen IRL Alerts

A lightweight return-channel for IRL stream notifications.

The project sends events from **Streamer.bot** on the home streaming PC to a small receiver running beside **BELABOX** on a Radxa ROCK 5B+. The receiver plays short local alert sounds through the configured audio output, with Bluetooth speaker support planned as the primary field setup.

## Architecture

```text
Twitch / YouTube event
        |
        v
Streamer.bot at home
        |
        | HTTPS POST
        v
Cometen IRL Relay
        |
        | HTTPS long polling / polling
        v
ROCK 5B+ receiver beside BELABOX
        |
        v
Bluetooth speaker
```

BELABOX remains responsible for video, audio capture, bonding and SRT/SRTLA transport. This project runs as a separate service and does not modify BELABOX.

## V1 goal

1. Run a test action in Streamer.bot.
2. Send a signed JSON event to the relay.
3. Receive the event on the ROCK 5B+.
4. Play a local `test.wav` sound.
5. Acknowledge the event so it is not replayed.

## Planned event types

- `test`
- `follow`
- `sub`
- `resub`
- `giftsub`
- `raid`
- `bits`
- `channelpoint`
- `moderator`
- `system`

## Repository layout

```text
streamerbot/   Streamer.bot C# sender actions
relay/         PHP and MySQL relay API
receiver/      Python receiver for the ROCK 5B+
docs/          Setup and architecture documentation
```

## Security

Never commit real API tokens or database credentials. Copy the example configuration files and keep the real files outside Git or ignored by `.gitignore`.

## Status

Initial development scaffold.
