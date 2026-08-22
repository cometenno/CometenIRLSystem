# Cometen IRL System - Documentation

This is the canonical documentation index for Cometen IRL System.

> **Unofficial project:** Cometen IRL System is not an official BELABOX project and is not developed, maintained, endorsed or supported by BELABOX. It is an independent personal add-on/modification project created by Cometen for his own BELABOX-based IRL streaming setup.

The project combines the IRL alert return channel, BELABOX-side audio, Browser Source audio, heartbeat/status, Twitch remote control, OBS admin control, automatic SRT failover/recovery, status LEDs and CometenWebAdmin integration into one coordinated system.

## Start here

- [Installation](INSTALLATION.md) - complete first-time setup and update workflow
- [Architecture](architecture.md) - end-to-end data flow and responsibility boundaries
- [Module overview](MODULES.md) - what each module does, where it runs and when to use it
- [Commands](COMMANDS.md) - complete IRL chat command reference
- [Streamer.bot setup](streamerbot-setup.md) - actions, triggers, globals and permissions
- [Receiver setup](receiver-setup.md) - ROCK 5B+/BELABOX receiver and local audio
- [Relay setup](relay-setup.md) - PHP/MySQL HTTPS relay on the web host

## Feature guides

- [Browser Audio](BROWSER_AUDIO.md) - Sound Alerts, multiple Browser Sources and Bluetooth playback
- [Remote Control](REMOTE_CONTROL.md) - volume, mute, status, test alert and BELABOX control path
- [OBS Admin Control](OBS_ADMIN_CONTROL.md) - IRL start/go/BRB/back/end/stop, scenes, rewards and language
- [Watchdog and Heartbeat](WATCHDOG_HEARTBEAT.md) - BELABOX ingest telemetry, OBS failover/recovery and receiver health
- [Status LEDs](STATUS_LEDS.md) - physical status indicators on the BELABOX enclosure
- [Chat URL Guard](CHAT_URL_GUARD.md) - Twitch URL policy and deletion of URL-bearing commands
- [BELABOX Headless Setup](BELABOX_HEADLESS.md) - PipeWire, Bluetooth and headless ROCK 5B+ notes
- [BELABOX Stability Notes](BELABOX_STABILITY.md) - known stability findings and diagnostics
- [Field Test Log](FIELD_TEST_LOG.md) - real-world incidents with confirmed causes and operational lessons
- [CometenWebAdmin integration](../integration/cometenwebadmin/README.md) - forwarding alerts from the web admin system

## Repository rename and compatibility

The repository was renamed from `CometenIRLAlerts` to `CometenIRLSystem` on 22 August 2026.

Fresh clones should use:

```text
https://github.com/la1ona/CometenIRLSystem
```

Existing installations may still use a local checkout directory named `~/CometenIRLAlerts`. That local path can remain in place to avoid breaking installed service paths. Runtime identifiers such as `CometenIRL_*` globals and `cometen-irl-alerts.service` are also intentionally retained for compatibility unless a separate migration is performed.

## Security rules

Never commit or publish:

- `relay/config.php`
- `receiver/config.json`
- sender or receiver tokens
- database passwords
- BELABOX stream IDs
- private Browser Source URLs or tokens

Private Browser Source URLs are intentionally stored only in the local, gitignored receiver configuration.

## Design rule

IRL-specific functionality should stay coordinated inside this project. Do not add a second automatic OBS scene switcher such as NOALBS in parallel with `IRLAlertsController`, because competing scene authorities can fight over the active OBS scene.

## Verification status

Production-tested functionality includes the HTTPS relay/receiver path, local PipeWire/Bluetooth audio, heartbeat, status/volume/mute/test control, Sound Alerts Browser Audio on BELABOX, simultaneous Sound Alerts playback in OBS and on the BELABOX Bluetooth speaker, and the core BELABOX ingest failover/recovery workflow.

Browser Audio multi-source management is implemented. Chat-based add/status/source on/off has been exercised on the live setup; independent playback from a second provider should still be verified before calling that provider production-tested.
