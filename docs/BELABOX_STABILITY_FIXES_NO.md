# BELABOX stabilitetsfikser - 2026-08-14

Før neste langtest skal to endringer legges inn:

1. Hysterese på vifta.
2. Persistent systemjournal over reboot/strømbrudd.

## 1. Viftehysterese

Eksisterende viftekurve beholdes:

- under 43 C: state 0
- 43-49 C: state 1
- 50-56 C: state 2
- 57-64 C: state 3
- 65 C og høyere: state 4

Problemet var at temperaturen lå rundt 42-43 C og vifta dermed kunne bytte state 0/1 svært ofte. Ny kontroll bruker hysterese på av/på-overgangen:

- Vifta starter state 1 ved 43 C.
- Når vifta først går, stopper den ikke før temperaturen er under 40 C.
- Om temperaturen er 40-42 C og vifta allerede er av, forblir den av.
- Høyere trinn 2-4 er uendret.

Ferdig kontrollfil ligger i `belabox/cometen-fan-control` og installeres over `/usr/local/sbin/cometen-fan-control`. Installer tar først timestampet backup av eksisterende fil.

## 2. Persistent journald

`belabox/99-cometen-persistent-journal.conf` setter:

```ini
[Journal]
Storage=persistent
```

Installer oppretter også `/var/log/journal`, slik at journal fra forrige boot skal være tilgjengelig etter normal reboot og så langt som er skrevet til disk før et strømbrudd.

## Installering

På BELABOX:

```bash
cd ~/CometenIRLAlerts
git pull --ff-only
sudo bash belabox/install-stability-fixes.sh
```

Installer restarter kun `systemd-journald` og `cometen-fan.service`. Den restarter ikke belaUI, NetworkManager, SRTLA eller stream-pipelinen.

## Kontroll rett etter installering

Installer skriver ut:

- om `cometen-fan.service` er active
- aktiv journald Storage-innstilling
- om `/var/log/journal` finnes
- SoC-temperatur
- aktuell fan state
- journalens diskbruk

Ekstra kontroll:

```bash
systemctl status cometen-fan.service --no-pager
journalctl -t cometen-fan -n 30 --no-pager
journalctl --list-boots
```

## Reboot-test

Når det passer å avbryte testen:

```bash
sudo reboot
```

Etter oppstart:

```bash
journalctl --list-boots
journalctl -b -1 -n 50 --no-pager
systemctl is-active cometen-fan.service
systemctl is-active cometen-wps200.service
sudo -u user XDG_RUNTIME_DIR=/run/user/1000 systemctl --user is-active cometen-irl-alerts.service
```

Hvis `journalctl -b -1` viser forrige boot, er persistent journal verifisert.

Deretter gjøres en langtest uten flere endringer, slik at eventuelle nye feil kan sammenlignes mot denne baselinen.
