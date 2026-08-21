# IRL Browser Audio - Sound Alerts på BELABOX

Denne modulen lar ROCK 5B+/BELABOX åpne den samme Browser Source-URL-en som brukes i OBS, og spille lyden direkte ut på WPS200/Soundcore via PipeWire.

Første mål er Sound Alerts, men løsningen er generell og kan brukes med andre tjenester som leverer en vanlig Browser Source-URL.

## Arkitektur

```text
Twitch viewer
    |
    v
Sound Alerts
    |
    +--> OBS Browser Source på streaming-PC --> lyd på stream
    |
    +--> Chromium på ROCK 5B+ --> PipeWire --> WPS200/Soundcore
```

Browser Audio er en del av samme CometenIRLAlerts-prosjekt. Den bruker ikke en egen relay og laster ikke ned enkeltlyder på forhånd.

## Sikkerhet

Sound Alerts Browser Source-URL kan inneholde en unik nøkkel/token.

Den skal derfor aldri legges i GitHub, skjermbilder eller offentlig chat.

URL-en lagres kun i:

```text
receiver/config.json
```

Denne filen er gitignored.

## 1. Hent URL fra Sound Alerts

I Sound Alerts Dashboard finner du Browser Source-oppsettet og kopierer samme URL som du normalt limer inn i OBS Browser Source.

Ikke lim URL-en inn i GitHub.

## 2. Oppdater repo på BELABOX

```bash
cd ~/CometenIRLAlerts
git pull
cd receiver
```

## 3. Konfigurer Browser Audio

Kjør:

```bash
python3 configure-browser-audio.py
```

Lim inn Sound Alerts Browser Source-URL når scriptet spør.

Scriptet endrer bare Browser Audio-feltene i eksisterende `config.json`. Relay-token, lydmapping, LED-oppsett og andre lokale innstillinger beholdes.

Standard sink-match hentes fra eksisterende remote-control-oppsett. På det verifiserte oppsettet 21. august 2026 var sink-match:

```text
soundcore Select 4 Go
```

## 4. Installer Browser Audio-runtime

Browser Audio trenger:

- Chromium/Chrome-runtime
- Xvfb / `xvfb-run`
- `xauth`
- PipeWire `pw-cli`
- `wpctl`

På Ubuntu 22.04/Jammy ARM64 i BELABOX-imaget finnes ikke vanlig apt-`chromium` som forventet. `chromium-bsu` er et spill og skal ikke installeres. Snap-sporet brukes heller ikke i dette oppsettet.

Installer først basispakker:

```bash
sudo apt install -y xvfb xauth python3-venv
```

Installer deretter den lokale Playwright Chromium-runtime som er laget for IRL Browser Audio:

```bash
bash install-browser-runtime.sh
```

Runtime lagres under:

```text
~/.local/share/cometen-irl-browser-audio/
```

Install-scriptet setter automatisk korrekt lokal browser-path i `receiver/config.json`.

På BELABOX-imaget kan `/tmp` være begrenset selv om rot-disken har mye ledig plass. Runtime-installeren bruker derfor egen temp-katalog under `~/.local/share/cometen-irl-browser-audio/tmp` slik at store Chromium-nedlastinger og utpakking ikke stopper med `ENOSPC`.

Installer/start deretter tjenesten:

```bash
bash install-browser-audio.sh
```

## 5. Hva tjenesten gjør

User service:

```text
cometen-irl-browser-audio.service
```

Den:

1. venter på at konfigurert Bluetooth-sink finnes som PipeWire `Audio/Sink`
2. setter sinken som standard lydutgang
3. starter Chromium i en virtuell Xvfb-skjerm
4. åpner Browser Source-URL-en med autoplay aktivert
5. holder browseren aktiv selv om vinduet ikke er synlig
6. overvåker PipeWire sink-ID
7. restarter browseren dersom Bluetooth-sinken kobles opp igjen med ny dynamisk node-ID
8. starter browseren på nytt dersom Chromium krasjer

Browser Source-URL-en maskeres i loggen.

## 6. Test

Kontroller service:

```bash
systemctl --user status cometen-irl-browser-audio.service
```

Følg logg:

```bash
journalctl --user -u cometen-irl-browser-audio.service -f
```

Når Bluetooth-høyttaleren er tilkoblet skal loggen vise at riktig PipeWire sink er funnet og at Chromium er startet.

Åpne deretter Sound Alerts Dashboard og kjør:

```text
Play test alert
```

Målet er at samme test-alert høres:

- i OBS/stream-miksen hjemme
- fysisk på Bluetooth-høyttaleren ute ved BELABOX

### Verifisert 21. august 2026

Praktisk test på faktisk ROCK 5B+/BELABOX bekreftet:

- `cometen-irl-browser-audio.service` står `active (running)`
- lokal Playwright Chromium kjører under Xvfb
- PipeWire fant `soundcore Select 4 Go` og satte den som standard sink
- Sound Alerts Browser Source lastes på BELABOX
- testlyd fra Sound Alerts høres fysisk på Bluetooth-høyttaleren

Dermed er **Sound Alerts -> BELABOX -> PipeWire -> Bluetooth-høyttaler** praktisk verifisert.

Samtidig avspilling i både OBS-klienten og BELABOX-klienten regnes som full ende-til-ende-verifisering og skal bekreftes separat hvis dette ikke allerede observeres i samme test.

## 7. Restart og stopp

Restart:

```bash
systemctl --user restart cometen-irl-browser-audio.service
```

Stopp:

```bash
systemctl --user stop cometen-irl-browser-audio.service
```

Start:

```bash
systemctl --user start cometen-irl-browser-audio.service
```

Deaktiver permanent:

```bash
python3 configure-browser-audio.py --disable
systemctl --user disable --now cometen-irl-browser-audio.service
```

## 8. Konfigurasjonsfelt

`configure-browser-audio.py` legger inn Browser Audio-feltene i lokal `config.json`, blant annet:

```json
{
  "browser_audio_enabled": true,
  "browser_audio_url": "DIN_PRIVATE_BROWSER_SOURCE_URL",
  "browser_audio_sink_match": "soundcore Select 4 Go",
  "browser_audio_browser": "/home/user/.local/share/cometen-irl-browser-audio/.../chrome",
  "browser_audio_profile_directory": "~/.cache/cometen-irl-browser-audio/chromium-profile",
  "browser_audio_sink_wait_seconds": 120,
  "browser_audio_sink_check_seconds": 5,
  "browser_audio_width": 1280,
  "browser_audio_height": 720,
  "browser_audio_disable_gpu": true,
  "browser_audio_extra_args": []
}
```

Den eksakte browser-pathen settes automatisk av `install-browser-runtime.sh` og skal ikke hardkodes manuelt.

Ikke kopier eksempel-URL-en over en fungerende config. Bruk konfigurasjonsscriptet.

## Status

Implementert og BELABOX/Bluetooth-verifisert 21. august 2026.

Browser Source-avspilling fra Sound Alerts fungerer på faktisk ROCK 5B+ gjennom PipeWire til `soundcore Select 4 Go`. Full dobbelklient-test (OBS + BELABOX samtidig) beholdes som siste eksplisitte ende-til-ende-bekreftelse dersom den ikke er observert i samme alert-test.
