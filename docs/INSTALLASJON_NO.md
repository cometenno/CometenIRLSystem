# Installasjon - Cometen IRL Alerts

Denne guiden beskriver komplett installasjon av Cometen IRL Alerts:

```text
Streamer.bot på streaming-PC
        |
        | HTTPS
        v
PHP/MySQL-relay på webhotell
        |
        | HTTPS polling
        v
Linux-enhet ved IRL-riggen
        |
        v
Bluetooth-høyttaler via PipeWire
```

BELABOX brukes fortsatt bare til video, lydopptak, bonding og SRT/SRTLA. Alertsystemet kjører separat og endrer ikke BELABOX.

## Bekreftet funksjon

Følgende kjede er testet og bekreftet:

- Streamer.bot sender event til relay
- Relay lagrer event i MySQL
- Linux-receiver henter og kvitterer eventen
- Riktig lokal WAV-fil spilles
- PipeWire keepalive holder Bluetooth-høyttaleren våken
- Follow, Sub, Resub, Gifted Sub, Gift Bomb, Bits, Donation, Raid og YouTube Sub støttes

Baseline bekreftet 31. juli 2026.

---

# 1. Krav

## Streaming-PC

- Windows
- Streamer.bot
- Internett
- CometenWebAdmin dersom sentral alertintegrasjon skal brukes

## Webhotell

- HTTPS
- PHP 8 eller nyere
- MySQL eller MariaDB
- Tilgang til filopplasting og databaseverktøy, for eksempel FTP og phpMyAdmin

## Linux-mottaker

Eksempel:

- Raspberry Pi 5
- Radxa ROCK 5B+
- Annen Debian- eller Ubuntu-basert SBC

Må ha:

- Python 3
- Git
- PipeWire
- `pw-play`
- `pw-cat`
- WirePlumber
- Bluetooth

Kontroller kommandoene:

```bash
python3 --version
git --version
pw-play --version
pw-cat --version
wpctl --version
bluetoothctl --version
```

På Debian, Ubuntu og Raspberry Pi OS kan nødvendige pakker vanligvis installeres slik:

```bash
sudo apt update
sudo apt install -y git python3 pipewire-bin wireplumber bluez
```

Pakkenavn kan variere mellom Linux-distribusjoner.

---

# 2. Klon repoet på Linux-enheten

Med SSH:

```bash
cd ~
git clone git@github.com:la1ona/CometenIRLAlerts.git
cd CometenIRLAlerts
```

Med HTTPS:

```bash
cd ~
git clone https://github.com/la1ona/CometenIRLAlerts.git
cd CometenIRLAlerts
```

Repoet er privat, så GitHub-tilgangen må være konfigurert på enheten.

Ved senere oppdateringer:

```bash
cd ~/CometenIRLAlerts
git pull
```

---

# 3. Opprett databasen

Opprett en MySQL- eller MariaDB-database på webhotellet.

Importer:

```text
relay/database.sql
```

SQL-filen oppretter tabellen:

```text
irl_alert_events
```

Med kommandolinje kan importen gjøres slik:

```bash
mysql -u DATABASEBRUKER -p DATABASENAVN < relay/database.sql
```

På vanlig webhotell brukes normalt phpMyAdmin:

1. Velg riktig database.
2. Åpne `Importer`.
3. Velg `database.sql`.
4. Start importen.

---

# 4. Lag sender- og mottakertoken

Lag to forskjellige, lange tilfeldige token.

På Linux:

```bash
python3 -c "import secrets; print(secrets.token_urlsafe(48))"
python3 -c "import secrets; print(secrets.token_urlsafe(48))"
```

Det første brukes som sender-token. Det andre brukes som receiver-token.

Tokenene skal aldri:

- legges i GitHub
- vises i skjermbilder
- sendes i Discord eller chat
- brukes som samme verdi

Ved eksponering må tokenet byttes både i relay og klienten som bruker det.

---

# 5. Installer relay på webhotellet

Last opp innholdet i `relay/` til en egen HTTPS-mappe på webhotellet.

Eksempel:

```text
https://dittdomene.no/CometenIRLAlerts_Relay
```

Relay-mappen skal inneholde minst:

```text
acknowledge.php
bootstrap.php
config.php
health.php
poll.php
push.php
```

Kopier:

