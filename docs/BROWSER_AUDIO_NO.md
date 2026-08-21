# IRL Browser Audio - flere Browser Source-kilder på BELABOX

IRL Browser Audio lar ROCK 5B+/BELABOX åpne Browser Source-URL-er headless og spille lyden direkte til Bluetooth-høyttaleren via PipeWire.

Løsningen er praktisk verifisert med Sound Alerts på `soundcore Select 4 Go`. Fra 21. august 2026 støtter den også flere samtidige kilder, for eksempel Sound Alerts og Blerp.

## Arkitektur

```text
Twitch viewer
    |
    +--> Sound Alerts --> OBS Browser Source hjemme --> stream
    |                \-> Browser Audio [soundalerts] --+
    |
    +--> Blerp ----------------------------------------+--> PipeWire --> Bluetooth
                                                     |
                           flere kilder ved behov ----+
```

Hver kilde kjøres i sin egen Chromium-prosess og får egen Chromium-profil. Hvis én kilde krasjer eller restartes, skal de andre fortsette.

## Sikkerhet

Browser Source-URL-er kan inneholde private nøkler/token.

- URL-ene lagres i `receiver/config.json`, som er gitignored.
- URL-ene skal aldri committes til GitHub.
- URL-ene maskeres i Browser Audio-loggen.
- Ved `!irlaudio add <navn> <url>` slettes selve Twitch-meldingen så snart Streamer.bot har mottatt og tolket den.
- Chatbekreftelsen inneholder bare kildenavnet, aldri URL-en.
- URL-en går midlertidig gjennom den eksisterende HTTPS-relayen som en kortlivet control-event. Control-events utløper etter maks 15 sekunder.

## Førstegangsoppsett

Oppdater repoet:

```bash
cd ~/CometenIRLAlerts
git pull
cd receiver
```

På BELABOX/Jammy ARM64 brukes lokal Playwright Chromium-runtime. Installer basisavhengigheter:

```bash
sudo apt install -y xvfb xauth python3-venv
```

Installer browser-runtime:

```bash
bash install-browser-runtime.sh
```

På BELABOX-imaget kan `/tmp` være begrenset selv om hoveddisken har mye ledig plass. Runtime-installeren bruker derfor egen temp-katalog under:

```text
~/.local/share/cometen-irl-browser-audio/tmp
```

Ikke installer `chromium-bsu`; det er ikke Chromium-nettleseren.

## Legg inn første kilde lokalt

Standardnavnet er `soundalerts`:

```bash
python3 configure-browser-audio.py
```

Lim Browser Source-URL-en direkte inn i SSH-terminalen når scriptet spør. Ikke lim den i chat eller GitHub.

Eksempel med eksplisitt navn:

```bash
python3 configure-browser-audio.py --name blerp
```

Vis konfigurerte kilder uten å vise URL-er:

```bash
python3 configure-browser-audio.py --list
```

Start/installer supervisor-tjenesten:

```bash
bash install-browser-audio.sh
```

Kontroller:

```bash
systemctl --user status cometen-irl-browser-audio.service
journalctl --user -u cometen-irl-browser-audio.service -f
```

## Multi-source-konfigurasjon

Lokal `config.json` bruker nå en liste:

```json
"browser_audio_enabled": true,
"browser_audio_sources": [
  {
    "name": "soundalerts",
    "url": "PRIVATE_URL",
    "enabled": true,
    "generation": 0
  },
  {
    "name": "blerp",
    "url": "PRIVATE_URL",
    "enabled": true,
    "generation": 0
  }
]
```

`browser_audio_url` beholdes foreløpig for bakoverkompatibilitet med eksisterende Sound Alerts-oppsett. Et gammelt single-source-oppsett migreres automatisk til kilden `soundalerts` når en multi-source-endring lagres.

Maks antall kilder via adminverktøy/chat er 8. Kildenavn kan inneholde små bokstaver, tall, `_` og `-`, maks 32 tegn.

## Chatkommandoer

Koble en Streamer.bot-kommando `!irlaudio` til actionen som bruker:

```text
streamerbot/CometenIRL_BrowserAudioControl.cs
```

Bruk en Starts With-kommando slik at resten av teksten sendes som `rawInput`.

Browser Audio-kommandoene er broadcaster/mod-only både i Streamer.bot-oppsettet og i C#-koden.

