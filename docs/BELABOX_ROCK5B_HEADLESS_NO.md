# BELABOX ROCK 5B+ - headless Bluetooth alerts

Denne siden dokumenterer det bekreftede oppsettet som brukes på Cometen BELABOX med Radxa ROCK 5B+.

Målet er at hele alert-kjeden skal starte uten lokal innlogging eller SSH:

```text
Strøm på BELABOX
    |
    v
BlueZ + Realtek Bluetooth-driver
    |
    v
PipeWire + WirePlumber
    |
    v
WPS200 kobles automatisk til
    |
    v
CometenIRLAlerts receiver + stille keepalive
    |
    v
Follow/Sub/etc. fra relay spilles lokalt
```

Status: bekreftet fungerende 8. august 2026.

## Testet plattform

- Radxa ROCK 5B+
- BELABOX image `20250915-a84acea`
- Ubuntu 22.04.5 LTS
- kernel `5.10.160-belabox`
- WirePlumber `0.4.8-4`
- PipeWire 0.3.x
- Realtek RTL8852BE Bluetooth-delen på USB, USB ID `13d3:3572`
- vendor-driver `rtk_btusb`
- Bluetooth-høyttaler: WPS200
- BELABOX-bruker: `user`, UID `1000`

> UID, brukernavn, MAC-adresse og stier må tilpasses hvis oppsettet brukes på en annen maskin.

## 1. Installer lyd- og Bluetooth-pakkene

```bash
sudo apt update
sudo apt install -y \
  pipewire \
  pipewire-bin \
  pipewire-audio-client-libraries \
  wireplumber \
  libspa-0.2-bluetooth \
  bluez
```

Aktiver tjenestene:

```bash
sudo systemctl enable --now bluetooth
systemctl --user enable --now pipewire.service wireplumber.service
sudo loginctl enable-linger user
```

Kontroller linger:

```bash
loginctl show-user user -p Linger
```

Forventet:

```text
Linger=yes
```

## 2. Realtek RTL8852BE - bruk `rtk_btusb`

På denne BELABOX-imagen bandt generisk `btusb` seg til kontrolleren, men scanning fant ingen Bluetooth-enheter. BELABOX-kjernen inneholder samtidig Radxa/Realtek-driveren `rtk_btusb`, som fungerer med kontrolleren.

### Firmware-navn

`rtk_btusb` forventer firmwarefilene i firmware-roten uten `.bin`:

```text
rtl8852bu_fw
rtl8852bu_config
```

BELABOX hadde filene under `/lib/firmware/rtl_bt/`.

Lag derfor permanente symlinker:

```bash
sudo ln -sf /lib/firmware/rtl_bt/rtl8852bu_fw.bin \
  /lib/firmware/rtl8852bu_fw

sudo ln -sf /lib/firmware/rtl_bt/rtl8852bu_config.bin \
  /lib/firmware/rtl8852bu_config
```

`rtl8852bu_config.bin` kan allerede være en symlink til `rtl8761bu_config.bin`. Det er normalt på denne installasjonen og skal ikke endres.

### Hindre generisk `btusb` i å ta kontrolleren

Opprett:

```bash
sudo tee /etc/modprobe.d/belabox-bluetooth.conf >/dev/null <<'EOF'
blacklist btusb
EOF
```

Reboot er den enkleste måten å få korrekt driverbinding permanent:

```bash
sudo reboot
```

Etter boot kan bindingen verifiseres med:

```bash
readlink -f /sys/class/bluetooth/hci0/device/driver
lsmod | grep -E 'rtk_btusb|btusb'
```

Bekreftet korrekt resultat på BELABOX:

```text
/sys/bus/usb/drivers/rtk_btusb
```

Generisk `btusb` skal ikke eie `hci0`.

## 3. Par og trust WPS200

Kjør:

```bash
bluetoothctl
```

Deretter inne i `bluetoothctl`:

```text
power on
scan on
pair <WPS200_MAC>
trust <WPS200_MAC>
connect <WPS200_MAC>
scan off
quit
```

Kontroller:

```bash
bluetoothctl info <WPS200_MAC> | grep -E 'Paired|Trusted|Connected'
```

