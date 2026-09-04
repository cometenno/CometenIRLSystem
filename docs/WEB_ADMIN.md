# BELABOX Web Admin

Cometen IRL System includes a small authenticated web panel that uses the existing relay as the transport between the web hotel and BELABOX.

The panel does **not** SSH into BELABOX and it does not expose sender/receiver relay tokens to the browser.

## Features

- BELABOX online/offline heartbeat
- expanded system status (`CPU`, BME280 enclosure temperature, fan PWM, video/encoder, Wi-Fi, uptime)
- Browser Audio / Sound Alerts URL
  - read current URL
  - save/update URL
  - master on/off
  - restart Browser Audio sources
  - remove Sound Alerts source
- Bluetooth
  - list paired devices
  - scan for nearby devices
  - pair/trust
  - connect/disconnect
  - remove
  - set a paired device as the default BELABOX speaker
- simple audio controls
  - set volume
  - mute/unmute
  - play local test alert

## Architecture

```text
Browser
  |
  | HTTPS + admin session/CSRF
  v
relay/admin.php on web hotel
  |
  | existing irl_alert_events control queue
  v
BELABOX receiver/run_receiver.py
  |
  +--> Browser Audio config.json (unprivileged)
  |
  +--> sudo -n /usr/local/sbin/cometen-irl-admin-helper
         |
         +--> bluetoothctl
         +--> /etc/default/cometen-wps200
         +--> cometen-wps200.service
```

`admin.php` uses an allow-list of commands. Arbitrary shell commands cannot be submitted from the browser.

## 1. Install/update BELABOX receiver files

The receiver needs:

```text
receiver/run_receiver.py
receiver/admin_control.py
```

The receiver service remains the existing user service:

```bash
systemctl --user restart cometen-irl-alerts.service
```

## 2. Install the privileged Bluetooth helper

From the repository checkout on BELABOX:

```bash
cd ~/CometenIRLAlerts
sudo bash belabox/install-admin-helper.sh
```

Installed files:

```text
/usr/local/sbin/cometen-irl-admin-helper
/etc/sudoers.d/cometen-irl-admin-helper
```

The sudo rule permits the `user` account to run only the validated admin helper as root. The helper itself accepts only its fixed Bluetooth action list and validates MAC addresses.

Test manually:

```bash
sudo -n /usr/local/sbin/cometen-irl-admin-helper status
sudo -n /usr/local/sbin/cometen-irl-admin-helper list
```

## 3. Deploy the web panel

Upload/copy this file into the same live relay directory that already contains `push.php`, `poll.php`, `control_result.php`, and `config.php`:

```text
relay/admin.php
```

For the current legacy production path, keeping the old relay directory name is fine. There is no requirement to rename a working `CometenIRLAlerts_Relay` directory.

The resulting URL is normally:

```text
https://YOUR-HOST/CometenIRLAlerts_Relay/admin.php
```

Use HTTPS.

## 4. Configure admin login

Do not commit the real password or password hash to GitHub.

Generate a password hash on any machine with PHP:

```bash
php -r 'echo password_hash("YOUR-ADMIN-PASSWORD", PASSWORD_DEFAULT), PHP_EOL;'
```

Add the resulting hash to the live relay `config.php`:

```php
'admin' => [
    'username' => 'cometen',
    'password_hash' => '$2y$...',
    'control_ttl_seconds' => 45,
],
```

The admin panel is disabled until a non-empty password hash is configured.

## Browser Audio behavior

The web panel manages the existing `soundalerts` Browser Audio source. Saving a URL sends:

```text
browser_audio_add soundalerts https://...
```

The Browser Audio supervisor already reloads `config.json` continuously, so a full BELABOX reboot is not required when the URL changes.

## Bluetooth default device

Selecting **Sett standard** performs two coordinated changes:

1. the root helper writes the selected MAC/name to `/etc/default/cometen-wps200` and restarts the existing Bluetooth watchdog;
2. the receiver updates these fields in `receiver/config.json`:

```text
remote_audio_sink_match
browser_audio_sink_match
status_leds.bluetooth_sink_match
```

The legacy service/file names containing `wps200` are kept for compatibility, but the selected device can be any paired Bluetooth audio device.

## Security notes

- Use HTTPS.
- `relay/config.php` must not be publicly downloadable.
- The browser never receives relay sender/receiver tokens.
- Admin login uses PHP `password_verify()` and session cookies with `HttpOnly` and `SameSite=Strict`.
- State-changing API calls require a session CSRF token.
- Bluetooth root access is isolated behind one allow-listed helper.
- Destructive Bluetooth removal requires confirmation in the UI.