```text
!irlaudio status
!irlaudio on
!irlaudio off
!irlaudio restart

!irlaudio add soundalerts <Browser Source URL>
!irlaudio add blerp <Browser Source URL>
!irlaudio remove blerp

!irlaudio soundalerts on
!irlaudio soundalerts off
!irlaudio soundalerts restart
!irlaudio soundalerts status
```

`delete` og `del` kan også brukes som alias for `remove`.

### Hva on/off betyr

`!irlaudio off` stopper alle aktive browser-kilder, men lar `cometen-irl-browser-audio.service` stå aktiv som supervisor. Dermed kan `!irlaudio on` starte kildene igjen uten sudo/systemctl fra chat.

En enkelt kilde kan slås av uten å påvirke de andre:

```text
!irlaudio blerp off
```

## Legge til URL via chat

Eksempel:

```text
!irlaudio add blerp https://PRIVATE_BROWSER_SOURCE_URL
```

Flyt:

```text
Twitch chat
  -> Streamer.bot mottar msgId + kommando + URL
  -> original Twitch-melding slettes
  -> kontrollkommando sendes over HTTPS-relay
  -> BELABOX validerer og lagrer URL lokalt i config.json
  -> Browser Audio supervisor oppdager config-endringen
  -> bare blerp-prosessen startes/restartes
  -> BELABOX kvitterer
  -> Twitch får: IRL Audio: blerp lagt til
```

URL-en skal aldri gjentas i chatbekreftelsen eller loggen.

## Slette en kilde via chat

```text
!irlaudio remove blerp
```

Forventet bekreftelse:

```text
IRL Audio: blerp slettet
```

Supervisoren oppdager at kilden er fjernet og avslutter bare den Chromium-prosessen.

## Restart

Alle kilder:

```text
!irlaudio restart
```

Én kilde:

```text
!irlaudio soundalerts restart
```

Restart implementeres med en intern `generation`-verdi i config. Det gjør at supervisoren kan restarte akkurat riktig kilde uten å restarte hele systemd-tjenesten.

## Lokal administrasjon uten Twitch

Legg til/oppdater:

```bash
python3 configure-browser-audio.py --name blerp
```

Slå av én kilde:

```bash
python3 configure-browser-audio.py --disable-source blerp
```

Slå på én kilde:

```bash
python3 configure-browser-audio.py --enable blerp
```

Restart én kilde:

```bash
python3 configure-browser-audio.py --restart-source blerp
```

Slett én kilde:

```bash
python3 configure-browser-audio.py --remove blerp
```

Slå av master:

```bash
python3 configure-browser-audio.py --disable
```

## Generelt URL-filter i Twitch-chat

Repoet inneholder også:

```text
streamerbot/Cometen_ChatUrlGuard.cs
```

Lag en egen Streamer.bot-action med denne koden og koble den til:

```text
Twitch -> Chat -> Chat Message
```

Reglene er:

- broadcaster/mod/VIP kan poste vanlige URL-er
- vanlige brukere får URL-meldingen slettet og en kort beskjed om at lenker er for VIP/mods
- alle meldinger som både er kommando og inneholder URL slettes etter at Streamer.bot har mottatt eventen
- guard-actionen svarer ikke på URL-kommandoer; den egentlige kommando-actionen sender eventuell bekreftelse

Dette gjør blant annet at `!sr <url>` kan behandles av SR-systemet samtidig som URL-en forsvinner fra offentlig chat.

Streamer.bot bruker `msgId` fra Twitch-eventet for sletting. Botkontoen bør være moderator. Koden prøver botkontoen først og broadcasterkontoen som fallback.

## Test

1. Kjør `!irlaudio status`.
2. Legg til en testkilde med `!irlaudio add <navn> <url>`.
3. Bekreft at URL-meldingen forsvinner fra Twitch-chat.
4. Bekreft at chat bare svarer med kildenavn/status.
5. Spill en alert fra den nye kilden.
6. Bekreft lyd i Bluetooth-høyttaleren.
7. Slå bare den kilden av/på og kontroller at Sound Alerts fortsetter dersom den er en annen kilde.

## Verifisert status

21. august 2026 ble single-source-kjeden verifisert ende-til-ende på faktisk ROCK 5B+/BELABOX:

```text
Sound Alerts -> OBS hjemme
Sound Alerts -> BELABOX -> Chromium -> PipeWire -> soundcore Select 4 Go
```

Samme alert ble hørt begge steder.

Multi-source-supervisor, chatbasert add/remove/on/off/restart og generell URL-guard er ny kode fra samme dato og skal praktisk testgodkjennes før de markeres produksjonsverifisert.
