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
PIN 38 - PIN_38 - gul    - VIDEO SIGNAL / INPUT
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

### Gul - VIDEO SIGNAL / INPUT

Gul skal vise aktiv lokal videopipeline, ikke bare at en USB-enhet er fysisk koblet til.

- fast lys: `belacoder` kjører og har et faktisk BELABOX-videodevice åpent
- rask blink: `belacoder` kjører, men ingen videodevice er aktivt åpent - input/pipeline er mistet eller restartes
- av: encoder/videopipeline er ikke aktiv

Modulen auto-detekterer disse BELABOX-videoenhetene:

```text
/dev/usb_capture
/dev/hdmirx
/dev/hdmi_capture
```

`camera_device` i lokal `config.json` prioriteres først dersom en annen device-path brukes.
Alle paths blir resolvet til det virkelige device-et, for eksempel:

```text
/dev/usb_capture -> /dev/video1
```

Deretter kontrollerer modulen `/proc/<belacoder-pid>/fd/` for å bekrefte at GStreamer/belacoder faktisk har videodevicet åpent. LED-modulen åpner derfor aldri kameraet selv og konkurrerer ikke med BELABOX om V4L2-enheten.

Dette er valgt fordi BELABOX selv avslutter/restarter `belacoder` når `v4l2src0` feiler eller pipelinen staller. Dermed forsvinner den aktive device-handle under et reelt videobortfall og gul går over til feilindikasjon.

Dersom ingen lokal BELABOX-videoenhet finnes, beholdes eldre RTMP-deteksjon (`http://127.0.0.1/stat` / port 1935) som legacy-fallback.

### Rød - LIVE / OUTPUT

- fast lys: `belacoder` kjører og video-input er aktiv
- sakte blink: `belacoder` kjører, men videostatus kan ikke bestemmes
- rask blink: `belacoder` kjører mens video-input mangler
- av: BELABOX encoder ikke aktiv / ikke live

Rød LED viser foreløpig BELABOX encoder/output-status. Den er ikke Twitch/OBS-live-indikator.

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

`camera_device` er valgfri. Dersom den mangler brukes `/dev/usb_capture` først, og de kjente HDMI-device-pathene blir også kontrollert automatisk.

## Test bare LED-ene

```bash
cd ~/CometenIRLAlerts/receiver
python3 status_leds.py config.json --test
```

Forventet:

```text
grønn -> blå -> gul -> rød -> alle
```

## Test videosignalstatus

Etter restart kan videosignalendringer sees i receiver-loggen:

```bash
journalctl --user -u cometen-irl-alerts.service -f
```

Eksempler:

```text
Video signal active: local BELABOX video device is open by belacoder
Video signal missing: belacoder is running but no active video device is open
Video signal inactive: encoder is stopped
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
