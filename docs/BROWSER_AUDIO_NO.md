# IRL Browser Audio - Sound Alerts på BELABOX

Denne modulen lar ROCK 5B+/BELABOX åpne den samme Browser Source-URL-en som brukes i OBS, og spille lyden direkte ut på Bluetooth-høyttaleren via PipeWire.

Løsningen er laget og praktisk verifisert med **Sound Alerts**, men er generell og kan også brukes med andre tjenester som leverer en vanlig Browser Source-URL.

## Arkitektur

```text
Twitch viewer
    |
    v
Sound Alerts
    |
    +--> OBS Browser Source på streaming-PC --> lyd på stream
    |
    +--> Chromium på ROCK 5B+ --> PipeWire --> Bluetooth-høyttaler
```

Browser Audio er en del av samme `CometenIRLAlerts`-prosjekt. Den bruker ikke en egen relay og laster ikke ned enkeltlyder på forhånd.

## Verifisert 21. august 2026

Full praktisk ende-til-ende-test er bekreftet på faktisk ROCK 5B+/BELABOX:

- `cometen-irl-browser-audio.service` står `active (running)`
- lokal Playwright Chromium kjører under Xvfb
- PipeWire finner `soundcore Select 4 Go` og setter den som standard sink
- Sound Alerts Browser Source lastes på BELABOX
- samme Sound Alerts-test høres i **OBS hjemme**
- samme Sound Alerts-test høres samtidig fysisk på **soundcore Select 4 Go** ute ved BELABOX

Dermed er hele kjeden verifisert:

```text
Sound Alerts -> OBS hjemme
            -> BELABOX -> Chromium -> PipeWire -> soundcore Select 4 Go
```

## Sikkerhet

Sound Alerts Browser Source-URL kan inneholde en unik nøkkel/token.

Den skal derfor aldri legges i GitHub, skjermbilder eller offentlig chat.

URL-en lagres kun i:

```text
receiver/config.json
```

Denne filen er gitignored.

Hvis Browser Source-URL-en ved et uhell blir offentlig, bør den regenereres/resettes i Sound Alerts og konfigureres på nytt lokalt.

# Installasjon

## 1. Hent Browser Source-URL fra Sound Alerts

I Sound Alerts Dashboard finner du Browser Source-oppsettet og kopierer samme URL som brukes i OBS Browser Source.

Ikke lim URL-en inn i GitHub eller offentlig chat.

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

Standard sink-match hentes fra eksisterende remote-control-oppsett. På det verifiserte oppsettet er sink-match:

```text
soundcore Select 4 Go
```

Forventet avslutning fra scriptet:

```text
IRL Browser Audio er konfigurert.
Audio sink-match: soundcore Select 4 Go
Browser Source URL er lagret lokalt og vises ikke her.
```

## 4. Installer basispakker

Browser Audio trenger:

- Xvfb / `xvfb-run`
- `xauth`
- `python3-venv`
- PipeWire `pw-cli`
- `wpctl`
- lokal Chromium-runtime

Installer basispakkene:

```bash
sudo apt install -y xvfb xauth python3-venv
```

Det er ikke nødvendig å kjøre full systemoppgradering.

### Viktig på Ubuntu 22.04/Jammy ARM64

På BELABOX-imaget finnes ikke vanlig apt-`chromium` som forventet. `chromium-bsu` er et spill og skal **ikke** installeres. Snap-sporet brukes heller ikke i dette oppsettet.

Browser Audio bruker i stedet en lokal Playwright Chromium-runtime.

## 5. Installer lokal Chromium-runtime

Kjør:

```bash
bash install-browser-runtime.sh
```

Runtime lagres under:

```text
~/.local/share/cometen-irl-browser-audio/
```

Scriptet setter automatisk korrekt lokal browser-path i `receiver/config.json`.

På BELABOX-imaget kan `/tmp` være begrenset selv om rot-disken har mye ledig plass. Runtime-installeren bruker derfor egen temp-katalog:

```text
~/.local/share/cometen-irl-browser-audio/tmp
```

Dette hindrer Chromium-nedlastingen i å stoppe med `ENOSPC` på den lille `/tmp`-tmpfs-en.

Forventet resultat:

```text
Playwright Chromium er installert lokalt for IRL Browser Audio.
Browser: /home/user/.local/share/cometen-irl-browser-audio/.../chrome
config.json er oppdatert med lokal browser-path.
```

## 6. Installer og start Browser Audio-tjenesten

Kjør:

```bash
bash install-browser-audio.sh
```

Dette oppretter og aktiverer user service:

```text
cometen-irl-browser-audio.service
```

Tjenesten starter automatisk igjen ved senere oppstart når user-systemd/linger-oppsettet ellers er aktivt.

