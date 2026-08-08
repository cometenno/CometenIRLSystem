<?php

declare(strict_types=1);

require __DIR__ . '/bootstrap.php';

$config = load_config();
$method = $_SERVER['REQUEST_METHOD'] ?? '';

if ($method === 'POST') {
    require_token((string)($config['receiver_token'] ?? ''));
    $payload = json_body();

    $id = clean_text($payload['id'] ?? '', 100);
    $ok = filter_var($payload['ok'] ?? false, FILTER_VALIDATE_BOOLEAN);
    $message = clean_text($payload['message'] ?? '', 220);

    if ($id === '' || preg_match('/^[A-Za-z0-9._:-]{8,100}$/', $id) !== 1) {
        json_response(422, ['ok' => false, 'error' => 'invalid_event_id']);
    }

    if ($message === '') {
        $message = $ok ? 'Command completed' : 'Command failed';
    }

    $storedMessage = ($ok ? 'RESULT_OK:' : 'RESULT_ERR:') . $message;

    try {
        $statement = database($config)->prepare(
            'UPDATE irl_alert_events
             SET message = :message
             WHERE id = :id
               AND event_type = \'control\'
               AND acknowledged_at IS NOT NULL'
        );
        $statement->execute([
            ':message' => $storedMessage,
            ':id' => $id,
        ]);
    } catch (Throwable $exception) {
        error_log('CometenIRL control result write error: ' . $exception->getMessage());
        json_response(500, ['ok' => false, 'error' => 'result_write_failed']);
    }

    if ($statement->rowCount() !== 1) {
        json_response(409, ['ok' => false, 'error' => 'event_not_acknowledged']);
    }

    json_response(200, ['ok' => true, 'event_id' => $id]);
}

if ($method === 'GET') {
    require_token((string)($config['sender_token'] ?? ''));
    $id = clean_text($_GET['id'] ?? '', 100);

    if ($id === '' || preg_match('/^[A-Za-z0-9._:-]{8,100}$/', $id) !== 1) {
        json_response(422, ['ok' => false, 'error' => 'invalid_event_id']);
    }

    try {
        $statement = database($config)->prepare(
            'SELECT message
             FROM irl_alert_events
             WHERE id = :id
               AND event_type = \'control\'
             LIMIT 1'
        );
        $statement->execute([':id' => $id]);
        $row = $statement->fetch();
    } catch (Throwable $exception) {
        error_log('CometenIRL control result read error: ' . $exception->getMessage());
        json_response(500, ['ok' => false, 'error' => 'result_read_failed']);
    }

    if (!is_array($row)) {
        json_response(200, ['ok' => true, 'ready' => false]);
    }

    $storedMessage = (string)($row['message'] ?? '');
    $resultOk = null;
    $message = '';

    if (str_starts_with($storedMessage, 'RESULT_OK:')) {
        $resultOk = true;
        $message = substr($storedMessage, strlen('RESULT_OK:'));
    } elseif (str_starts_with($storedMessage, 'RESULT_ERR:')) {
        $resultOk = false;
        $message = substr($storedMessage, strlen('RESULT_ERR:'));
    }

    if ($resultOk === null) {
        json_response(200, ['ok' => true, 'ready' => false]);
    }

    json_response(200, [
        'ok' => true,
        'ready' => true,
        'result_ok' => $resultOk,
        'message' => $message,
    ]);
}

json_response(405, ['ok' => false, 'error' => 'method_not_allowed']);