```text
config.example.php
```

til:

```text
config.php
```

Fyll inn databaseinformasjon og token lokalt på webhotellet:

```php
<?php

declare(strict_types=1);

return [
    'database' => [
        'dsn' => 'mysql:host=localhost;dbname=DIN_DATABASE;charset=utf8mb4',
        'username' => 'DIN_DATABASEBRUKER',
        'password' => 'DITT_DATABASEPASSORD',
    ],

    'sender_token' => 'DITT_LANGE_SENDER_TOKEN',
    'receiver_token' => 'DITT_LANGE_RECEIVER_TOKEN',

    'event_ttl_seconds' => 90,
    'lease_seconds' => 30,
];
```

Ikke last opp `config.php` til GitHub.

## Test relay

Åpne:

```text
https://dittdomene.no/CometenIRLAlerts_Relay/health.php
```

Relay skal returnere JSON som viser at tjenesten er klar.

Ved databasefeil må DSN, databasenavn, brukernavn, passord og tabellen `irl_alert_events` kontrolleres.

---

# 6. Installer senderen i Streamer.bot

## Globale variabler

Opprett disse som persistente globale variabler i Streamer.bot:

```text
CometenIRL_RelayUrl
CometenIRL_SenderToken
```

Eksempelverdi for relay-URL:

```text
https://dittdomene.no/CometenIRLAlerts_Relay
```

Ikke legg til `/push.php` i globalvariabelen. Senderkoden legger til endepunktet selv.

`CometenIRL_SenderToken` skal inneholde sender-tokenet fra `relay/config.php`.

## Sender-action

Opprett en action med nøyaktig navn:

```text
Cometen IRL Notifications - Send
```

Legg til en C#-sub-action, og lim inn hele innholdet fra:

```text
streamerbot/CometenIRL_Send.cs
```

Trykk `Compile` og kontroller at koden kompilerer uten feil.

## Enkel test-action

Opprett midlertidig en test-action med:

```text
Set Argument
eventType = follow
```

```text
Set Argument
userName = CometenTest
```

```text
Run Action
Cometen IRL Notifications - Send
```

Denne testen skal senere gi:

```text
Playing event ... type=follow user=CometenTest sound=follow.wav
```

---

# 7. Sentral CometenWebAdmin-integrasjon

Standardalertene kan videresendes sentralt uten å legge IRL-sub-actions inn i hver Follow-, Sub-, Raid- og Bits-action.

Filen ligger her:

```text
integration/cometenwebadmin/irl-forward.js
```

Kopier filen til samme mappe som CometenWebAdmin sin `alerts.html`.

Legg deretter denne linjen rett før `</body>` i `alerts.html`:

```html
<script src="irl-forward.js"></script>
```

Oppdater OBS Browser Source etter lagring.

Integrasjonen krever:

- Streamer.bot WebSocket på `127.0.0.1:8081`
- action med nøyaktig navn `Cometen IRL Notifications - Send`
- senderens to globale variabler

Full integrasjonsbeskrivelse:

```text
integration/cometenwebadmin/README_NO.md
```

## Viktig om doble alerts

Når `irl-forward.js` brukes, skal IRL-senderen ikke også kjøres inne i hver enkelt alert-action. Begge deler samtidig gir doble IRL-varsler.

---

# 8. Koble til Bluetooth-høyttaleren

Start Bluetooth-verktøyet:

```bash
bluetoothctl
```

Kjør deretter:

```text
power on
agent on
default-agent
scan on
```

Finn MAC-adressen til høyttaleren og kjør:

```text
pair XX:XX:XX:XX:XX:XX
trust XX:XX:XX:XX:XX:XX
connect XX:XX:XX:XX:XX:XX
quit
```

Kontroller PipeWire:

```bash
wpctl status
```

Finn høyttaleren under `Sinks` og sett den som standard:

```bash
wpctl set-default SINK_ID
```

Eksempel:

```bash
wpctl set-default 65
```

Sink-ID kan endre seg etter omstart og må kontrolleres med `wpctl status`.

---

# 9. Konfigurer receiveren

Gå til receiver-mappen:

```bash
cd ~/CometenIRLAlerts/receiver
```

Kopier eksempelkonfigurasjonen:

```bash
cp config.example.json config.json
```

Åpne filen:

```bash
nano config.json
```

