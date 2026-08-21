# CometenWebAdmin integration

This integration forwards existing CometenWebAdmin alert events into `Cometen IRL Notifications - Send` without embedding IRL-specific code inside every Follow, Sub, Raid or Bits action.

## Purpose

The integration keeps the normal local/OBS alert path and the IRL BELABOX forwarding path separate:

```text
CometenWebAdmin alert
        |
        +--> normal local/OBS alert
        |
        +--> irl-forward.js -> Cometen IRL Notifications - Send -> relay -> BELABOX
```

Turning IRL forwarding off should stop only the BELABOX/IRL forwarding. The normal local alert should continue.

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

Canonical WebAdmin overlay file lives in the CometenWebAdmin project.

IRL forwarding script in this repository:

```text
integration/cometenwebadmin/irl-forward.js
```

The overlay and `irl-forward.js` should be in the same local Alerts directory.

The overlay loads the integration before `</body>`:

```html
<script src="irl-forward.js"></script>
```

## Streamer.bot WebSocket

Expected local WebSocket endpoint:

```text
127.0.0.1:8081
```

Required/related actions include:

```text
CWA - Alerts Status
CWA - Alerts Save Settings
CWA - Alerts Send Config
CWA - Alerts Test
Cometen IRL Notifications - Send
```

## Settings synchronization

The alert overlay should obtain current settings through the CometenWebAdmin status action and make the IRL sub-settings available to `irl-forward.js`.

A previously tested browser-state issue was caused by stale `localStorage` on one PC. The updated alert overlay uses the newer settings storage key rather than the old state key.

Do not downgrade the storage key/state model without testing both the streaming PC and any secondary machine that opens the overlay.

## Avoid duplicate IRL events

Do **not** also add a separate `Run Action` to `Cometen IRL Notifications - Send` inside every Follow/Sub/Raid/etc. action when `irl-forward.js` is already forwarding that event.

Otherwise the same alert can be sent twice to the BELABOX receiver.

## Recommended test sequence

1. IRL master ON + Follow ON -> normal alert + BELABOX/IRL audio.
2. IRL master OFF -> normal alert only.
3. IRL master ON + Follow OFF -> normal Follow alert only.
4. IRL master ON + Follow ON -> BELABOX/IRL audio returns.

Repeat with other alert types after the common path is confirmed.

## Historical verification

The integration path has previously been exercised end-to-end from a CometenWebAdmin test alert through the IRL sender, HTTPS relay and BELABOX audio receiver.

Any new settings/UI changes should still be retested after deployment.

## Related documentation

- [Project documentation](../../docs/README.md)
- [Architecture](../../docs/architecture.md)
- [Installation](../../docs/INSTALLATION.md)
- [Module overview](../../docs/MODULES.md)
