# Cometen IRL System Wiki

Cometen IRL System is the coordinated IRL control, monitoring and return-channel system used with the Cometen BELABOX setup.

![Current Cometen BELABOX prototype](https://raw.githubusercontent.com/la1ona/CometenIRLSystem/main/docs/images/belabox-prototype-overview.jpg)

*Current Cometen BELABOX prototype. This photo will be replaced by final enclosure photos after the remaining hardware is installed.*

It covers alert delivery, BELABOX-side audio, Browser Source audio, receiver health, remote control, OBS IRL workflow, SRT failover/recovery, physical status LEDs, Twitch URL safety and CometenWebAdmin integration.

## Quick navigation

- [[Installation]]
- [[Module-Overview]]
- [[Commands]]
- [[Status-LEDs]]
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

- Documentation home: https://github.com/la1ona/CometenIRLSystem/blob/main/docs/README.md
- Installation: https://github.com/la1ona/CometenIRLSystem/blob/main/docs/INSTALLATION.md
- Architecture: https://github.com/la1ona/CometenIRLSystem/blob/main/docs/architecture.md
- Module overview: https://github.com/la1ona/CometenIRLSystem/blob/main/docs/MODULES.md
- Commands: https://github.com/la1ona/CometenIRLSystem/blob/main/docs/COMMANDS.md
- Status LEDs: https://github.com/la1ona/CometenIRLSystem/blob/main/docs/STATUS_LEDS.md

## Repository rename compatibility

The repository was renamed from `CometenIRLAlerts` to `CometenIRLSystem` on 22 August 2026. Existing runtime identifiers and installed service names are intentionally kept compatible, so names such as `CometenIRL_*` and `cometen-irl-alerts.service` may still appear throughout setup and troubleshooting documentation.

## Security

Never publish or commit sender/receiver tokens, database passwords, BELABOX stream IDs or private Browser Source URLs.
