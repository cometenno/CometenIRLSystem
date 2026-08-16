# BELABOX status-LED-er - Cometen IRL Alerts

Denne modulen styrer fire 5 mm status-LED-er fra GPIO-headeren på Radxa ROCK 5B+.

LED-styringen ligger i `CometenIRLAlerts/receiver`. Selve alert-receiveren og GPIO-styringen kjører som vanlig user-systemd-tjeneste. Kun videosignal-proben kjører som en liten root-systemd-tjeneste fordi BELABOX `belacoder` kjører som root og Linux ellers skjuler `/proc/<pid>/fd` for vanlig bruker.

## Hardware

Bekreftet GPIO-oppsett på Cometen BELABOX:

```text
PIN 32 - PIN_32 - grønn - SYSTEM / ONLINE
PIN 34 - GND    - felles jord
PIN 36 - PIN_36 - blå   - BLUETOOTH / WPS200
PIN 38 - PIN_38 - gul    - VIDEO INPUT
PIN 40 - PIN_40 - rød    - BELABOX ENCODER / OUTPUT
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

### Gul - VIDEO INPUT

Gul og rød er bevisst skilt fra hverandre.

**Gul skal vise at videokilden/inputen er tilgjengelig, også når BELABOX-streamen er stoppet med vilje.**

Regler:

- fast lys når videokilden finnes og BELABOX encoder er stoppet
- fast lys når BELABOX encoder kjører og pipelinen faktisk bruker videodevicet
- rask blink når encoder kjører, men videodevicet ikke lenger er aktivt i pipelinen - for eksempel ved V4L2/GStreamer-feil eller pipeline-restart
- av når ingen kjent lokal videokilde er tilgjengelig

Dette betyr at `Stop` i BELABOX normalt skal gi:

```text
Gul: FAST - videokilden finnes fortsatt
Rød: AV   - belacoder/encoder er stoppet
```

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

På dagens USB Facecam-oppsett brukes tilstedeværelsen av `/dev/usb_capture` som input-status når encoder er stoppet. Når encoder kjører kreves i tillegg at `belacoder`-pipelinen faktisk har videoenheten åpen.

For en framtidig HDMI-kilde kan kilde-spesifikk signal-lock/dv-timings-deteksjon legges til dersom det er nødvendig å skille mellom at HDMI-capture-enheten finnes og at HDMI-signalet faktisk er låst.

**Bekreftet 16. august 2026:** root video-probe + LED-integrasjonen er testet på ROCK 5B+ med `/dev/usb_capture -> /dev/video1`. Med aktiv videopipeline lyser gul LED fast.

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

Root-proben åpner aldri kameraet. Den leser bare hvilke eksisterende file descriptors `belacoder`/barn har åpne og hvilke kjente video-device-paths som finnes. Dermed konkurrerer den ikke med GStreamer om V4L2-enheten.

Statusfilen kan kontrolleres manuelt:

```bash
cat /run/cometen-irl-video-status.json
```

Ny statusmodell:

```json
{
  "encoder_running": true,
  "source_present": true,
  "pipeline_active": true,
  "active": true,
  "device": "/dev/video1"
}
```

Feltene betyr:

- `source_present` - lokal videokilde/device finnes
- `pipeline_active` - encoder-pipelinen har videodevicet åpent
- `encoder_running` - `belacoder` kjører
- `active` - effektiv status som brukes av gul LED

Når encoder er stoppet med vilje og input fortsatt finnes, forventes omtrent:

```json
{
  "encoder_running": false,
  "source_present": true,
  "pipeline_active": false,
  "active": true,
  "device": "/dev/video1"
}
```

LED-koden godtar bare fersk probe-status. Standard stale-grense er 3 sekunder. Hvis root-proben ikke kjører eller statusfilen er gammel, faller modulen tilbake til eldre lokal/RTMP-deteksjon.

### Rød - BELABOX ENCODER / OUTPUT

- fast lys: `belacoder` kjører og video-input/pipeline er OK
- rask blink: `belacoder` kjører, men video-input/pipeline mangler
- sakte blink: encoder kjører, men videostatus kan ikke bestemmes
- av: BELABOX encoder er stoppet

Rød LED viser BELABOX encoder/output-status. Den er ikke Twitch/OBS-live-indikator.

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

## Test video- og encoderstatus

```bash
cat /run/cometen-irl-video-status.json
journalctl --user -u cometen-irl-alerts.service -f
```

Testsekvens:

1. Kamera/input koblet til, BELABOX startet - gul fast, rød fast.
2. Trykk `Stop` i BELABOX - gul skal forbli fast, rød skal gå av.
3. Start BELABOX igjen - rød skal bli fast igjen når pipeline er aktiv.
4. Ved reelt V4L2/GStreamer-drop mens encoder kjører skal gul/rød indikere feilen under restart.

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
