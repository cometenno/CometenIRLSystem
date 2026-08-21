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

Standard sink-match er den samme som remote control bruker, normalt:

```text
WPS200
```

## 4. Installer avhengigheter ved behov

Browser Audio trenger:

- Chromium eller Chrome
- Xvfb / `xvfb-run`
- `xauth`
- PipeWire `pw-cli`
- `wpctl`

Kjør install-scriptet først:

```bash
bash install-browser-audio.sh
```

Hvis noe mangler stopper scriptet og viser komponentene.

På Debian/Ubuntu-baserte image er typisk installasjon:

```bash
sudo apt update
sudo apt install -y xvfb xauth chromium
```

Installer bare manglende pakker. Ikke kjør full systemoppgradering bare for Browser Audio.

Kjør deretter install-scriptet igjen:

```bash
bash install-browser-audio.sh
```

## 5. Hva tjenesten gjør

User service:

```text
cometen-irl-browser-audio.service
```

Den:

1. venter på at WPS200 finnes som PipeWire `Audio/Sink`
2. setter WPS200 som standard sink
3. starter Chromium i en virtuell Xvfb-skjerm
4. åpner Browser Source-URL-en med autoplay aktivert
5. holder browseren aktiv selv om vinduet ikke er synlig
6. overvåker PipeWire sink-ID
7. restarter browseren dersom WPS200 kobles opp igjen med ny dynamisk node-ID
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

Når WPS200 er tilkoblet skal loggen blant annet vise at sinken er funnet og Chromium startet.

Åpne deretter Sound Alerts Dashboard og kjør:

```text
Play test alert
```

Målet er at samme test-alert høres:

- i OBS/stream-miksen hjemme
- fysisk på WPS200/Soundcore ute ved BELABOX

Dette må praktisk verifiseres. Sound Alerts-delen regnes ikke som produksjonsverifisert før samme alert er bekreftet samtidig gjennom begge klientene.

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

`configure-browser-audio.py` legger inn disse feltene i lokal `config.json`:

```json
{
  "browser_audio_enabled": true,
  "browser_audio_url": "DIN_PRIVATE_BROWSER_SOURCE_URL",
  "browser_audio_sink_match": "WPS200",
  "browser_audio_browser": "auto",
  "browser_audio_profile_directory": "~/.cache/cometen-irl-browser-audio/chromium-profile",
  "browser_audio_sink_wait_seconds": 120,
  "browser_audio_sink_check_seconds": 5,
  "browser_audio_width": 1280,
  "browser_audio_height": 720,
  "browser_audio_disable_gpu": true,
  "browser_audio_extra_args": []
}
```

Ikke kopier eksempel-URL-en over en fungerende config. Bruk konfigurasjonsscriptet.

## Status

Implementert 21. august 2026.

Kode og systemd-oppsett er lagt inn i repoet. Praktisk Sound Alerts-test på faktisk ROCK 5B+/WPS200 gjenstår før funksjonen markeres produksjonsverifisert.