Eksempel:

```json
{
  "relay_base_url": "https://dittdomene.no/CometenIRLAlerts_Relay",
  "receiver_token": "DITT_LANGE_RECEIVER_TOKEN",
  "poll_interval_seconds": 0.75,
  "request_timeout_seconds": 10,
  "batch_size": 5,
  "sounds_directory": "sounds",
  "default_sound": "test.wav",
  "sound_map": {
    "test": "test.wav",
    "follow": "follow.wav",
    "sub": "sub.wav",
    "resub": "resub.wav",
    "giftsub": "gifted.wav",
    "gifted": "gifted.wav",
    "giftbomb": "giftbomb.wav",
    "raid": "raid.wav",
    "bits": "bits.wav",
    "donation": "donation.wav",
    "charity": "donation.wav",
    "youtubesub": "sub.wav",
    "yt_sub": "sub.wav",
    "channelpoint": "test.wav",
    "moderator": "test.wav",
    "system": "test.wav"
  },
  "audio_player": "pw-play {file}",
  "audio_player_timeout_seconds": 30,
  "audio_keepalive_enabled": true,
  "audio_keepalive_command": "pw-cat --playback --rate=48000 --channels=2 --format=s16 -",
  "audio_keepalive_input": "/dev/zero",
  "audio_keepalive_restart_seconds": 5
}
```

Kontroller at JSON-filen er gyldig:

```bash
python3 -m json.tool config.json >/dev/null && echo "config.json OK"
```

## Viktig om `pw-cat`

Ikke bruk `--raw` i keepalive-kommandoen. Enkelte PipeWire-versjoner støtter ikke dette valget og stopper med:

```text
pw-cat: unrecognized option '--raw'
```

Korrekt kompatibel kommando er:

```text
pw-cat --playback --rate=48000 --channels=2 --format=s16 -
```

Receiveren mater stille PCM-data fra `/dev/zero` inn gjennom standard input.

---

# 10. Installer lydfiler

Legg WAV-filene i:

```text
receiver/sounds/
```

Anbefalt format:

- WAV
- PCM
- 16-bit
- stereo
- 44,1 eller 48 kHz

Standardnavn:

```text
bits.wav
donation.wav
follow.wav
giftbomb.wav
gifted.wav
raid.wav
resub.wav
sub.wav
test.wav
```

Kontroller filene:

```bash
cd ~/CometenIRLAlerts/receiver
ls -lh sounds/*.wav
```

Test en lyd direkte:

```bash
pw-play sounds/follow.wav
```

Lyden skal komme fra Bluetooth-høyttaleren.

---

# 11. Start receiveren manuelt

```bash
cd ~/CometenIRLAlerts/receiver
python3 receiver.py config.json
```

Forventet oppstart:

```text
Cometen IRL Alert Receiver started
Relay: https://dittdomene.no/CometenIRLAlerts_Relay
Audio keepalive started through PipeWire
```

Keepalive-prosessen skal bli stående. Det skal ikke komme en ny restartlinje hvert femte sekund.

Kjør deretter Follow-testen fra Streamer.bot.

Forventet receiver-logg:

```text
Playing event ... type=follow user=CometenTest sound=follow.wav
```

Stopp receiveren med:

```text
Ctrl+C
```

---

# 12. Installer autostart

Bluetooth- og PipeWire-lyd kjører i brukerens lydsesjon. Bruk derfor user-systemd-tjenesten, ikke den eldre systemtjenesten i `receiver/install.sh`.

Kjør:

```bash
cd ~/CometenIRLAlerts/receiver
bash install-user-service.sh
```

Tillat at brukertjenesten starter uten interaktiv innlogging:

```bash
sudo loginctl enable-linger "$USER"
```

Kontroller status:

```bash
systemctl --user status cometen-irl-alerts.service
```

Følg loggen:

```bash
journalctl --user -u cometen-irl-alerts.service -f
```

Start på nytt:

```bash
systemctl --user restart cometen-irl-alerts.service
```

Stopp:

```bash
systemctl --user stop cometen-irl-alerts.service
```

Aktiver:

```bash
systemctl --user enable --now cometen-irl-alerts.service
```

Deaktiver:

```bash
systemctl --user disable --now cometen-irl-alerts.service
```

## Bluetooth etter omstart

