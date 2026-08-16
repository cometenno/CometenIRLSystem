<?php

declare(strict_types=1);

return [
    'database' => [
        'dsn' => 'mysql:host=localhost;dbname=cometen_irl_alerts;charset=utf8mb4',
        'username' => 'CHANGE_ME',
        'password' => 'CHANGE_ME',
    ],

    // Use two different long random values.
    'sender_token' => 'CHANGE_ME_TO_A_LONG_RANDOM_SENDER_TOKEN',
    'receiver_token' => 'CHANGE_ME_TO_A_LONG_RANDOM_RECEIVER_TOKEN',

    // Events older than this are never delivered.
    'event_ttl_seconds' => 90,

    // A polled event is reserved for this long before it can be delivered again.
    'lease_seconds' => 30,

    // Heartbeat is diagnostic only. Default receiver heartbeat is every 30 seconds.
    // Mark the receiver offline only after three missed heartbeats.
    'receiver_offline_seconds' => 90,
];
