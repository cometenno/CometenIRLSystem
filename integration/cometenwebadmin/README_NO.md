# CometenWebAdmin-integrasjon

Denne integrasjonen sender de eksisterende CometenWebAdmin-alertene videre til `Cometen IRL Notifications - Send` uten at IRL-kode må legges inn i hver Follow-, Sub-, Raid- eller Bits-action.

## CWA v19.6 - IRL-kontroller

Alerts-fanen har egne IRL-innstillinger:

- hovedbryter for alle IRL-alerts
- Follow
- Sub
- Resub
- Gifted Sub
- Gift Bomb
- Bits
- Donation / Charity
- Raid
- YouTube Sub

Innstillingene lagres sammen med de eksisterende Alerts-innstillingene i Streamer.bot. Eldre lagrede innstillinger uten en IRL-seksjon behandles som ON for bakoverkompatibilitet.

Når hovedbryteren er OFF, stopper bare videresendingen til IRL-mottakeren. Vanlige OBS-alerts fortsetter uendret.

## Filer

Plasser disse i den lokale Alerts-mappen:

```text
alerts.html
irl-forward.js
```

`alerts.html` må laste integrasjonen rett før `</body>`:

```html
<script src="irl-forward.js"></script>
```

## Streamer.bot

Kontroller at WebSocket-serveren kjører på:

```text
127.0.0.1:8081
```

Streamer.bot-actionen må hete nøyaktig:

```text
Cometen IRL Notifications - Send
```

## Bruk

1. Åpne Alerts-fanen i CometenWebAdmin.
2. Slå `Enable IRL alerts` av eller på.
3. Velg hvilke alerttyper som skal sendes til IRL.
4. Trykk `Save IRL settings`.
5. Trykk `Refresh IRL settings` og kontroller statusfeltet.
6. Oppdater OBS Browser Source etter at `alerts.html` eller `irl-forward.js` er erstattet.

## Test

Start receiveren på Linux-enheten og kjør en alerttest fra CometenWebAdmin.

Når typen er aktiv, skal receiver-loggen vise for eksempel:

```text
Playing event ... type=follow ... sound=follow.wav
```

Når hovedbryteren eller den aktuelle typen er OFF, skal den normale OBS-alerten vises, men receiveren skal ikke motta eventen.

## Viktig

Ikke legg en ekstra `Run Action` til IRL-senderen i de enkelte alert-actionene når denne integrasjonen brukes. Det vil gi doble varsler.
