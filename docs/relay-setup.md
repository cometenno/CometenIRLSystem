# Oppsett av relay på webhotellet

## Krav

- PHP 8.1 eller nyere
- MySQL eller MariaDB med InnoDB
- HTTPS-sertifikat
- Egen mappe på webhotellet, for eksempel `/irl-alerts/`

## 1. Opprett database

Importer `relay/database.sql` i databasen som skal brukes.

## 2. Last opp PHP-filene

Last opp innholdet i `relay/` til ønsket mappe på webhotellet.

Eksempel:

```text
public_html/irl-alerts/
├── acknowledge.php
├── bootstrap.php
├── config.php
├── health.php
├── poll.php
└── push.php
```

`database.sql` og `config.example.php` trenger ikke ligge offentlig etter installasjonen.

## 3. Lag konfigurasjonen

Kopier `config.example.php` til `config.php` og fyll inn:

- PDO DSN
- databasenavn
- databasebruker
- databasepassord
- sender-token
- receiver-token

Bruk to forskjellige tilfeldige tokens på minst 32 tegn.

`config.php` er ignorert av Git og skal aldri committes.

## 4. Test relayen

Åpne:

```text
https://DITT-DOMENE/irl-alerts/health.php
```

Forventet svar:

```json
{"ok":true,"status":"ready"}
```

## 5. URL som brukes videre

Relay base URL er mappen uten filnavn:

```text
https://DITT-DOMENE/irl-alerts
```

Denne legges inn både i Streamer.bot og i receiverens `config.json`.

## Viktig

Ikke deaktiver HTTPS-validering. Hvis webhotellet bruker proxy eller cache, må API-mappen unntas fra caching.
