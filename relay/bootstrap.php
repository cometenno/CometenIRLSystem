<?php

declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

function json_response(int $status, array $payload): never
{
    http_response_code($status);
    echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

function require_method(string $method): void
{
    if (($_SERVER['REQUEST_METHOD'] ?? '') !== $method) {
        json_response(405, ['ok' => false, 'error' => 'method_not_allowed']);
    }
}

function load_config(): array
{
    $path = __DIR__ . '/config.php';
    if (!is_file($path)) {
        json_response(500, ['ok' => false, 'error' => 'relay_not_configured']);
    }

    $config = require $path;
    if (!is_array($config)) {
        json_response(500, ['ok' => false, 'error' => 'invalid_config']);
    }

    return $config;
}

function database(array $config): PDO
{
    static $pdo = null;
    if ($pdo instanceof PDO) {
        return $pdo;
    }

    $db = $config['database'] ?? [];
    try {
        $pdo = new PDO(
            (string)($db['dsn'] ?? ''),
            (string)($db['username'] ?? ''),
            (string)($db['password'] ?? ''),
            [
                PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
                PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
                PDO::ATTR_EMULATE_PREPARES => false,
            ]
        );
    } catch (Throwable $exception) {
        error_log('CometenIRL relay database error: ' . $exception->getMessage());
        json_response(500, ['ok' => false, 'error' => 'database_unavailable']);
    }

    return $pdo;
}

function request_token(): string
{
    return trim((string)($_SERVER['HTTP_X_COMETEN_TOKEN'] ?? ''));
}

function require_token(string $expected): void
{
    $provided = request_token();
    if ($expected === '' || $provided === '' || !hash_equals($expected, $provided)) {
        json_response(401, ['ok' => false, 'error' => 'unauthorized']);
    }
}

function json_body(): array
{
    $raw = file_get_contents('php://input');
    if ($raw === false || trim($raw) === '') {
        json_response(400, ['ok' => false, 'error' => 'empty_body']);
    }

    try {
        $decoded = json_decode($raw, true, 32, JSON_THROW_ON_ERROR);
    } catch (JsonException) {
        json_response(400, ['ok' => false, 'error' => 'invalid_json']);
    }

    if (!is_array($decoded)) {
        json_response(400, ['ok' => false, 'error' => 'invalid_payload']);
    }

    return $decoded;
}

function clean_text(mixed $value, int $maxLength): string
{
    $text = trim((string)$value);
    $text = preg_replace('/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]/u', '', $text) ?? '';

    return function_exists('mb_substr')
        ? mb_substr($text, 0, $maxLength, 'UTF-8')
        : substr($text, 0, $maxLength);
}

function event_types(): array
{
    return [
        'test',
        'follow',
        'sub',
        'resub',
        'giftsub',
        'gifted',
        'giftbomb',
        'raid',
        'bits',
        'donation',
        'charity',
        'youtubesub',
        'yt_sub',
        'channelpoint',
        'moderator',
        'system',
    ];
}

function random_event_id(): string
{
    return 'evt-' . gmdate('Ymd-His') . '-' . bin2hex(random_bytes(8));
}