Høyttaleren må være paret og `trusted`. Dersom den slås på etter at receiveren allerede har startet:

1. Kontroller tilkoblingen med `bluetoothctl`.
2. Kontroller standard sink med `wpctl status`.
3. Start receiver-tjenesten på nytt.

```bash
systemctl --user restart cometen-irl-alerts.service
```

---

# 13. Event- og lydmapping

| CometenWebAdmin-alert | Relay-type | Lokal lyd |
|---|---|---|
| Follow | `follow` | `follow.wav` |
| Sub | `sub` | `sub.wav` |
| Resub | `resub` | `resub.wav` |
| Gifted Sub | `gifted` | `gifted.wav` |
| Gift Bomb | `giftbomb` | `giftbomb.wav` |
| Bits / Cheer | `bits` | `bits.wav` |
| Donation / Charity | `donation` | `donation.wav` |
| Raid | `raid` | `raid.wav` |
| YouTube Sub | `youtubesub` | `sub.wav` |
| Test | `test` | `test.wav` |

Nye eventtyper kan legges til senere i:

- `relay/bootstrap.php`
- `streamerbot/CometenIRL_Send.cs`
- `receiver/config.json`
- CometenWebAdmin-integrasjonen

---

# 14. Feilsøking

## Receiveren får HTTP 401

Årsak:

- feil receiver-token
- sender-token og receiver-token er byttet om
- tokenet i `config.json` er ikke likt receiver-tokenet i relayens `config.php`

Kontroller uten å vise tokenet i skjermbilder.

## Streamer.bot får HTTP 401

Kontroller:

- `CometenIRL_SenderToken`
- sender-tokenet i `relay/config.php`
- at variabelen er persistent

## Relay returnerer databasefeil

Kontroller:

- databasevert
- databasenavn
- databasebruker
- databasepassord
- at `database.sql` er importert
- at PHP har PDO MySQL

## Ingen lyd

Kjør:

```bash
wpctl status
pw-play sounds/follow.wav
```

Kontroller at Bluetooth-høyttaleren er standard sink.

## Keepalive stopper med exit code 1

Kjør manuelt med verbose logging:

```bash
pw-cat -v --playback --rate=48000 --channels=2 --format=s16 - < /dev/zero
```

Ikke bruk `--raw`.

## Advarsel om `/dev/zero`

Oppdater repoet:

```bash
cd ~/CometenIRLAlerts
git pull
```

Ny receiver godtar `/dev/zero` som tegn-enhet.

## Alerten spiller `test.wav` i stedet for riktig lyd

Kontroller:

- eventtypen i Streamer.bot
- filnavnet i `sound_map`
- at WAV-filen finnes i `receiver/sounds/`
- store og små bokstaver i filnavnet

Linux skiller mellom for eksempel `Follow.wav` og `follow.wav`.

## Doble alerts

Dette skjer når både:

- sentral `irl-forward.js`-integrasjon brukes
- de enkelte alert-actionene også kjører IRL-senderen

Behold bare den sentrale integrasjonen.

## Receiveren kjører, men høyttaleren slår seg av

Kontroller at loggen viser:

```text
Audio keepalive started through PipeWire
```

Kontroller deretter at keepalive-prosessen fortsatt kjører:

```bash
pgrep -af pw-cat
```

---

# 15. Sikkerhet

- Bruk alltid HTTPS.
- Bruk forskjellige sender- og receiver-token.
- Ikke legg `relay/config.php` i Git.
- Ikke legg `receiver/config.json` i Git.
- Ikke vis token eller databasepassord i skjermbilder.
- Roter token straks dersom det er eksponert.
- Gi databasebrukeren bare tilgang til riktig database.
- Ikke åpne MySQL direkte mot internett dersom webhotellet ikke krever det.

---

# 16. Oppdatering

Oppdater Linux-receiveren:

```bash
cd ~/CometenIRLAlerts
git pull
systemctl --user restart cometen-irl-alerts.service
```

Ved endringer i relay må de oppdaterte PHP-filene lastes opp manuelt til webhotellet. Behold alltid den lokale `config.php` med de virkelige hemmelighetene.

Ved endringer i senderkoden må innholdet i `streamerbot/CometenIRL_Send.cs` kopieres inn i Streamer.bot-actionen og kompileres på nytt.
