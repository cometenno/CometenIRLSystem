# Twitch Chat URL Guard

`streamerbot/Cometen_ChatUrlGuard.cs` er et generelt URL-filter for Twitch-chat.

## Regler

- Broadcaster, moderatorer og VIP-er kan poste vanlige URL-er.
- URL-er fra andre brukere slettes.
- Brukeren får en kort melding: `@bruker lenker er kun tillatt for VIP/mods.`
- En chatkommando som inneholder URL slettes uansett rolle etter at Streamer.bot har mottatt eventen.
- URL-kommandoer får ikke ekstra svar fra URL-guarden. Den egentlige kommandoen bekrefter resultatet.

Dette er med vilje slik at for eksempel:

```text
!sr https://...
!irlaudio add blerp https://...
```

kan behandles av riktig system, mens den offentlige meldingen fjernes ved hjelp av Twitch `msgId`.

## Streamer.bot

1. Lag action `Cometen - Chat URL Guard`.
2. Legg inn `Execute C# Code` med hele `streamerbot/Cometen_ChatUrlGuard.cs`.
3. Koble actionen til trigger `Twitch -> Chat -> Chat Message`.
4. Botkontoen bør være moderator slik at den kan slette meldinger.

Koden prøver sletting med botkontoen først og broadcasterkontoen som fallback.

## Viktig

URL-en logges aldri av guard-koden.

Guard-actionen trenger ikke å lese eller lagre URL-en utover det innkommende Twitch-eventet. Streamer.bot har allerede mottatt `msgId` og meldingsteksten når actionen kjører, så command-actionen kan behandle eventet selv om chatmeldingen slettes etterpå.

Praktisk test av URL-command + URL-guard skal gjennomføres før funksjonen markeres produksjonsverifisert.