Forventet når høyttaleren er på:

```text
Paired: yes
Trusted: yes
Connected: yes
```

## 4. WirePlumber 0.4.8 - headless BlueZ uten aktiv login-session

### Symptomet

Før denne endringen startet Bluetooth, `user@1000`, PipeWire, WirePlumber og watchdog ved boot, men WPS200 ville ikke koble seg til før noen logget inn med SSH.

Watchdog-loggen viste gjentatte feil:

```text
Failed to connect: org.bluez.Error.Failed br-connection-profile-unavailable
```

BlueZ viste samtidig:

```text
a2dp-sink profile connect failed ... Protocol not available
```

I samme øyeblikk som SSH-login skjedde registrerte WirePlumber A2DP-endepunktene, og neste connect-forsøk lyktes.

Årsaken på WirePlumber `0.4.8-4` var at BlueZ-monitoren lastet `logind` direkte fra `30-bluez-monitor.lua`. For denne dedikerte headless-boksen må Bluetooth-monitoren kunne kjøre uten krav om en aktiv logind-seat.

### Deaktiver logind for BlueZ-monitoren

Opprett:

```bash
mkdir -p ~/.config/wireplumber/bluetooth.lua.d

cat > ~/.config/wireplumber/bluetooth.lua.d/80-disable-logind.lua <<'EOF'
bluez_monitor.properties["with-logind"] = false
EOF
```

Kopier distroens BlueZ-monitor lokalt, slik at `/usr/share` ikke redigeres direkte:

```bash
cp /usr/share/wireplumber/bluetooth.lua.d/30-bluez-monitor.lua \
   ~/.config/wireplumber/bluetooth.lua.d/30-bluez-monitor.lua
```

Patch bare `load_optional_module("logind")`-linjen:

```bash
python3 - <<'PY'
from pathlib import Path

p = Path.home() / ".config/wireplumber/bluetooth.lua.d/30-bluez-monitor.lua"
s = p.read_text()

old = '  load_optional_module("logind")'
new = '''  if bluez_monitor.properties["with-logind"] then
    load_optional_module("logind")
  end'''

if old not in s:
    raise SystemExit("Fant ikke forventet logind-linje - ingen endring gjort")

p.write_text(s.replace(old, new, 1))
PY
```

Restart WirePlumber:

```bash
systemctl --user restart wireplumber.service
```

## 5. System-watchdog for WPS200

Linger sørger for at brukerens systemd-manager kan starte uten login. I tillegg brukes en systemtjeneste som:

- starter sammen med BlueZ og `user@1000.service`
- gir PipeWire/WirePlumber noen sekunder til å komme opp
- restarter WirePlumber gjennom brukerens systemd-manager
- forsøker å koble WPS200 til igjen dersom den ikke er tilkoblet
- fortsetter å overvåke forbindelsen under bruk

Opprett watchdog-scriptet:

```bash
sudo tee /usr/local/sbin/cometen-wps200-watchdog >/dev/null <<'EOF'
#!/bin/bash

MAC="<WPS200_MAC>"

# Gi user-systemd/PipeWire litt tid til å komme opp.
sleep 3

# Sørg for at WirePlumber registrerer Bluetooth/A2DP-endepunktene.
systemctl --user --machine=user@.host restart wireplumber.service || true

sleep 3

while true; do
    if ! /usr/bin/bluetoothctl info "$MAC" 2>/dev/null | /usr/bin/grep -q "Connected: yes"; then
        /usr/bin/bluetoothctl power on >/dev/null 2>&1 || true
        /usr/bin/timeout 8 /usr/bin/bluetoothctl connect "$MAC" || true
    fi
    sleep 5
done
EOF

sudo chmod +x /usr/local/sbin/cometen-wps200-watchdog
```

Opprett systemd-tjenesten:

```bash
sudo tee /etc/systemd/system/cometen-wps200.service >/dev/null <<'EOF'
[Unit]
Description=Cometen WPS200 Bluetooth Auto Connect
Wants=bluetooth.service user@1000.service
After=bluetooth.service user@1000.service

[Service]
Type=simple
ExecStart=/usr/local/sbin/cometen-wps200-watchdog
Restart=always
RestartSec=3

[Install]
WantedBy=multi-user.target
EOF
```

