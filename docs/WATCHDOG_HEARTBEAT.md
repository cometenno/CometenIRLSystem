# Watchdog and Heartbeat

Cometen IRL System uses two different health concepts that must not be confused:

1. **receiver heartbeat** - tells you whether the ROCK 5B+/receiver is alive
2. **BELABOX ingest watchdog** - tells you whether the live video stream is actually reaching the BELABOX/SRT ingest and controls OBS fallback/recovery

Heartbeat is diagnostic. The ingest watchdog is the automatic OBS scene authority for signal loss.

## Receiver heartbeat

Path:

```text
ROCK 5B+
   |
   | HTTPS POST every ~30 seconds
   v
heartbeat.php
   |
   v
receiver status storage
   |
   v
receiver_status.php
```

Recommended values:

```text
heartbeat_interval_seconds = 30
receiver_offline_seconds = 90
```

A 1-second heartbeat interval caused HTTP 429 rate limiting on the tested web host and must not be used.

The heartbeat client includes backoff behavior for 429 responses.

## What heartbeat proves

Heartbeat can tell you:

- receiver process/system is alive enough to report
- BELABOX has network access to the relay
- receiver status is fresh/stale

Heartbeat does **not** prove:

- camera input is valid
- encoder pipeline is producing video
- SRT/SRTLA transport is healthy
- ingest is receiving usable bitrate
- OBS should stay on the BELABOX scene

## BELABOX ingest watchdog

Primary Streamer.bot controller:

```text
streamerbot/IRLAlertsController.cs
```

The production-tested core watchdog reads BELABOX Cloud/SRT ingest telemetry rather than relying only on OBS Media Source state.

Typical flow:

```text
BELABOX encoder / SRTLA
        |
        v
BELABOX ingest endpoint
        |
        | connected / bitrate / RTT / packet data
        v
IRLAlertsController
        |
        +--> signal healthy -> BELABOX SRT
        |
        +--> signal lost -> IRL - SIGNAL MISTET
        |
        +--> signal stable again -> return to BELABOX SRT / intended return scene
```

## Important globals

```text
CometenIRL_BelaboxStreamId
CometenIRL_BelaboxStatsBaseUrl
CometenIRL_FallbackScene
CometenIRL_DefaultReturnScene
CometenIRL_WatchdogLiveOnly
CometenIRL_WatchdogArmed
```

Typical scene defaults:

```text
CometenIRL_FallbackScene = IRL - SIGNAL MISTET
CometenIRL_DefaultReturnScene = BELABOX SRT
```

Never commit the BELABOX stream ID.

## Live-only gating

Offline/test mode:

```text
CometenIRL_WatchdogLiveOnly = false
```

Production mode:

```text
CometenIRL_WatchdogLiveOnly = true
```

With `true`, the watchdog may continue collecting telemetry while OBS is offline, but it must not automatically change scenes unless OBS is actually streaming.

This behavior was production-tested.

## Armed state

Separate persistent global:

```text
CometenIRL_WatchdogArmed
```

Meaning:

```text
true  -> automatic fallback/recovery is allowed
false -> telemetry may continue, but automatic scene changes are blocked
```

The IRL admin workflow uses this intentionally:

```text
Starting Soon -> disarmed
BELABOX SRT   -> armed
BRB           -> disarmed
Back to SRT   -> armed
Ending        -> disarmed
```

## Scene switching API

The tested watchdog uses:

```text
CPH.ObsSetScene()
```

for fallback and recovery.

An earlier raw OBS request path was not as reliable in the tested setup. Do not replace the tested scene-switch API without a new explicit fallback/recovery test.

## Recovery stability

Do not recover to the BELABOX scene on the first single good sample after an outage.

The controller uses a stable-good-check concept so the ingest must remain healthy for multiple checks before recovery. This prevents rapid scene flapping when the connection is unstable.

## Single automatic scene authority

Do not run NOALBS or another automatic scene switching system in parallel with `IRLAlertsController`.

Two automatic scene authorities can create:

- repeated scene bouncing
- failed recovery
- unpredictable BRB/Ending behavior
- fights between manual admin state and signal failover

The admin module coordinates with the watchdog by using the armed flag.

## Production verification

The core watchdog workflow was verified with both offline test mode and real OBS streaming:

```text
BELABOX feed healthy
-> BELABOX SRT

BELABOX feed stopped
-> ingest connected=false / bitrate=0
-> IRL - SIGNAL MISTET

BELABOX feed restored
-> ingest healthy for stable checks
-> automatic return to BELABOX SRT
```

With production live-only mode:

```text
OBS offline + BELABOX feed lost
-> no automatic scene switch

OBS streaming + BELABOX feed lost
-> IRL - SIGNAL MISTET

OBS streaming + feed restored
-> automatic recovery
```

The workflow was also exercised in BELABOX broadband-mode testing.

## Video input versus encoder state

The physical box/status code may report video-input and encoder/output as separate states.

For example:

- camera/source present while encoder stopped
- camera/source missing while encoder running
- source and encoder pipeline both healthy

Do not collapse these into one status signal.

## USB/video hardware diagnostics

Observed camera failures have included Linux UVC/USB errors such as:

```text
uvcvideo: Non-zero status (-71) in video completion handler
uvcvideo: Failed to resubmit video URB
USB disconnect
GStreamer/v4l2 source errors
```

These are input/hardware issues and are separate from relay heartbeat or SRT ingest logic.

Watch new kernel events:

```bash
sudo journalctl -kf -n 0 | grep -Ei 'uvc|usb|video|v4l2|xhci|disconnect|reset|error'
```

## Troubleshooting order

When OBS falls back unexpectedly, check in this order:

1. BELABOX ingest telemetry
2. watchdog armed/live-only state
3. OBS connection and scene names
4. BELABOX encoder state
5. camera/USB input state
6. network/SRT state
7. receiver heartbeat only as supporting diagnostics

## Compatibility note

The `IRLAlertsController` filename and the `CometenIRL_*` global prefix are retained runtime identifiers. They do not need to be renamed when the repository/project branding changes to Cometen IRL System.

## Related documentation

- [Architecture](architecture.md)
- [OBS Admin Control](OBS_ADMIN_CONTROL.md)
- [Status LEDs](STATUS_LEDS.md)
- [BELABOX Stability](BELABOX_STABILITY.md)
