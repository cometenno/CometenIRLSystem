# CometenWebAdmin-integrasjon

Denne integrasjonen sender eksisterende CometenWebAdmin-alerts videre til `Cometen IRL Notifications - Send` uten at IRL-kode legges inn i hver enkelt Follow-, Sub-, Raid- eller Bits-action.

## CWA v19.9 - IRL settings sync

Alerts-fanen har:

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

Innstillingene lagres sammen med de vanlige Alerts-innstillingene i Streamer.bot. Når hovedbryteren eller én alerttype er OFF, skal bare IRL-videresendingen stoppe. Den normale OBS-alerten fortsetter.

## Bekreftet 1. august 2026

Denne kjeden virket:

```text
Follow-test fra CometenWebAdmin
-> alerts.html
-> irl-forward.js
-> Cometen IRL Notifications - Send
-> HTTPS-relay
-> receiver.py på Raspberry Pi 5
-> PipeWire / Bluetooth-lydutgang
```

Manuell IRL-test virket også.

## Viktig browser-state-fiks

Streaming-PC-en hadde gammel `localStorage` som blokkerte IRL-videresending, selv om de samme filene virket på gaming-PC-en.

Den oppdaterte `alerts.html` bruker:

```text
universal_alert_webadmin_v2_settings
```

Ikke endre den tilbake til v1.

## Settings-synkronisering

Den oppdaterte overlayfila:

- henter innstillinger via `CWA - Alerts Status`
- godtar meldingen `ALERTS_SETTINGS`
- lagrer mottatte innstillinger i v2-state
- gjør `adminSettings.irl` tilgjengelig for `irl-forward.js`

Av/på-fiksen er lagret i prosjektene, men trenger siste praktiske test etter utrulling.

## Filer

Den kanoniske overlayfila ligger i:

```text
la1ona/cometenWebAdmin/alerts/alerts.html
```

IRL-scriptet ligger i dette prosjektet:

```text
integration/cometenwebadmin/irl-forward.js
```

Kopier begge til samme lokale Alerts-mappe:

```text
alerts.html
irl-forward.js
```

`alerts.html` laster integrasjonen rett før `</body>`:

```html
<script src="irl-forward.js"></script>
```

## Streamer.bot

WebSocket-server:

```text
127.0.0.1:8081
```

Nødvendige actions:

```text
CWA - Alerts Status
CWA - Alerts Save Settings
CWA - Alerts Send Config
CWA - Alerts Test
Cometen IRL Notifications - Send
```

## Testrekkefølge

1. IRL ON og Follow ON - lokal alert og lyd på receiver.
2. IRL OFF - bare lokal alert.
3. IRL ON og Follow OFF - bare lokal Follow-alert.
4. IRL ON og Follow ON - receiverlyden skal komme tilbake.

Ikke legg ekstra `Run Action` til IRL-senderen i hver alert-action. Det kan gi doble varsler.
