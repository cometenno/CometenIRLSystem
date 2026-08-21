# Cometen IRL Alerts Wiki

Cometen IRL Alerts is the coordinated IRL control/return-channel system used with the Cometen BELABOX setup.

It covers alert delivery, BELABOX-side audio, Browser Source audio, receiver health, remote control, OBS IRL workflow, SRT failover/recovery, physical status LEDs, Twitch URL safety and CometenWebAdmin integration.

## Quick navigation

- [[Installation]]
- [[Module-Overview]]
- [[Commands]]
- [[Security]]
- [[Troubleshooting]]

## System at a glance

```text
Twitch / YouTube / CometenWebAdmin
        |
        v
Streamer.bot on streaming PC
        |
        | HTTPS
        v
PHP/MySQL relay on web host
        |
        +---------------------------+
        |                           |
        | alerts / control          | heartbeat/status
        v                           v
ROCK 5B+ receiver              receiver status
        |
        v
PipeWire / Bluetooth / local audio

Browser Sources
        |
        +--> OBS on streaming PC
        |
        +--> headless Chromium on BELABOX -> PipeWire -> Bluetooth

BELABOX/SRT ingest telemetry
        |
        v
IRLAlertsController -> OBS fallback/recovery
```

## Canonical repository documentation

The detailed maintained documentation also lives in the main repository under `docs/`:

- Documentation home: https://github.com/la1ona/CometenIRLAlerts/blob/main/docs/README.md
- Installation: https://github.com/la1ona/CometenIRLAlerts/blob/main/docs/INSTALLATION.md
- Architecture: https://github.com/la1ona/CometenIRLAlerts/blob/main/docs/architecture.md
- Module overview: https://github.com/la1ona/CometenIRLAlerts/blob/main/docs/MODULES.md
- Commands: https://github.com/la1ona/CometenIRLAlerts/blob/main/docs/COMMANDS.md

## Security

Never publish or commit sender/receiver tokens, database passwords, BELABOX stream IDs or private Browser Source URLs.
