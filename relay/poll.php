<?php

declare(strict_types=1);

require __DIR__ . '/bootstrap.php';

require_method('GET');
$config = load_config();
require_token((string)($config['receiver_token'] ?? ''));

$limit = max(1, min(10, (int)($_GET['limit'] ?? 5)));
$leaseSeconds = max(10, min(120, (int)($config['lease_seconds'] ?? 30)));
$pdo = database($config);

try {
    $pdo->beginTransaction();

    $sql = <<<SQL
SELECT id, event_type, user_name, amount, message, sound_file, priority, created_at, expires_at
FROM irl_alert_events
WHERE acknowledged_at IS NULL
  AND expires_at > UTC_TIMESTAMP(6)
  AND (leased_until IS NULL OR leased_until < UTC_TIMESTAMP(6))
ORDER BY priority DESC, created_at ASC
LIMIT {$limit}
FOR UPDATE
SQL;

    $events = $pdo->query($sql)->fetchAll();
    $updateSql = <<<SQL
UPDATE irl_alert_events
SET lease_id = :lease_id,
    leased_until = DATE_ADD(UTC_TIMESTAMP(6), INTERVAL {$leaseSeconds} SECOND)
WHERE id = :id
SQL;
    $update = $pdo->prepare($updateSql);

    foreach ($events as &$event) {
        $leaseId = bin2hex(random_bytes(16));
        $update->execute([
            ':lease_id' => $leaseId,
            ':id' => $event['id'],
        ]);

        $event = [
            'id' => $event['id'],
            'lease_id' => $leaseId,
            'type' => $event['event_type'],
            'user' => $event['user_name'],
            'amount' => (int)$event['amount'],
            'message' => $event['message'],
            'sound' => $event['sound_file'],
            'priority' => (int)$event['priority'],
            'created_at' => $event['created_at'] . 'Z',
            'expires_at' => $event['expires_at'] . 'Z',
        ];
    }
    unset($event);

    $pdo->commit();
} catch (Throwable $exception) {
    if ($pdo->inTransaction()) {
        $pdo->rollBack();
    }
    error_log('CometenIRL poll error: ' . $exception->getMessage());
    json_response(500, ['ok' => false, 'error' => 'poll_failed']);
}

json_response(200, ['ok' => true, 'events' => $events]);
