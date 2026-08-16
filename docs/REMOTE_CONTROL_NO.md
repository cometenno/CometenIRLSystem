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
- `!irlstatus` - hent status direkte fra BELABOX
- `!alerttest` - spill lokal `test.wav` på BELABOX og returner bekreftet resultat til Twitch

Eksempler:

```text
!volum 30
!mute
!unmute
!irlstatus
!alerttest
```

## Bekreftet retur til Twitch

Fra v0.4 sender BELABOX et resultat tilbake gjennom relayen etter at kontrollkommandoen faktisk er utført. Streamer.bot venter inntil ca. fem sekunder på resultatet og skriver det i Twitch-chatten.

Eksempler på svar:

```text
IRL: volum satt til 30%
IRL: WPS200 muted
IRL: WPS200 unmuted
IRL status: BELABOX online | WPS200 OK | Audio node 36 | WiFi CometenIRL_5G | Uptime 2t 14m
IRL: test-alert spilt av på WPS200
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

Control-events får kun 15 sekunders TTL på relayen, slik at gamle volum/mute/status/test-kommandoer ikke blir kjørt lenge etter at de ble sendt.

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

Statusen hentes direkte på BELABOX og inkluderer:

- BELABOX online
- WPS200 funnet som aktiv PipeWire Audio/Sink
- dynamisk PipeWire node-ID
- aktiv WiFi-tilkobling/SSID via NetworkManager
- system-uptime fra `/proc/uptime`

Hvis `nmcli` ikke kan leses, vises `WiFi ?`. Hvis ingen WiFi-enhet er tilkoblet, vises `WiFi offline`.

### Alert-test

Lag en action, for eksempel:

```text
IRL - Alert Test
```

Sett:

```text
controlAction = alert_test
```

og kjør deretter:

```text
CometenIRL_RemoteControl
```

Koble kommandoen:

```text
!alerttest
```

til denne actionen.

BELABOX sjekker først at WPS200 finnes som PipeWire Audio/Sink, spiller deretter lokal `test.wav` via den samme avspillingsfunksjonen som vanlige alerts, og returnerer først suksess når avspillingskommandoen er fullført uten feil.

Forventet Twitch-svar:

```text
IRL: test-alert spilt av på WPS200
```

## BELABOX receiver

Receiveren behandler `type=control` separat fra lyd-alerts. Vanlige control-events spiller ikke lyd. `alert_test` er det eksplisitte unntaket og bruker lokal testlyd.

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
  "remote_volume_max_percent": 100,
  "remote_test_sound": "test.wav"
}
```

Disse feltene er valgfrie. Hvis de mangler i eksisterende `config.json`, brukes standardverdiene automatisk.

## Test

På BELABOX:

```bash
sudo journalctl _SYSTEMD_USER_UNIT=cometen-irl-alerts.service -f
```

Send:

```text
!volum 30
!irlstatus
!alerttest
```

Forventede svar i Twitch:

```text
IRL: volum satt til 30%
IRL status: BELABOX online | WPS200 OK | Audio node <ID> | WiFi <SSID> | Uptime <tid>
IRL: test-alert spilt av på WPS200
```

## Status

v0.4.2 legger til bekreftet `!alerttest`. Status, volum, mute/unmute og returkanalen er ellers uendret.

### Verifisert 16. august 2026

Følgende ble testet ende-til-ende fra Twitch-chat via Streamer.bot og relay til BELABOX/WPS200, med bekreftet svar tilbake i Twitch-chat:

- `!irlstatus` - statusretur fungerer, inkludert korrekt `WiFi offline` når ruteren/nettet er nede
- `!alerttest` - lokal testlyd spilles av på WPS200 og bekreftes i chat
- `!mute` - WPS200 blir muted og bekreftes i chat
- `!unmute` - WPS200 blir unmuted og bekreftes i chat
- `!volum` / dynamisk volumkommando - ønsket volum settes på WPS200 og bekreftes i chat

Remote-control-delen regnes dermed som **fullt praktisk verifisert**.