Aktiver:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now cometen-wps200.service
```

Status:

```bash
sudo systemctl status cometen-wps200.service --no-pager
```

## 6. Installer CometenIRLAlerts som brukertjeneste

Receiver-oppsettet ligger i `receiver/`.

```bash
cd ~/CometenIRLAlerts/receiver
bash ./install-user-service.sh
sudo loginctl enable-linger user
```

Kontroller:

```bash
systemctl --user is-enabled pipewire.service
systemctl --user is-enabled wireplumber.service
systemctl --user is-enabled cometen-irl-alerts.service
```

Alle tre skal være `enabled`.

## 7. Stille PipeWire-keepalive

På denne BELABOX-versjonen feilet den opprinnelige keepalive-kommandoen:

```text
pw-cat --playback --rate=48000 --channels=2 --format=s16 -
```

med:

```text
error: failed to open audio file "-": Format not recognised.
```

Løsningen ble en ekte stille PCM WAV-fil som spilles kontinuerlig med `pw-play`.

Lag `receiver/sounds/keepalive.wav`:

```bash
cd ~/CometenIRLAlerts/receiver

python3 - <<'PY'
import wave
from pathlib import Path

path = Path("sounds/keepalive.wav")
rate = 48000
seconds = 30
channels = 2
sample_width = 2

with wave.open(str(path), "wb") as w:
    w.setnchannels(channels)
    w.setsampwidth(sample_width)
    w.setframerate(rate)
    w.writeframes(b"\x00" * rate * seconds * channels * sample_width)

print(path)
PY
```

I lokal `receiver/config.json` brukes:

```json
{
  "audio_keepalive_enabled": true,
  "audio_keepalive_command": "bash -c 'while true; do pw-play /home/user/CometenIRLAlerts/receiver/sounds/keepalive.wav || sleep 1; done'",
  "audio_keepalive_input": "/dev/zero",
  "audio_keepalive_restart_seconds": 5
}
```

Ikke legg den virkelige `config.json` i GitHub - den inneholder receiver-token og lokal relay-konfigurasjon.

Restart receiveren etter endring:

```bash
systemctl --user restart cometen-irl-alerts.service
```

## 8. PipeWire sink og volum

Når WPS200 er koblet til:

```bash
wpctl status
```

Sink-ID er dynamisk og skal ikke hardkodes i tjenester eller scripts.

På den testede WPS200-en ble omtrent `0.45` brukt som egnet volum. Sett volum med aktuell sink-ID:

```bash
wpctl set-volume <SINK_ID> 0.45
```

## 9. Verifiser ekte headless boot

Den viktige testen er å teste uten SSH-login:

1. Reboot eller slå strømmen helt av/på.
2. Ikke logg inn lokalt eller via SSH.
3. Vent omtrent 30-60 sekunder.
4. Kontroller at WPS200 kobler seg til automatisk.
5. Send Follow/Sub-test fra CometenWebAdmin.
6. Alerten skal spilles lokalt på WPS200.

Dette er bekreftet fungerende på Cometen BELABOX.

Hvis testen feiler, kan man logge inn etterpå og lese boot-loggene:

```bash
sudo journalctl -b -u cometen-wps200.service --no-pager
sudo journalctl -b -u user@1000.service --no-pager
sudo journalctl -b _UID=1000 --no-pager | \
  grep -iE 'wireplumber|bluez|logind|seat|bluetooth'
```

Typisk feil før headless-fiksen:

```text
br-connection-profile-unavailable
```

## 10. Bekreftet sluttresultat

Etter oppsettet over er arbeidsflyten:

```text
Sett strøm på BELABOX
        -> Bluetooth-kontrolleren starter med rtk_btusb
        -> user@1000 starter uten login
        -> PipeWire/WirePlumber starter
        -> WPS200-watchdog kobler til høyttaleren
        -> stille keepalive holder lydveien aktiv
        -> CometenIRLAlerts receiver poller relay
        -> WebAdmin/Streamer.bot alerts spilles på WPS200
```

Ingen SSH-login er nødvendig for normal IRL-bruk.
