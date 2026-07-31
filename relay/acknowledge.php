<?php

declare(strict_types=1);

require __DIR__ . '/bootstrap.php';

require_method('POST');
$config = load_config();
require_token((string)($config['receiver_token'] ?? ''));
$payload = json_body();

$id = clean_text($payload['id'] ?? '', 100);
$leaseId = clean_text($payload['lease_id'] ?? '', 64);

if ($id === '' || $leaseId === '') {
    json_response(422, ['ok' => false, 'error' => 'missing_event_or_lease']);
}

try {
    $statement = database($config)->prepare(
        'UPDATE irl_alert_events
         SET acknowledged_at = UTC_TIMESTAMP(6), leased_until = NULL
         WHERE id = :id
           AND lease_id = :lease_id
           AND acknowledged_at IS NULL'
    );
    $statement->execute([
        ':id' => $id,
        ':lease_id' => $leaseId,
    ]);
} catch (Throwable $exception) {
    error_log('CometenIRL acknowledge error: ' . $exception->getMessage());
    json_response(500, ['ok' => false, 'error' => 'acknowledge_failed']);
}

if ($statement->rowCount() !== 1) {
    json_response(409, ['ok' => false, 'error' => 'event_not_leased']);
}

json_response(200, ['ok' => true, 'event_id' => $id]);
