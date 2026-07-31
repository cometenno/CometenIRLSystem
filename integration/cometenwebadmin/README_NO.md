# CometenWebAdmin-integrasjon

Denne integrasjonen sender de eksisterende CometenWebAdmin-alertene videre til `Cometen IRL Notifications - Send` uten at IRL-kode må legges inn i hver Follow-, Sub-, Raid- eller Bits-action.

## Installasjon

1. Kopier `irl-forward.js` til samme mappe som CometenWebAdmin sin `alerts.html`.
2. Åpne `alerts.html` i en teksteditor.
3. Legg denne linjen rett før `</body>`:

```html
<script src="irl-forward.js"></script>
```

4. Lagre filen og oppdater OBS Browser Source.
5. Kontroller at Streamer.bot WebSocket kjører på `127.0.0.1:8081`.
6. Kontroller at Streamer.bot-actionen heter nøyaktig:

```text
Cometen IRL Notifications - Send
```

## Alerttyper

Integrasjonen videresender:

- Follow
- Sub
- Resub
- Gifted Sub
- Gift Bomb / Community Gift
- Bits / Cheer
- Donation / Charity
- Raid
- YouTube Sub

## Test

Start receiveren på Linux-enheten, og kjør en alerttest fra CometenWebAdmin. Receiver-loggen skal vise riktig type og WAV-fil, for eksempel:

```text
Playing event ... type=follow ... sound=follow.wav
```

## Viktig

Ikke legg en ekstra `Run Action` til IRL-senderen i de enkelte alert-actionene når denne integrasjonen brukes. Det vil gi doble varsler.
