# BELABOX status-LED-er - Cometen IRL Alerts

Denne modulen styrer fire 5 mm status-LED-er fra GPIO-headeren på Radxa ROCK 5B+.

LED-styringen ligger i `CometenIRLAlerts/receiver` og starter sammen med den eksisterende
`cometen-irl-alerts.service`. Alert-receiveren fortsetter å fungere dersom LED-funksjonen
er deaktivert eller GPIO ikke kan åpnes.

## Hardware

Bekreftet GPIO-oppsett på Cometen BELABOX:

```text
PIN 32 - PIN_32 - grønn - SYSTEM / ONLINE
PIN 34 - GND    - felles jord
PIN 36 - PIN_36 - blå   - BLUETOOTH / WPS200
PIN 38 - PIN_38 - gul    - CAMERA / INPUT
PIN 40 - PIN_40 - rød    - LIVE / OUTPUT
```

Disse fem fysiske pinnene ligger etter hverandre på samme rekke av 40-pin-headeren:
`32, 34, 36, 38, 40`.

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

### Gul - CAMERA / INPUT

- fast lys: USB-kamera finnes på `/dev/usb_capture`
- sakte blink: kamera kan ikke bekreftes ennå
- rask blink: kamera-feed har vært aktiv og senere forsvinner

Fra 16. august 2026 sjekker LED-modulen USB-kameraet først. Standard device er:

```text
/dev/usb_capture
```

Hvis USB-device ikke kan bekreftes, brukes den eldre RTMP-sjekken som fallback:
`http://127.0.0.1/stat` og deretter ekstern publisher på port 1935.

Dette retter problemet der gul/rød LED blinket selv om Elgato Facecam faktisk var koblet til via USB.

### Rød - LIVE / OUTPUT

- fast lys: `belacoder` kjører og kamera-input er bekreftet
- sakte blink: `belacoder` kjører, men kamerastatus kan ikke bekreftes
- rask blink: `belacoder` kjører mens kamera-input mangler
- av: BELABOX encoder ikke aktiv / ikke live

Rød LED viser at encoder/output er aktiv. Den er ikke Twitch/OBS-live-indikator.

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
slik at user-systemd-tjenesten kan bruke `/dev/gpiochip*` uten root.

Reboot én gang etter første GPIO-installasjon:

```bash
sudo reboot
```

## Aktiver i lokal config.json

Den virkelige `receiver/config.json` ligger lokalt og skal ikke legges i GitHub.

Relevant del:

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
  "camera_device": "/dev/usb_capture",
  "camera_status_url": "http://127.0.0.1/stat",
  "camera_app": "publish",
  "camera_stream": "live",
  "live_process": "belacoder"
}
```

`camera_device` er valgfri. Dersom den mangler brukes `/dev/usb_capture` som standard.

## Test bare LED-ene

```bash
cd ~/CometenIRLAlerts/receiver
python3 status_leds.py config.json --test
```

Forventet:

```text
grønn -> blå -> gul -> rød -> alle
```

## Installer/oppdater brukertjenesten

Service-malen bruker `run_receiver.py`, som starter LED-monitoren og `receiver.py` i samme
CometenIRLAlerts-tjeneste.

Etter `git pull`:

```bash
cd ~/CometenIRLAlerts/receiver
bash ./install-user-service.sh
systemctl --user restart cometen-irl-alerts.service
```

Status og logg:

```bash
systemctl --user status cometen-irl-alerts.service --no-pager
journalctl --user -u cometen-irl-alerts.service -f
```

Hvis LED-ene er deaktivert i config, kjører receiveren akkurat som før.
