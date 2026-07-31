<?php

declare(strict_types=1);

require __DIR__ . '/bootstrap.php';

require_method('POST');
$config = load_config();
require_token((string)($config['sender_token'] ?? ''));
$payload = json_body();

$type = strtolower(clean_text($payload['type'] ?? '', 32));
if (!in_array($type, event_types(), true)) {
    json_response(422, ['ok' => false, 'error' => 'unsupported_event_type']);
}

$id = clean_text($payload['id'] ?? '', 100);
if ($id === '' || preg_match('/^[A-Za-z0-9._:-]{8,100}$/', $id) !== 1) {
    $id = random_event_id();
}

$userName = clean_text($payload['user'] ?? '', 100);
$message = clean_text($payload['message'] ?? '', 250);
$amount = max(0, min(1000000000, (int)($payload['amount'] ?? 0)));
$priority = max(-100, min(100, (int)($payload['priority'] ?? 0)));

$sound = clean_text($payload['sound'] ?? ($type . '.wav'), 100);
if (preg_match('/^[A-Za-z0-9_-]+\.wav$/', $sound) !== 1) {
    $sound = $type . '.wav';
}

$ttl = max(15, min(600, (int)($config['event_ttl_seconds'] ?? 90)));
$createdAt = new DateTimeImmutable('now', new DateTimeZone('UTC'));
$expiresAt = $createdAt->modify('+' . $ttl . ' seconds');

$sql = <<<'SQL'
INSERT INTO irl_alert_events
    (id, event_type, user_name, amount, message, sound_file, priority, created_at, expires_at)
VALUES
    (:id, :event_type, :user_name, :amount, :message, :sound_file, :priority, :created_at, :expires_at)
ON DUPLICATE KEY UPDATE id = VALUES(id)
SQL;

try {
    $statement = database($config)->prepare($sql);
    $statement->execute([
        ':id' => $id,
        ':event_type' => $type,
        ':user_name' => $userName,
        ':amount' => $amount,
        ':message' => $message,
        ':sound_file' => $sound,
        ':priority' => $priority,
        ':created_at' => $createdAt->format('Y-m-d H:i:s.u'),
        ':expires_at' => $expiresAt->format('Y-m-d H:i:s.u'),
    ]);
} catch (Throwable $exception) {
    error_log('CometenIRL push error: ' . $exception->getMessage());
    json_response(500, ['ok' => false, 'error' => 'event_not_queued']);
}

json_response(202, [
    'ok' => true,
    'event_id' => $id,
    'expires_at' => $expiresAt->format(DATE_ATOM),
]);
