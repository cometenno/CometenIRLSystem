# Relay setup

The relay is the HTTPS/PHP/MySQL middle layer between Streamer.bot and the BELABOX receiver.

## What the relay does

```text
Streamer.bot
   |
   | POST alert/control event
   v
push.php
   |
   v
MySQL/MariaDB event queue
   |
   | lease/poll
   v
poll.php -> BELABOX receiver
   |
   +--> acknowledge.php
   +--> control_result.php

BELABOX heartbeat
   |
   v
heartbeat.php -> receiver status
```

The relay does not contain Twitch, YouTube or OBS credentials.

## Requirements

- HTTPS
- PHP 8+
- MySQL or MariaDB
- InnoDB
- PDO MySQL support

## Database

Create a database and import:

```text
relay/database.sql
```

The schema contains the event/control queue and receiver status/heartbeat data used by the current system.

## Files to upload

Upload the relay directory to an HTTPS location. Production requires at least:

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

Use `config.example.php` as the template for the private `config.php`.

## Tokens

Use two different long random tokens:

- sender token - used by Streamer.bot/CometenWebAdmin senders
- receiver token - used by BELABOX polling/heartbeat

Generate tokens locally, for example:

```bash
python3 -c "import secrets; print(secrets.token_urlsafe(48))"
```

Never commit `relay/config.php`.

## Example private configuration

```php
<?php

declare(strict_types=1);

return [
    'database' => [
        'dsn' => 'mysql:host=localhost;dbname=DATABASE;charset=utf8mb4',
        'username' => 'DATABASE_USER',
        'password' => 'DATABASE_PASSWORD',
    ],

    'sender_token' => 'LONG_SENDER_TOKEN',
    'receiver_token' => 'LONG_RECEIVER_TOKEN',

    'event_ttl_seconds' => 90,
    'lease_seconds' => 30,
    'receiver_offline_seconds' => 90,
];
```

## Health test

Open:

```text
https://example.com/CometenIRLAlerts_Relay/health.php
```

Expected result: JSON reporting `ok=true`.

## Event lifecycle

1. sender posts an event with a unique ID to `push.php`
2. relay stores it with a TTL
3. receiver leases it through `poll.php`
4. receiver performs the action
5. receiver acknowledges the event through `acknowledge.php`
6. control actions may also store a result
7. Streamer.bot polls `control_result.php` and returns that result to chat

Unacknowledged events can become available again after the lease expires.

## Heartbeat

Recommended values:

```text
heartbeat interval: 30 seconds
receiver offline threshold: 90 seconds
```

A 1-second heartbeat interval caused HTTP 429 responses on the tested web host and must not be used.

## Browser Audio URL handling

`!irlaudio add <name> <url>` sends the private Browser Source URL temporarily as a short-lived control event. The receiver validates and stores it locally in gitignored `receiver/config.json`.

The URL must never be written to logs or echoed back to Twitch chat.

## Updating the relay

A `git pull` on BELABOX does not update the web host. If files in `relay/` change, upload only the changed PHP/SQL files required by that update.

Never overwrite the production `config.php` with the example file during an update.

## Troubleshooting

Check:

- `health.php`
- PHP error log
- database connectivity
- sender/receiver token mismatch
- HTTP 401/403 responses
- HTTP 429 rate limiting
- event TTL/lease values

Do not expose token values while troubleshooting.

## Related documentation

- [Installation](INSTALLATION.md)
- [Architecture](architecture.md)
- [Receiver setup](receiver-setup.md)
- [Remote Control](REMOTE_CONTROL.md)