Kontroller status:

```bash
systemctl --user status cometen-irl-browser-audio.service
```

Forventet status:

```text
Active: active (running)
```

## 7. Test Sound Alerts

Følg gjerne loggen i ett SSH-vindu:

```bash
journalctl --user -u cometen-irl-browser-audio.service -f
```

Når Bluetooth-høyttaleren er tilkoblet skal loggen vise at riktig PipeWire sink er funnet og at Chromium er startet.

Åpne Sound Alerts Dashboard og kjør:

```text
Play test alert
```

Bekreft at samme alert høres:

1. i OBS/stream-miksen hjemme
2. fysisk på Bluetooth-høyttaleren ute ved BELABOX

Hvis begge spiller samme alert, er hele dobbeltklient-oppsettet verifisert.

# Hva tjenesten gjør

`cometen-irl-browser-audio.service`:

1. venter på at konfigurert Bluetooth-sink finnes som PipeWire `Audio/Sink`
2. setter sinken som standard lydutgang
3. starter Chromium i en virtuell Xvfb-skjerm
4. åpner Browser Source-URL-en med autoplay aktivert
5. holder browseren aktiv selv om vinduet ikke er synlig
6. overvåker PipeWire sink-ID
7. restarter browseren dersom Bluetooth-sinken kobles opp igjen med ny dynamisk node-ID
8. starter browseren på nytt dersom Chromium krasjer

Browser Source-URL-en maskeres i loggen.

# Drift

Status:

```bash
systemctl --user status cometen-irl-browser-audio.service
```

Logg:

```bash
journalctl --user -u cometen-irl-browser-audio.service -f
```

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

# Oppdatering

Ved senere kodeoppdatering:

```bash
cd ~/CometenIRLAlerts
git pull
cd receiver
bash install-browser-audio.sh
```

`receiver/config.json` er lokal og gitignored, så Browser Source-URL og andre lokale innstillinger beholdes ved vanlig `git pull`.

Hvis Browser Audio-runtime-installasjonen er endret i repoet, kan denne kjøres på nytt uten å skrive inn Sound Alerts-URL på nytt:

```bash
bash install-browser-runtime.sh
bash install-browser-audio.sh
```

# Bytte eller resette Sound Alerts-URL

Hvis Sound Alerts gir deg en ny Browser Source-URL:

```bash
cd ~/CometenIRLAlerts/receiver
python3 configure-browser-audio.py
systemctl --user restart cometen-irl-browser-audio.service
```

Lim inn den nye URL-en når scriptet spør.

# Feilsøking

## `browser_audio_enabled er ikke aktivert`

Kjør først:

```bash
python3 configure-browser-audio.py
```

## `Package 'chromium' has no installation candidate`

Dette er forventet på det testede Jammy ARM64-imaget.

Ikke installer `chromium-bsu`.

Bruk:

```bash
sudo apt install -y xvfb xauth python3-venv
bash install-browser-runtime.sh
```

## `ENOSPC: no space left on device` under Chromium-nedlasting

Sjekk først reell diskplass:

```bash
df -h / /home/user
df -i / /home/user
```

På det testede BELABOX-imaget kom `ENOSPC` fra begrenset `/tmp`, ikke fra SSD-en. Ny runtime-installer bruker egen temp-katalog på hjemmedisken.

Oppdater repo og kjør runtime-installasjonen på nytt:

```bash
cd ~/CometenIRLAlerts
git pull
cd receiver
bash install-browser-runtime.sh
```

## Tjenesten kjører, men ingen lyd kommer

Kontroller først Bluetooth/PipeWire:

```bash
wpctl status
```

Kontroller deretter Browser Audio-loggen:

```bash
journalctl --user -u cometen-irl-browser-audio.service -n 100 --no-pager
```

Se etter at konfigurert sink blir funnet og satt som default.

Hvis Bluetooth-enheten har fått annet navn, kjør konfigurasjonsscriptet på nytt med riktig sink-match.

## Sound Alerts fungerer i OBS, men ikke på BELABOX

Kontroller:

```bash
systemctl --user status cometen-irl-browser-audio.service
journalctl --user -u cometen-irl-browser-audio.service -n 100 --no-pager
```

Bekreft også at Browser Source-URL-en på BELABOX er den samme aktive URL-en som brukes av Sound Alerts-oppsettet i OBS.

# Konfigurasjonsfelt

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

**Produksjonsverifisert 21. august 2026 for Sound Alerts dobbeltklient-avspilling.**

Samme Sound Alerts-event er bekreftet samtidig i OBS hjemme og på `soundcore Select 4 Go` via BELABOX Browser Audio.