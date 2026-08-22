# Module Overview

This page explains where each Cometen IRL System module runs and what it is used for.

## Streaming PC modules

### Streamer.bot alert sender

File: `streamerbot/CometenIRL_Send.cs`

Sends normal alert events to the HTTPS relay.

### Remote Control

File: `streamerbot/CometenIRL_RemoteControl.cs`

Used for speaker volume, mute/unmute, `!irlstatus` and test-alert control.

### Browser Audio chat control

File: `streamerbot/CometenIRL_BrowserAudioControl.cs`

Used for `!irlaudio` commands that add/remove/start/stop/restart Browser Source audio on BELABOX.

### OBS IRL Admin Control

File: `streamerbot/CometenIRL_AdminControl.cs`

Used for Starting Soon, BELABOX SRT, BRB, Ending, stop, scene aliases, Channel Point groups and persistent IRL language.

### Ending helper

File: `streamerbot/CometenIRL_EndAutoStop.cs`

Handles delayed automatic OBS stop after the Ending scene.

### BELABOX ingest watchdog

File: `streamerbot/IRLAlertsController.cs`

Reads BELABOX/SRT ingest telemetry and performs automatic OBS fallback/recovery.

### CometenWebAdmin integration

File: `integration/cometenwebadmin/irl-forward.js`

Forwards selected WebAdmin alerts into the same IRL sender path without duplicating sender logic in every alert action.

## Web-host modules

Directory: `relay/`

The relay provides:

- HTTPS event intake
- MySQL/MariaDB queue storage
- receiver polling/leasing
- acknowledgement
- remote-control results
- heartbeat/receiver status

It does not need Twitch, YouTube or OBS credentials.

## BELABOX / ROCK 5B+ modules

### Receiver

Files: `receiver/receiver.py`, `receiver/run_receiver.py`

Polls the relay, plays local audio, executes supported remote-control actions and publishes results.

### Heartbeat

Reports receiver/system health to the relay. Heartbeat is diagnostic only and is not the OBS signal-loss decision source.

### Browser Audio supervisor

File: `receiver/browser_audio.py`

Runs one headless Chromium process/profile per configured Browser Source and routes audio into PipeWire/Bluetooth.

### Status LEDs

File: `receiver/status_leds.py`

Drives the four physical indicators:

- green - BELABOX online / internet connectivity
- blue - configured Bluetooth audio device state, not one specific speaker model
- yellow - current video-input availability
- red - BELABOX encoder/output state

See [[Status-LEDs]].

### Root video probe

File: `receiver/video_signal_probe.py`

A small root system service observes the BELABOX video/encoder state and publishes a safe status file for the normal user receiver/LED service.

The probe supports both local device inputs and RTMP network input. Local device paths include `/dev/usb_capture`, `/dev/hdmirx` and `/dev/hdmi_capture`. RTMP publishers are detected separately through nginx-rtmp status / TCP port `1935`, and the active BELABOX pipeline can be identified as RTMP through `rtmpsrc`.

## Compatibility names

The repository branding is now Cometen IRL System, but existing runtime identifiers such as `CometenIRL_*`, `IRLAlertsController` and `cometen-irl-alerts.service` are retained for compatibility.

## Responsibility rule

Automatic OBS signal-loss/recovery scene changes belong to `IRLAlertsController` only. Do not run another automatic scene switcher such as NOALBS in parallel.

For the full maintained module reference, see:

https://github.com/la1ona/CometenIRLSystem/blob/main/docs/MODULES.md
