# Arkitektur

Cometen IRL Alerts er en separat returkanal ved siden av BELABOX.

```text
Twitch / YouTube
       |
       v
Streamer.bot på streaming-PC
       |
       | HTTPS POST + sender-token
       v
PHP/MySQL relay på webhotellet
       |
       | HTTPS polling + receiver-token
       v
Python receiver på ROCK 5B+
       |
       v
Bluetooth-høyttaler eller annen lydutgang
```

## Avgrensning

BELABOX skal fortsatt bare håndtere:

- HDMI-video fra GoPro
- lydinngang fra DJI Mic
- videokoding
- nettverksbonding
- SRT/SRTLA til relay og OBS

Varslingssystemet kjører som en separat Linux-tjeneste og endrer ikke BELABOX-koden.

## Levering av hendelser

1. Streamer.bot lager en unik hendelses-ID.
2. Hendelsen sendes til `push.php`.
3. Relayen lagrer hendelsen med utløpstid.
4. Receiveren henter en kort lease via `poll.php`.
5. Receiveren spiller en lokal WAV-fil.
6. Receiveren kvitterer hendelsen via `acknowledge.php`.
7. En hendelse uten kvittering kan leveres på nytt når leasen utløper.

## Sikkerhet

- Kun HTTPS skal brukes i produksjon.
- Sender og receiver bruker forskjellige tokens.
- Ingen Twitch-, YouTube- eller Streamer.bot-token lagres på relayen.
- Lydfiler ligger lokalt på ROCK 5B+.
- Filnavn valideres og kan ikke inneholde katalogstier.
- Hendelser utløper automatisk og skal ikke brukes som permanent logg.

## V1

V1 bruker kort polling. Dette er enkelt å drifte på vanlig PHP-webhotell. WebSocket eller Server-Sent Events kan vurderes senere dersom webhotellet støtter det stabilt.
