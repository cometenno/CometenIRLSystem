# Cometen IRL Remote Control

Remote Control er en del av samme CometenIRLAlerts-kjede som vanlige IRL-varsler.

```text
Twitch chat -> Streamer.bot -> HTTPS relay -> BELABOX receiver -> PipeWire/WPS200
```

## Første funksjoner

- `!vol75` - sett BELABOX-lyd til 75 %
- `!vol 75` - samme, dersom du bruker en vanlig `!vol`-kommando med input
- `!volup` - +5 %
- `!voldown` - -5 %
- `!mute` - mute
- `!unmute` - unmute

Volumkommandoene bruker `wpctl` mot `@DEFAULT_AUDIO_SINK@` som standard. Dette gjør at vi ikke hardkoder en dynamisk PipeWire sink-ID.

## Sikkerhet

Sett command permissions i Streamer.bot til **Broadcaster + Moderators**. Relayen krever fortsatt `CometenIRL_SenderToken`, og BELABOX mottar kun et hardkodet sett med tillatte control-actions. Ingen vilkårlige shell-kommandoer kan sendes gjennom denne funksjonen.

Control-events får kun 15 sekunders TTL på relayen, slik at gamle volum/mute-kommandoer ikke blir kjørt lenge etter at de ble sendt.

## Streamer.bot

Lag en action:

```text
Cometen IRL Remote Control - Send
```

Legg inn `Execute C# Code` med hele innholdet i:

```text
streamerbot/CometenIRL_RemoteControl.cs
```

Actionen bruker de samme persisted globals som alerts:

```text
CometenIRL_RelayUrl
CometenIRL_SenderToken
```

### Enkelt oppsett med faste kommandoer

For `!vol75`:

1. Lag command `!vol75`.
2. Sett permissions til Broadcaster + Moderators.
3. Før C#-actionen settes:
   - `controlAction = volume_set`
   - `controlValue = 75`
4. Kjør `Cometen IRL Remote Control - Send`.

Samme metode kan brukes for `!vol25`, `!vol50`, `!vol100` osv.

For de andre kommandoene settes bare `controlAction`:

```text
!volup    -> volume_up
!voldown  -> volume_down
!mute     -> mute
!unmute   -> unmute
```

### Dynamisk `!vol 75`

Hvis C#-actionen får `rawInput` fra en Streamer.bot command-trigger, kan den også tolke et tall fra 0 til 100 direkte. Den forstår både `75`, `!vol75` og `!vol 75`.

## BELABOX receiver

Receiveren behandler `type=control` separat fra lyd-alerts. Control-events spiller derfor ikke `test.wav`.

Standardinnstillinger er:

```json
{
  "remote_control_enabled": true,
  "remote_audio_sink": "@DEFAULT_AUDIO_SINK@",
  "remote_volume_step_percent": 5,
  "remote_volume_max_percent": 100
}
```

Disse feltene er valgfrie. Hvis de mangler i eksisterende `config.json`, brukes verdiene over automatisk.

## Test

Etter oppdatering på BELABOX:

```bash
systemctl --user restart cometen-irl-alerts.service
journalctl --user -u cometen-irl-alerts.service -f
```

Send så `!vol75` fra en konto med riktig permission. Forventet receiver-logg:

```text
Remote control: volume set to 75%
```

## Status-retur

`!irlstatus` med retur tilbake til Streamer.bot/chat er planlagt som neste steg. Det trenger en liten retur/statuskanal på relayen og er ikke aktivert i denne første Remote Control-builden.
