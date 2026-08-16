<?php

declare(strict_types=1);

require __DIR__ . '/bootstrap.php';

require_method('POST');
$config = load_config();
require_token((string)($config['receiver_token'] ?? ''));
$payload = json_body();

$receiverId = strtolower(clean_text($payload['receiver_id'] ?? 'belabox', 64));
$version = clean_text($payload['version'] ?? '', 32);

if ($receiverId === '' || preg_match('/^[a-z0-9._-]{1,64}$/', $receiverId) !== 1) {
    json_response(422, ['ok' => false, 'error' => 'invalid_receiver_id']);
}

$pdo = database($config);

try {
    $pdo->exec(
        "CREATE TABLE IF NOT EXISTS irl_receiver_status (
            receiver_id VARCHAR(64) NOT NULL,
            last_seen DATETIME(6) NOT NULL,
            version VARCHAR(32) NOT NULL DEFAULT '',
            last_ip VARCHAR(45) NOT NULL DEFAULT '',
            PRIMARY KEY (receiver_id)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"
    );

    $statement = $pdo->prepare(
        "INSERT INTO irl_receiver_status (receiver_id, last_seen, version, last_ip)
         VALUES (:receiver_id, UTC_TIMESTAMP(6), :version, :last_ip)
         ON DUPLICATE KEY UPDATE
            last_seen = VALUES(last_seen),
            version = VALUES(version),
            last_ip = VALUES(last_ip)"
    );

    $statement->execute([
        ':receiver_id' => $receiverId,
        ':version' => $version,
        ':last_ip' => clean_text($_SERVER['REMOTE_ADDR'] ?? '', 45),
    ]);
} catch (Throwable $exception) {
    error_log('CometenIRL heartbeat error: ' . $exception->getMessage());
    json_response(500, ['ok' => false, 'error' => 'heartbeat_failed']);
}

json_response(200, [
    'ok' => true,
    'receiver_id' => $receiverId,
    'time_utc' => gmdate(DATE_ATOM),
]);
