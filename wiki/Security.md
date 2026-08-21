# Security

Cometen IRL Alerts intentionally separates public code from private runtime configuration.

## Never commit

```text
relay/config.php
receiver/config.json
```

## Never publish

- sender token
- receiver token
- database username/password
- BELABOX stream ID
- private Browser Source URL/token
- speaker MAC address if you do not want it public

## Browser Source URLs

Browser Source URLs may contain long-lived access tokens.

Rules:

- store them only in local gitignored receiver configuration
- do not paste them into GitHub issues, Discord or public chat
- do not log the complete URL
- do not echo the URL in confirmation messages
- delete URL-bearing Twitch command messages after Streamer.bot captures them
- rotate/regenerate the URL if it is exposed

## Relay tokens

Use separate sender and receiver tokens.

The web relay does not need Twitch/YouTube credentials.

## Remote control

The BELABOX receiver accepts a hardcoded set of control actions. It must not expose generic arbitrary shell execution through the public relay path.

## URL Guard

Use one global URL Guard in Streamer.bot to enforce the Twitch URL policy and delete sensitive URL-bearing commands after capture.
