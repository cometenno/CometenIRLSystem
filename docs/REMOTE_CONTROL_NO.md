# Cometen IRL Remote Control

Remote Control er en del av samme CometenIRLAlerts-kjede som vanlige IRL-varsler.

```text
Twitch chat -> Streamer.bot -> HTTPS relay -> BELABOX receiver -> PipeWire/WPS200
```

## Kommandoer

- `!volum 0-100` - sett BELABOX-lyd til ønsket prosent
- `!mute` - mute
- `!unmute` - unmute

Eksempler:

```text
!volum 25
!volum 50
!volum 75
!volum 100
```

## Sikkerhet

Sett command permissions i Streamer.bot til **Broadcaster + Moderators**. Relayen krever fortsatt `CometenIRL_SenderToken`, og BELABOX mottar kun et hardkodet sett med tillatte control-actions. Ingen vilkårlige shell-kommandoer kan sendes gjennom denne funksjonen.

Control-events får kun 15 sekunders TTL på relayen, slik at gamle volum/mute-kommandoer ikke blir kjørt lenge etter at de ble sendt.

## Streamer.bot

Lag/bruk actionen:

```text
CometenIRL_RemoteControl
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

### Dynamisk `!volum 0-100`

Lag én command:

```text
!volum
```

Commanden skal trigge `CometenIRL_RemoteControl` direkte. C#-senderen foretrekker Streamer.bot-argumentet `input0`, og faller tilbake til `rawInput`. Den godtar både bare tallet og full kommandoform dersom Streamer.bot leverer det slik.

Gyldige former:

```text
30
!vol30
!vol 30
!volum30
!volum 30
```

Verdier under 0 eller over 100 avvises.

### Mute / unmute

For `!mute`:

```text
controlAction = mute
```

For `!unmute`:

```text
controlAction = unmute
```

Begge kjører samme `CometenIRL_RemoteControl`-sender.

## BELABOX receiver

Receiveren behandler `type=control` separat fra lyd-alerts. Control-events spiller derfor ikke `test.wav`.

Receiveren finner WPS200 dynamisk i PipeWire ved å matche `Audio/Sink` mot navnet `WPS200`, slik at dynamisk PipeWire node-ID etter reboot ikke trenger å hardkodes.

Standardinnstillinger er:

```json
{
  "remote_control_enabled": true,
  "remote_audio_sink": "auto",
  "remote_audio_sink_match": "WPS200",
  "remote_volume_step_percent": 5,
  "remote_volume_max_percent": 100
}
```

Disse feltene er valgfrie. Hvis de mangler i eksisterende `config.json`, brukes standardverdiene automatisk.

## Test

På BELABOX:

```bash
sudo journalctl _SYSTEMD_USER_UNIT=cometen-irl-alerts.service -f
```

Send så:

```text
!volum 30
```

Forventet receiver-logg:

```text
Remote control: resolved audio sink WPS200 as PipeWire node <ID>
Remote control: volume set to 30%
```

`!mute` og `!unmute` skal tilsvarende logge `muted` og `unmuted`.

## Status-retur

Retur/status tilbake til Streamer.bot/chat er ikke aktivert i denne Remote Control-builden ennå.
