<?php

declare(strict_types=1);

require __DIR__ . '/bootstrap.php';

require_method('GET');
$config = load_config();

try {
    database($config)->query('SELECT 1');
} catch (Throwable) {
    json_response(503, ['ok' => false, 'status' => 'database_unavailable']);
}

json_response(200, [
    'ok' => true,
    'status' => 'ready',
    'time_utc' => gmdate(DATE_ATOM),
]);
