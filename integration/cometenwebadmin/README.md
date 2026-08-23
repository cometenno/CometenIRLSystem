# Cometen WebAdmin integration

This integration connects the public **Cometen WebAdmin 1.0** alert overlay to **CometenIRLSystem** without embedding IRL-specific logic inside every Follow, Sub, Raid or Bits action.

Official WebAdmin repository:

- https://github.com/la1ona/Cometen-WebAdmin

## Purpose

The normal local/OBS alert path and the BELABOX/IRL forwarding path remain separate:

```text
Cometen WebAdmin alert
        |
        +--> normal local/OBS alert
        |
        +--> irl-forward.js
              |
              +--> Streamer.bot WebSocket
                    |
                    +--> Cometen IRL Notifications - Send
                          |
                          +--> relay
                                |
                                +--> BELABOX
```

Turning IRL forwarding off must stop only the BELABOX/IRL forwarding. The normal local alert must continue.

## Compatibility

Integration version: **1.0.0**

Target WebAdmin release: **Cometen WebAdmin 1.0**

The canonical public WebAdmin release contains its own copy of:

```text
alerts/irl-forward.js
```

The copy in this repository must remain behavior-compatible with that public release.

## WebSocket endpoint resolution

The integration no longer hardcodes `127.0.0.1:8081`.

The WebSocket host is resolved in this order:

1. `?host=` query parameter
2. `localStorage` key `cwa_ws_host`
3. the current page hostname when served over HTTP/HTTPS
4. fallback `127.0.0.1`

The WebSocket port is resolved in this order:

1. `?port=` query parameter
2. `localStorage` key `cwa_ws_port`
3. fallback `8081`

Examples:

```text
http://<STREAMERBOT_PC_IP>:<HTTP_PORT>/.../?host=<STREAMERBOT_PC_IP>&port=8081
```

or save the host/port through the normal Cometen WebAdmin configuration flow.

## Reconnect behavior

If Streamer.bot is temporarily unavailable, outgoing IRL alert requests are queued in memory while the WebSocket reconnects. Pending requests are flushed after the socket opens again.

This avoids silently dropping an IRL forward merely because the overlay started before Streamer.bot or the WebSocket reconnected.

## Supported alert settings

The WebAdmin Alerts panel can expose a master IRL switch plus per-event forwarding switches such as:

- Follow
- Sub
- Resub
- Gifted Sub
- Gift Bomb
- Bits
- Donation / Charity
- Raid
- YouTube Sub

These settings are stored with the normal alert configuration in Streamer.bot.

## Files

Canonical public WebAdmin package:

```text
Cometen-WebAdmin release -> CometenWebAdmin/alerts/irl-forward.js
```

IRL integration reference copy in this repository:

```text
integration/cometenwebadmin/irl-forward.js
```

The alert overlay and `irl-forward.js` must be available from the same local Alerts directory when using the script include:

```html
<script src="irl-forward.js"></script>
```

## Streamer.bot actions

Required/related actions include:

```text
CWA - Alerts Status
CWA - Alerts Save Settings
CWA - Alerts Send Config
CWA - Alerts Test
Cometen IRL Notifications - Send
```

The public Cometen WebAdmin package is responsible for its own WebAdmin/alert actions. `CometenIRLSystem` remains responsible for the IRL sender, relay, receiver and BELABOX-side behavior.

## Settings synchronization

The alert overlay obtains current settings through the Cometen WebAdmin alert configuration and makes the IRL sub-settings available to `irl-forward.js` through the current `adminSettings` object.

Do not reintroduce old browser-state keys or hardcoded machine addresses. Any change to the settings/state model should be tested on both the primary streaming machine and any secondary machine that opens the overlay.

## Avoid duplicate IRL events

Do **not** also add a separate `Run Action` to `Cometen IRL Notifications - Send` inside every Follow/Sub/Raid/etc. action when `irl-forward.js` is already forwarding that event.

Otherwise the same alert can be sent twice to the BELABOX receiver.

## Recommended test sequence

1. IRL master ON + Follow ON -> normal alert + BELABOX/IRL audio.
2. IRL master OFF -> normal alert only.
3. IRL master ON + Follow OFF -> normal Follow alert only.
4. IRL master ON + Follow ON -> BELABOX/IRL audio returns.
5. Open the overlay from a second machine using a non-local Streamer.bot host and verify forwarding still reaches the correct WebSocket endpoint.
6. Restart or temporarily stop the Streamer.bot WebSocket and verify reconnect/queued forwarding recovers.

Repeat with the other alert types after the common path is confirmed.

## Ownership boundary

**Cometen WebAdmin** owns:

- WebAdmin UI
- alert configuration UI
- browser overlay behavior
- public `irl-forward.js` copy
- WebSocket host/port discovery

**CometenIRLSystem** owns:

- `Cometen IRL Notifications - Send`
- relay transport
- BELABOX receiver
- IRL audio playback
- IRL diagnostics
- IRL-specific runtime behavior

This boundary keeps the public WebAdmin usable without CometenIRLSystem while still providing an optional integration path when the IRL system is installed.

## Related documentation

- [Cometen WebAdmin](https://github.com/la1ona/Cometen-WebAdmin)
- [Project documentation](../../docs/README.md)
- [Architecture](../../docs/architecture.md)
- [Installation](../../docs/INSTALLATION.md)
- [Module overview](../../docs/MODULES.md)
