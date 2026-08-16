# Oppsett av receiver på ROCK 5B+

Sist oppdatert: 16. august 2026.

Receiveren kjører ved siden av BELABOX og bruker samme repo som heartbeat, remote control og statusfunksjoner.

## 1. Klon repoet

```bash
cd ~
git clone https://github.com/la1ona/CometenIRLAlerts.git
cd CometenIRLAlerts/receiver
```

Ved senere oppdatering:

```bash
cd ~/CometenIRLAlerts
git pull
```

## 2. Lag lokal config

```bash
cd ~/CometenIRLAlerts/receiver
cp config.example.json config.json
nano config.json
```

Minimum må ha korrekt:

```text
relay_base_url
receiver_token
```

Heartbeat-standard:

```json
"heartbeat_receiver_id": "belabox",
"heartbeat_interval_seconds": 30,
"heartbeat_timeout_seconds": 5
```

Ikke sett heartbeat tilbake til 1 sekund. Det utløste `HTTP 429 Too Many Requests` på webhotellet.

Valider:

```bash
python3 -m json.tool config.json >/dev/null && echo "config.json OK"
```

## 3. Legg inn lokale lydfiler

Legg PCM WAV-filer i:

```text
~/CometenIRLAlerts/receiver/sounds/
```

Test:

```bash
pw-play ~/CometenIRLAlerts/receiver/sounds/test.wav
```

## 4. Installer user services

Bruk user-systemd. Dette er viktig fordi PipeWire/WirePlumber og Bluetooth-lyd kjører i brukerens sesjon.

Kjør:

```bash
cd ~/CometenIRLAlerts/receiver
bash install-user-service.sh
```

Skriptet installerer nå begge:

```text
cometen-irl-alerts.service
cometen-irl-heartbeat.service
```

Aktiver linger én gang:

```bash
sudo loginctl enable-linger "$USER"
```

## 5. Kontroller tjenester

Alert-receiver:

```bash
systemctl --user status cometen-irl-alerts.service
journalctl --user -u cometen-irl-alerts.service -n 30 --no-pager
```

Heartbeat:

```bash
systemctl --user status cometen-irl-heartbeat.service
journalctl --user -u cometen-irl-heartbeat.service -n 30 --no-pager
```

For heartbeat skal oppstart vise omtrent:

```text
Cometen IRL heartbeat started: receiver_id=belabox interval=30.0s
```

## 6. Restart etter config-endring

```bash
systemctl --user restart cometen-irl-alerts.service
systemctl --user restart cometen-irl-heartbeat.service
```

## 7. Oppdater eksisterende installasjon

```bash
cd ~/CometenIRLAlerts
git pull
cd receiver
bash install-user-service.sh
```

Dette oppdaterer de genererte user-service-filene og starter/aktiverer begge tjenester.

## 8. Bluetooth/PipeWire

Kontroller sinks:

```bash
wpctl status
```

Sett standard sink:

```bash
wpctl set-default SINK_ID
```

Fullt verifisert headless-oppsett for ROCK 5B+/BELABOX:

```text
docs/BELABOX_ROCK5B_HEADLESS_NO.md
```

## 9. Heartbeat er diagnostikk

Heartbeat forteller om boksen/receiveren lever. Den sier ikke nødvendigvis at kamera, GStreamer eller SRT-video er frisk.

OBS-scene-failover skal derfor baseres på BELABOX Cloud ingest-stats gjennom `IRLAlertsController`, ikke heartbeat alene.

Detaljer:

```text
docs/WATCHDOG_HEARTBEAT_NO.md
```

## 10. USB/video-feil

Nattest 16. august 2026 viste gjentatte UVC/GStreamer-feil fra Elgato Facecam, inkludert `uvcvideo -71`, URB-resubmit-feil og eksplisitt USB-disconnect. Dette er et separat hardware/input-spor.

For kun nye hendelser:

```bash
sudo journalctl -kf -n 0 | grep -Ei 'uvc|usb|video|v4l2|xhci|disconnect|reset|error'
```

Videre fysisk test av kamera/kabel avventes til annet utstyr er tilgjengelig.
