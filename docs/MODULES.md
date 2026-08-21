# Module overview

This page explains the major Cometen IRL Alerts modules, where they run, what they are responsible for and how they fit together.

## 1. Streamer.bot alert sender

**Runs on:** streaming PC  
**Primary file:** `streamerbot/CometenIRL_Send.cs`

Purpose:

- accepts alert/event data from Streamer.bot or CometenWebAdmin
- creates a unique event ID
- sends the event to the HTTPS relay
- does not play BELABOX audio locally

Use this for normal IRL alert delivery. Do not create a second sender for the same event path, or the BELABOX may receive duplicates.

## 2. Web relay

**Runs on:** HTTPS web host  
**Directory:** `relay/`

Purpose:

- accepts sender events through `push.php`
- stores short-lived events in MySQL/MariaDB
- leases events to the BELABOX receiver through `poll.php`
- records acknowledgements
- stores remote-control results
- receives heartbeat/status from the receiver

The relay contains no Twitch or YouTube credentials. It uses separate sender and receiver tokens.

## 3. BELABOX receiver

**Runs on:** ROCK 5B+/BELABOX  
**Primary files:** `receiver/receiver.py`, `receiver/run_receiver.py`

Purpose:

- polls the relay
- plays mapped local WAV files through PipeWire
- resolves the configured audio sink dynamically
- executes the hardcoded remote-control action set
- returns command results to the relay

The receiver is installed as the user service `cometen-irl-alerts.service`.

## 4. Heartbeat

**Runs on:** ROCK 5B+/BELABOX  
**Primary files:** receiver heartbeat scripts and service

Purpose:

- reports that the receiver is alive
- provides diagnostic online/offline state
- does not decide OBS scene changes

Normal interval is 30 seconds. The relay considers the receiver offline after the configured timeout, normally 90 seconds.

## 5. Remote Control

**Runs on:** Streamer.bot + relay + BELABOX receiver  
**Primary file:** `streamerbot/CometenIRL_RemoteControl.cs`

Purpose:

- set speaker volume
- volume up/down
- mute/unmute
- request expanded IRL status
- play a test alert

Path:

```text
Twitch chat -> Streamer.bot -> HTTPS relay -> BELABOX receiver
                                      ^              |
                                      +-- result -----+
```

See [Remote Control](REMOTE_CONTROL.md).

## 6. Browser Audio

**Runs on:** ROCK 5B+/BELABOX, controlled from Streamer.bot  
**Primary files:**

- `receiver/browser_audio.py`
- `receiver/configure-browser-audio.py`
- `streamerbot/CometenIRL_BrowserAudioControl.cs`

Purpose:

- open private Browser Source URLs in headless Chromium
- route web audio to the BELABOX PipeWire/Bluetooth sink
- allow multiple named sources such as `soundalerts` and `blerp`
- start, stop, restart, add or remove sources independently

Each source uses a separate Chromium process/profile so one source can be restarted without taking down the others.

See [Browser Audio](BROWSER_AUDIO.md).

## 7. OBS IRL Admin Control

**Runs on:** Streamer.bot on the streaming PC  
**Primary file:** `streamerbot/CometenIRL_AdminControl.cs`

Purpose:

- start the IRL stream on the Starting Soon scene
- switch to BELABOX SRT
- enter/leave BRB
- run Ending and automatic OBS stop
- perform emergency/manual stop
- manage the IRL watchdog armed state
- optionally switch Channel Point groups
- persist the IRL response language

This module controls OBS locally and does not go through the BELABOX relay.

See [OBS Admin Control](OBS_ADMIN_CONTROL.md).

## 8. BELABOX ingest watchdog

**Runs on:** Streamer.bot on the streaming PC  
**Primary file:** `streamerbot/IRLAlertsController.cs`

Purpose:

- reads actual BELABOX/SRT ingest telemetry
- detects signal loss
- switches OBS to `IRL - SIGNAL MISTET`
- waits for stable recovery
- returns OBS to `BELABOX SRT` or the intended return scene

This is the single automatic OBS scene authority for signal failover. Heartbeat is diagnostic only and is not a replacement for ingest telemetry.

See [Watchdog and Heartbeat](WATCHDOG_HEARTBEAT.md).

## 9. Ending helper

**Runs on:** Streamer.bot  
**Primary file:** `streamerbot/CometenIRL_EndAutoStop.cs`

Purpose:

- waits for the configured Ending duration
- validates that the Ending sequence is still current
- stops OBS only if the Ending state has not been cancelled

Run it on a separate Streamer.bot queue from the watchdog.

## 10. Status LEDs

**Runs on:** ROCK 5B+/BELABOX  
**Primary receiver status/LED module:** `receiver/status_leds.py`

Purpose:

- expose physical box state without opening a terminal
- indicate system/online state, Bluetooth/audio state, video-input state and encoder/output state

See [Status LEDs](STATUS_LEDS.md).

## 11. Twitch Chat URL Guard

**Runs on:** Streamer.bot  
**Primary code:** URL Guard action used by CometenWebAdmin/Streamer.bot

Purpose:

- allow normal URLs from configured trusted roles
- delete ordinary URLs from other users
- delete URL-bearing command messages after Streamer.bot has captured them
- protect commands such as `!sr <url>` and `!irlaudio add <name> <url>` from leaving private URLs visible in Twitch chat

See [Chat URL Guard](CHAT_URL_GUARD.md).

## 12. CometenWebAdmin integration

**Runs on:** streaming PC/web-admin integration layer  
**Primary file:** `integration/cometenwebadmin/irl-forward.js`

Purpose:

- forward selected CometenWebAdmin alert events into the same IRL sender path
- keep IRL alert delivery centralized

Do not forward the same alert through both this integration and a second Streamer.bot action.

See [CometenWebAdmin integration](../integration/cometenwebadmin/README.md).

## Responsibility summary

```text
Streaming PC
  Streamer.bot sender
  Remote-control sender
  Browser Audio chat control
  OBS admin control
  BELABOX ingest watchdog
  CometenWebAdmin integration

Web host
  HTTPS relay
  MySQL/MariaDB queue/status storage

BELABOX / ROCK 5B+
  receiver
  heartbeat
  local WAV playback
  Browser Audio Chromium processes
  PipeWire/Bluetooth routing
  status LEDs
```
