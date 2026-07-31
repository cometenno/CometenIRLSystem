# Oppsett av receiver på ROCK 5B+

Receiveren skal installeres ved siden av BELABOX og ikke inne i BELABOX-koden.

## 1. Kopier receiver-mappen

Klon repositoryet eller kopier `receiver/` til ROCK 5B+.

## 2. Installer

```bash
cd receiver
chmod +x install.sh uninstall.sh receiver.py
sudo ./install.sh
```

Installasjonen bruker brukeren som kjørte `sudo` som servicebruker og legger filene i:

```text
/opt/cometen-irl-alerts/
```

## 3. Konfigurer

Rediger:

```bash
sudo nano /opt/cometen-irl-alerts/config.json
```

Sett:

- `relay_base_url`
- `receiver_token`
- ønsket pollingintervall
- lydfilene som skal brukes

## 4. Legg inn lokale lydfiler

Legg korte PCM WAV-filer i:

```text
/opt/cometen-irl-alerts/sounds/
```

Test først med `test.wav`.

## 5. Test lydutgangen manuelt

Receiveren prøver i denne rekkefølgen:

1. `pw-play`
2. `paplay`
3. `aplay`

Test den spilleren som finnes på systemet, for eksempel:

```bash
pw-play /opt/cometen-irl-alerts/sounds/test.wav
```

Bluetooth-oppsett og automatisk reconnect ferdigstilles når ROCK 5B+, BELABOX-imaget og høyttaleren kan testes fysisk. Ulike BELABOX-images kan bruke forskjellig lydstakk.

## 6. Start tjenesten

```bash
sudo systemctl start cometen-irl-alerts.service
sudo systemctl status cometen-irl-alerts.service
```

Følg loggen:

```bash
journalctl -u cometen-irl-alerts.service -f
```

## Avinstallering

```bash
sudo ./uninstall.sh
```

Avinstalleringen fjerner tjenesten, men beholder konfigurasjon og lydfiler i `/opt/cometen-irl-alerts/`.
