# Cometen IRL Remote Control

Remote Control bruker samme CometenIRLAlerts-kjede som vanlige varsler:

```text
Twitch chat -> Streamer.bot -> HTTPS relay -> BELABOX receiver
                                      ^              |
                                      |-- resultat --|
```

BELABOX utfører kommandoen og returnerer et bekreftet resultat til Twitch-chat via `relay/control_result.php`.

## Vanlige kommandoer

```text
!volum 0-100
!volup
!voldown
!mute
!unmute
!irlstatus
!alerttest
```

Disse styrer PipeWire/Bluetooth som før.

## Browser Audio admin

Fra 21. august 2026 støtter samme remote-control-kjede Browser Audio via en egen Streamer.bot-action:

```text
streamerbot/CometenIRL_BrowserAudioControl.cs
```

Kommandoer:

```text
!irlaudio status
!irlaudio on
!irlaudio off
!irlaudio restart
!irlaudio add <navn> <url>
!irlaudio remove <navn>
!irlaudio <navn> on
!irlaudio <navn> off
!irlaudio <navn> restart
!irlaudio <navn> status
```

Browser Audio-kommandoene er broadcaster/mod-only i selve C#-koden. Sett også command permissions i Streamer.bot til Broadcaster + Moderators.

`!irlaudio add` sletter originalmeldingen fra Twitch etter at Streamer.bot har lest den. Bekreftelsen gjentar aldri URL-en:

```text
IRL Audio: blerp lagt til
```

Detaljert Browser Audio-oppsett:

```text
docs/BROWSER_AUDIO_NO.md
```

## Streamer.bot-oppsett - vanlig Remote Control

Bruk actionen:

```text
CometenIRL_RemoteControl
```

med hele:

```text
streamerbot/CometenIRL_RemoteControl.cs
```

Actionen bruker persisted globals:

```text
CometenIRL_RelayUrl
CometenIRL_SenderToken
```

## Streamer.bot-oppsett - Browser Audio

Lag action:

```text
CometenIRL_BrowserAudioControl
```

med hele:

```text
streamerbot/CometenIRL_BrowserAudioControl.cs
```

Lag én Twitch-command:

```text
!irlaudio
```

Bruk `Starts With` slik at hele resten av kommandoen blir tilgjengelig som `rawInput`. Koble kommandoen direkte til `CometenIRL_BrowserAudioControl`.

Actionen bruker de samme persisted globals som Remote Control og sender de nye Browser Audio-actionene gjennom samme relay og samme receiver.

## Sikkerhet

Receiveren godtar bare et hardkodet sett control-actions. Ingen vilkårlige shell-kommandoer sendes gjennom relayen.

Browser Source URL valideres som HTTP/HTTPS, kildenavn valideres strengt og det tillates maks 8 kilder. `!irlaudio add` begrenser URL-lengden slik at control-eventen passer i eksisterende relayformat. Control-events har kort TTL på maks 15 sekunder.

For generelt URL-filter i chat brukes:

```text
streamerbot/Cometen_ChatUrlGuard.cs
```

Koble den til `Twitch -> Chat -> Chat Message`. Den lar broadcaster/mod/VIP poste vanlige lenker, sletter lenker fra andre brukere, og sletter URL-baserte kommandomeldinger etter at Streamer.bot har mottatt dem.

Dette dekker også `!sr <url>` når samme URL-guard brukes på Twitch Chat Message-triggeren.

## Bekreftet retur til Twitch

BELABOX sender resultat tilbake gjennom relayen etter at kontrollkommandoen er utført. Streamer.bot poller `control_result.php` etter samme event-ID og skriver resultatet i Twitch-chatten.

Eksempler:

```text
IRL: volum satt til 30%
IRL: soundcore Select 4 Go muted
IRL Audio: ON | service active | soundalerts ON, blerp OFF
IRL Audio: blerp lagt til
IRL Audio: blerp slettet
```

Hvis BELABOX ikke kvitterer innen tidsgrensen:

```text
IRL Audio: ingen bekreftelse fra BELABOX.
```

## Status

Volum, mute/unmute, `!irlstatus` og `!alerttest` ble praktisk verifisert ende-til-ende 16. august 2026.

Browser Audio single-source ble praktisk verifisert ende-til-ende 21. august 2026. Multi-source og nye `!irlaudio` admin-kommandoer er ny kode og skal praktisk testgodkjennes før de markeres produksjonsverifisert.
