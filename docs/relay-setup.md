# Oppsett av relay på webhotellet

Sist oppdatert: 16. august 2026.

## Krav

- PHP 8.1 eller nyere
- MySQL eller MariaDB med InnoDB
- HTTPS-sertifikat
- egen mappe på webhotellet, for eksempel `/CometenIRLAlerts_Relay/`

## 1. Opprett database

Importer:

```text
relay/database.sql
```

Databasen brukes både til alert-events og receiver/heartbeat-status.

## 2. Last opp PHP-filene

Last opp innholdet i `relay/` til webhotellet.

Minstekrav:

```text
acknowledge.php
bootstrap.php
control_result.php
health.php
heartbeat.php
poll.php
push.php
receiver_status.php
config.php
```

`database.sql` og `config.example.php` trenger ikke ligge offentlig etter installasjonen.

## 3. Lag `config.php`

Kopier `config.example.php` til `config.php` og fyll inn database og token.

Eksempel på relevante verdier:

```php
'event_ttl_seconds' => 90,
'lease_seconds' => 30,
'receiver_offline_seconds' => 90,
```

`receiver_offline_seconds=90` er tilpasset heartbeat hvert 30. sekund og gir tre tapte heartbeat før receiveren regnes offline.

Bruk to forskjellige lange token:

```text
sender_token
receiver_token
```

`config.php` skal aldri committes.

## 4. Test relay

Åpne:

```text
https://DITT-DOMENE/CometenIRLAlerts_Relay/health.php
```

Forventet svar:

```json
{"ok":true,"status":"ready"}
```

## 5. Heartbeat og HTTP 429

`relay/heartbeat.php` har ingen intern rate-limit. Under test ble 1 heartbeat per sekund blokkert av nginx/webhotellet med:

```text
429 Too Many Requests
```

Derfor er standard heartbeat nå 30 sekunder. Klienten har også backoff ved 429.

Hvis 429 fortsatt oppstår, kontroller først at receiveren faktisk starter med:

```text
interval=30.0s
```

Ikke øk trafikken fra heartbeat for å gjøre status raskere. Primær video-status skal komme fra BELABOX ingest-watchdog, ikke heartbeat.

## 6. Oppdater relay etter GitHub-endringer

`git pull` på ROCK 5B+ oppdaterer **ikke** webhotellet.

Når filer i `relay/` endres, må de nye PHP-filene lastes opp til webhotellet manuelt/FTP.

Ved heartbeat-endringen 16. august 2026 må minst oppdatert:

```text
receiver_status.php
```

og `config.php` skal ha:

```php
'receiver_offline_seconds' => 90,
```

## 7. URL som brukes videre

Relay base URL er mappen uten filnavn:

```text
https://DITT-DOMENE/CometenIRLAlerts_Relay
```

Denne brukes av Streamer.bot, alert-receiver og heartbeat.

## Sikkerhet

- bruk HTTPS
- ikke deaktiver TLS-validering
- unnta API-mappen fra caching dersom webhotellet/proxyen cacher dynamiske svar
- ikke eksponer `config.php`, tokens eller databasepassord
