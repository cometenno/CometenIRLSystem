<?php

declare(strict_types=1);

require __DIR__ . '/bootstrap.php';

require_method('GET');
$config = load_config();
require_token((string)($config['sender_token'] ?? ''));

$receiverId = strtolower(clean_text($_GET['receiver_id'] ?? 'belabox', 64));
if ($receiverId === '' || preg_match('/^[a-z0-9._-]{1,64}$/', $receiverId) !== 1) {
    json_response(422, ['ok' => false, 'error' => 'invalid_receiver_id']);
}

$offlineSeconds = max(3, min(30, (int)($config['receiver_offline_seconds'] ?? 5)));
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
        "SELECT
            receiver_id,
            last_seen,
            version,
            TIMESTAMPDIFF(MICROSECOND, last_seen, UTC_TIMESTAMP(6)) / 1000000.0 AS age_seconds
         FROM irl_receiver_status
         WHERE receiver_id = :receiver_id"
    );
    $statement->execute([':receiver_id' => $receiverId]);
    $row = $statement->fetch();
} catch (Throwable $exception) {
    error_log('CometenIRL receiver status error: ' . $exception->getMessage());
    json_response(500, ['ok' => false, 'error' => 'receiver_status_failed']);
}

if (!$row) {
    json_response(200, [
        'ok' => true,
        'receiver_id' => $receiverId,
        'online' => false,
        'age_seconds' => null,
        'last_seen_utc' => null,
        'offline_after_seconds' => $offlineSeconds,
    ]);
}

$ageSeconds = max(0.0, (float)$row['age_seconds']);
$online = $ageSeconds <= $offlineSeconds;

json_response(200, [
    'ok' => true,
    'receiver_id' => $receiverId,
    'online' => $online,
    'age_seconds' => round($ageSeconds, 3),
    'last_seen_utc' => str_replace(' ', 'T', (string)$row['last_seen']) . 'Z',
    'offline_after_seconds' => $offlineSeconds,
    'version' => (string)$row['version'],
]);
