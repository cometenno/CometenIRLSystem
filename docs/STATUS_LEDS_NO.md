# BELABOX status-LED-er - Cometen IRL Alerts

Denne modulen styrer fire 5 mm status-LED-er fra GPIO-headeren på Radxa ROCK 5B+.

LED-styringen ligger i `CometenIRLAlerts/receiver` og starter sammen med den eksisterende
`cometen-irl-alerts.service`. Alert-receiveren fortsetter å fungere dersom LED-funksjonen
er deaktivert eller GPIO ikke kan åpnes.

## Hardware

Bekreftet ledige GPIO-linjer på Cometen BELABOX:

```text
PIN 32 - PIN_32 - grønn - SYSTEM / ONLINE
PIN 34 - GND    - felles jord
PIN 36 - PIN_36 - blå   - BLUETOOTH / WPS200
PIN 38 - PIN_38 - gul    - CAMERA / RTMP INPUT
PIN 40 - PIN_40 - rød    - LIVE / OUTPUT
```

Disse fem fysiske pinnene ligger etter hverandre på samme rekke av 40-pin-headeren:
`32, 34, 36, 38, 40`. Det gjør det mulig å bruke én 1x5 hunnkontakt med 2,54 mm pitch.

Hver LED skal ha sin egen seriemotstand. Bygget er planlagt med:

```text
680 ohm per LED
```

Kobling:

```text
PIN_32 ---- 680R ---->|----+
PIN_36 ---- 680R ---->|----+
PIN_38 ---- 680R ---->|----+---- PIN_34 GND
PIN_40 ---- 680R ---->|----+
```

Langt LED-bein / anode går mot GPIO via motstanden.
Kort bein / flat side / katode går til felles GND.

## Statusmønstre

### Grønn - SYSTEM / ONLINE

- sakte blink: receiver starter / relay-host er ikke nådd ennå
- fast lys: relay-host kan nås over nett
- rask blink: nett/relay ble borte etter at systemet hadde vært online
- av: boksen eller CometenIRLAlerts-tjenesten er av

### Blå - BLUETOOTH / WPS200

- fast lys: WPS200 finnes som PipeWire `Audio/Sink`
- sakte blink: WPS200 er ikke koblet til, men watchdog-tjenesten kjører
- rask blink: WPS200 er borte og watchdog-tjenesten er ikke aktiv

### Gul - CAMERA / RTMP INPUT

- fast lys: nginx har aktiv `publish/live`-stream
- sakte blink: venter på kamera
- rask blink: kamera-feed har vært aktiv og forsvant

Programmet prøver først nginx RTMP-statistikk via `http://127.0.0.1/stat`.
Hvis denne URL-en ikke er tilgjengelig, brukes en fallback som ser etter en ekstern
TCP-publisher på port 1935. Lokale `127.0.0.1`-lesere fra belacoder telles ikke som kamera.

### Rød - LIVE / OUTPUT

- fast lys: `belacoder` kjører og kamera-input er aktiv
- sakte blink: `belacoder` kjører, men kamerastatus kan ikke bekreftes
- rask blink: `belacoder` kjører mens kamera-input mangler
- av: BELABOX encoder ikke aktiv / ikke live

## Oppstartstest

Når LED-funksjonen starter, kjører den normalt:

```text
grønn -> blå -> gul -> rød -> alle
```

Standard tid per steg er 0,3 sekund.

## Installer GPIO-tilgang

Kjør på BELABOX:

```bash
cd ~/CometenIRLAlerts
git pull
cd receiver
bash ./install-gpio-leds.sh
```

Scriptet installerer `gpiod` og `python3-libgpiod`, og lager en `gpio`-gruppe/udev-regel
slik at den eksisterende user-systemd-tjenesten kan bruke `/dev/gpiochip*` uten root.

Reboot én gang etter installasjonen:

```bash
sudo reboot
```

## Aktiver i lokal config.json

Den virkelige `receiver/config.json` ligger lokalt og skal ikke legges i GitHub.

Legg til:

```json
"status_leds_enabled": true,
"status_leds": {
  "green_line": "PIN_32",
  "blue_line": "PIN_36",
  "yellow_line": "PIN_38",
  "red_line": "PIN_40",
  "active_high": true,
  "poll_seconds": 2.0,
  "lamp_test": true,
  "lamp_test_seconds": 0.3,
  "bluetooth_sink_match": "WPS200",
  "bluetooth_watchdog_service": "cometen-wps200.service",
  "camera_status_url": "http://127.0.0.1/stat",
  "camera_app": "publish",
  "camera_stream": "live",
  "live_process": "belacoder"
}
```

## Test bare LED-ene

Etter wiring, GPIO-installasjon og reboot:

```bash
cd ~/CometenIRLAlerts/receiver
python3 status_leds.py config.json --test
```

Forventet:

```text
grønn -> blå -> gul -> rød -> alle
```

## Installer/oppdater brukertjenesten

Service-malen bruker `run_receiver.py`, som starter LED-monitoren og den eksisterende
`receiver.py` i samme CometenIRLAlerts-tjeneste.

Etter `git pull`:

```bash
cd ~/CometenIRLAlerts/receiver
bash ./install-user-service.sh
systemctl --user restart cometen-irl-alerts.service
```

Status:

```bash
systemctl --user status cometen-irl-alerts.service --no-pager
journalctl --user -u cometen-irl-alerts.service -f
```

Hvis LED-ene er deaktivert i config, kjører receiveren akkurat som før.
