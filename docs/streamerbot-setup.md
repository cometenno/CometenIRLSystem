# Oppsett i Streamer.bot

## 1. Lag globale variabler

Opprett disse som **Persisted Global Variables**:

```text
CometenIRL_RelayUrl
CometenIRL_SenderToken
```

Eksempel på relay-URL:

```text
https://DITT-DOMENE/irl-alerts
```

Tokenet må være identisk med `sender_token` i relayens `config.php`.

## 2. Lag sender-action

Lag en action med navnet:

```text
Cometen IRL Notifications - Send
```

Legg inn en `Execute C# Code` sub-action og lim inn hele innholdet fra:

```text
streamerbot/CometenIRL_Send.cs
```

## 3. Lag test-action

Lag en action med navnet:

```text
Cometen IRL Notifications - Test
```

Legg inn disse sub-actions i rekkefølge:

1. `Set Argument` - `eventType` = `test`
2. `Set Argument` - `message` = `Cometen IRL test`
3. Kjør action `Cometen IRL Notifications - Send`

Når receiveren er på, skal `test.wav` spilles én gang.

## 4. Koble på virkelige hendelser

For hver trigger settes først `eventType`, og deretter kjøres sender-actionen.

Eksempler:

```text
Twitch Follow       -> eventType = follow
Twitch Sub          -> eventType = sub
Twitch Re-Sub       -> eventType = resub
Twitch Gift Sub     -> eventType = giftsub
Twitch Raid         -> eventType = raid
Twitch Cheer        -> eventType = bits
Channel Point       -> eventType = channelpoint
```

Senderen prøver automatisk å finne vanlige Streamer.bot-argumenter som `user`, `userName`, `displayName`, `viewerCount`, `viewers`, `bits` og `amount`.

Argumentnavn kan variere mellom triggere. Vi verifiserer og låser riktige felt per trigger når systemet testes i din Streamer.bot-versjon.

## 5. Egendefinerte meldinger

Følgende argumenter kan settes før sender-actionen:

```text
eventType
message
sound
priority
amount
```

Eksempel for moderatorvarsel:

```text
eventType = moderator
message   = Sjekk mobilen
priority  = 90
sound     = moderator.wav
```

## Feilsøking

Se Streamer.bot-loggen etter linjer som starter med:

```text
CometenIRL:
```

Ved HTTP-feil logges både statuskode og svaret fra relayen.
