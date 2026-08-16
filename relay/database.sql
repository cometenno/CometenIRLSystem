CREATE TABLE IF NOT EXISTS irl_alert_events (
    id VARCHAR(100) NOT NULL,
    event_type VARCHAR(32) NOT NULL,
    user_name VARCHAR(100) NOT NULL DEFAULT '',
    amount INT NOT NULL DEFAULT 0,
    message VARCHAR(250) NOT NULL DEFAULT '',
    sound_file VARCHAR(100) NOT NULL DEFAULT '',
    priority SMALLINT NOT NULL DEFAULT 0,
    created_at DATETIME(6) NOT NULL,
    expires_at DATETIME(6) NOT NULL,
    lease_id VARCHAR(64) NULL,
    leased_until DATETIME(6) NULL,
    acknowledged_at DATETIME(6) NULL,
    PRIMARY KEY (id),
    INDEX idx_delivery (acknowledged_at, expires_at, leased_until, priority, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS irl_receiver_status (
    receiver_id VARCHAR(64) NOT NULL,
    last_seen DATETIME(6) NOT NULL,
    version VARCHAR(32) NOT NULL DEFAULT '',
    last_ip VARCHAR(45) NOT NULL DEFAULT '',
    PRIMARY KEY (receiver_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
