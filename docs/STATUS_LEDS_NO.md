# BELABOX status-LED-er - Cometen IRL Alerts

Denne modulen styrer fire 5 mm status-LED-er fra GPIO-headeren på Radxa ROCK 5B+.

LED-styringen ligger i `CometenIRLAlerts/receiver`. Selve alert-receiveren og GPIO-styringen kjører som vanlig user-systemd-tjeneste. Kun videosignal-proben kjører som en liten root-systemd-tjeneste fordi BELABOX `belacoder` kjører som root og Linux ellers skjuler `/proc/<pid>/fd` for vanlig bruker.

## Hardware

Bekreftet GPIO-oppsett på Cometen BELABOX:

```text
PIN 32 - PIN_32 - grønn - SYSTEM / ONLINE
PIN 34 - GND    - felles jord
PIN 36 - PIN_36 - blå   - BLUETOOTH / WPS200
PIN 38 - PIN_38 - gul    - VIDEO SIGNAL / INPUT
PIN 40 - PIN_40 - rød    - LIVE / OUTPUT
```

Disse fem fysiske pinnene ligger etter hverandre på samme rekke av 40-pin-headeren: `32, 34, 36, 38, 40`.

Hver LED bruker egen seriemotstand:

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

Langt LED-bein / anode går mot GPIO via motstanden. Kort bein / flat side / katode går til felles GND.

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

Gul viser aktiv lokal videopipeline, ikke bare at en USB-enhet er fysisk koblet til.

- fast lys: `belacoder`/pipeline har et faktisk BELABOX-videodevice åpent
- rask blink: encoder kjører, men ingen videodevice er aktivt åpent - input/pipeline er mistet eller restartes
- av: encoder/videopipeline er ikke aktiv

Kjente videoenheter:

```text
/dev/usb_capture
/dev/hdmirx
/dev/hdmi_capture
```

`camera_device` i lokal `config.json` prioriteres først. Device-path resolv'es til virkelig device, for eksempel:

```text
/dev/usb_capture -> /dev/video1
```

### Hvorfor root video-probe brukes

På den testede BELABOX-installasjonen kjører `belacoder` som `root` og holder `/dev/video1` åpen direkte. En vanlig user-tjeneste kan derfor se at `belacoder` kjører, men får ikke nødvendigvis lov til å lese symlinkene i `/proc/<belacoder-pid>/fd/`.

Dette ga tidligere falsk status:

```text
Video signal missing: belacoder process tree is running but no active video device is open
```

Løsningen er:

```text
cometen-irl-video-probe.service   root system service
        |
        | leser kun /proc + device-paths
        v
/run/cometen-irl-video-status.json   world-readable status
        |
        v
cometen-irl-alerts.service        vanlig user service
        |
        v
Gul/rød LED + !irlstatus
```

Root-proben åpner aldri kameraet. Den leser bare hvilke eksisterende file descriptors `belacoder`/barn har åpne og skriver en liten JSON-status til `/run`. Dermed konkurrerer den ikke med GStreamer om V4L2-enheten.

Statusfilen kan kontrolleres manuelt:

```bash
cat /run/cometen-irl-video-status.json
```

Ved normalt aktivt USB-videosignal forventes omtrent:

```json
{"encoder_running":true,"active":true,"device":"/dev/video1","pid":1234}
```

LED-koden godtar bare fersk probe-status. Standard stale-grense er 3 sekunder. Hvis root-proben ikke kjører eller statusfilen er gammel, faller modulen tilbake til eldre lokal/RTMP-deteksjon.

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

## Installer / oppdater GPIO og video-probe

Kjør på BELABOX:

```bash
cd ~/CometenIRLAlerts
git pull
cd receiver
bash ./install-gpio-leds.sh
```

Scriptet gjør nå begge deler:

- installerer `gpiod` og `python3-libgpiod`, gpio-gruppe og udev-regel
- installerer/oppdaterer root-tjenesten `cometen-irl-video-probe.service`

Ved aller første GPIO-installasjon anbefales reboot slik at gruppeendringen blir aktiv:

```bash
sudo reboot
```

Hvis GPIO-gruppen allerede er aktiv, er reboot normalt ikke nødvendig. Restart da user-tjenesten:

```bash
systemctl --user restart cometen-irl-alerts.service
```

Kontroller root-proben:

```bash
sudo systemctl status cometen-irl-video-probe.service --no-pager
cat /run/cometen-irl-video-status.json
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
  "live_process": "belacoder",
  "video_probe_seconds": 0.5,
  "video_probe_stale_seconds": 3.0
}
```

De to `video_probe_*`-verdiene er valgfrie. Standard er 0,5 sekund polling og 3 sekunder stale-grense.

## Test bare selve LED-ene

```bash
cd ~/CometenIRLAlerts/receiver
python3 status_leds.py config.json --test
```

Forventet:

```text
grønn -> blå -> gul -> rød -> alle
```

## Test videosignalstatus

```bash
cat /run/cometen-irl-video-status.json
journalctl --user -u cometen-irl-alerts.service -f
```

Når root-proben rapporterer `active=true`, skal gul bli fast. Ved et reelt GStreamer/V4L2-bortfall forventes `active=false` mens encoder restarter, og gul går til rask blink eller avhengig av encoderstatus.

## Installer/oppdater user-tjenestene

Alert-receiver/LED kjører fortsatt som vanlig bruker:

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

Hvis LED-ene er deaktivert i config, fortsetter receiveren å fungere som før.
