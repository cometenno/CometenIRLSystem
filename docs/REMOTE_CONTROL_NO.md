# Cometen IRL Remote Control

Remote Control er en del av samme CometenIRLAlerts-kjede som vanlige IRL-varsler.

```text
Twitch chat -> Streamer.bot -> HTTPS relay -> BELABOX receiver -> PipeWire/WPS200
                                      ^                         |
                                      |---- bekreftet resultat -|
```

## Kommandoer

- `!volum 0-100` - sett BELABOX-lyd til ønsket prosent
- `!mute` - mute
- `!unmute` - unmute
- `!irlstatus` - be BELABOX bekrefte at receiver og WPS200-lydsink er tilgjengelig

Eksempler:

```text
!volum 30
!mute
!unmute
!irlstatus
```

## Bekreftet retur til Twitch

Fra v0.4 sender BELABOX et resultat tilbake gjennom relayen etter at kontrollkommandoen faktisk er utført. Streamer.bot venter inntil ca. fem sekunder på resultatet og skriver det i Twitch-chatten.

Eksempler på svar:

```text
IRL: volum satt til 30%
IRL: WPS200 muted
IRL: WPS200 unmuted
IRL status: BELABOX online | WPS200 tilkoblet | Audio OK (node 32)
```

Hvis BELABOX ikke kvitterer innen tidsgrensen:

```text
IRL: ingen bekreftelse fra BELABOX.
```

Dette er en reell utførelsesbekreftelse fra receiveren, ikke bare en bekreftelse på at Streamer.bot sendte kommandoen.

## Relay

Returkanalen bruker:

```text
relay/control_result.php
```

Ingen ny databasetabell er nødvendig. Resultatet lagres kortvarig på den samme `control`-eventen etter at eventen er kvittert. Control-events har fortsatt kort TTL.

`control_result.php` bruker:

- receiver-token ved POST fra BELABOX
- sender-token ved GET fra Streamer.bot

## Sikkerhet

Sett command permissions i Streamer.bot til **Broadcaster + Moderators**. Relayen krever fortsatt `CometenIRL_SenderToken`, og BELABOX mottar kun et hardkodet sett med tillatte control-actions. Ingen vilkårlige shell-kommandoer kan sendes gjennom denne funksjonen.

Control-events får kun 15 sekunders TTL på relayen, slik at gamle volum/mute/status-kommandoer ikke blir kjørt lenge etter at de ble sendt.

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

C#-senderen sender kommandoen, poller `control_result.php` etter samme event-ID og bruker `CPH.SendMessage(...)` til å skrive den bekreftede responsen i Twitch-chatten.

### Dynamisk `!volum 0-100`

Lag én command:

```text
!volum
```

Commanden skal trigge `CometenIRL_RemoteControl` direkte. C#-senderen foretrekker Streamer.bot-argumentet `input0`, og faller tilbake til `rawInput`.

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

### Status

Lag en action, for eksempel:

```text
IRL - Status
```

Sett:

```text
controlAction = status
```

og kjør deretter:

```text
CometenIRL_RemoteControl
```

Koble kommandoen:

```text
!irlstatus
```

til denne actionen.

## BELABOX receiver

Receiveren behandler `type=control` separat fra lyd-alerts. Control-events spiller derfor ikke `test.wav`.

Receiveren finner WPS200 dynamisk i PipeWire ved å matche `Audio/Sink` mot navnet `WPS200`, slik at dynamisk PipeWire node-ID etter reboot ikke trenger å hardkodes.

Etter utført control-event:

1. Receiveren utfører kommandoen.
2. Eventen ACK-es.
3. Receiveren POST-er resultatet til `control_result.php`.
4. Streamer.bot henter resultatet og skriver det i Twitch-chatten.

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

Twitch-chatten skal få:

```text
IRL: volum satt til 30%
```

Test deretter:

```text
!irlstatus
```

Forventet chat-svar:

```text
IRL status: BELABOX online | WPS200 tilkoblet | Audio OK (node <ID>)
```

## Status

v0.4 retur/status er implementert og klar for praktisk test i komplett kjede.
