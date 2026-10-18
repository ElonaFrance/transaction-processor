-- Transaction Aggregation System — Schema
-- MySQL 8.0

CREATE DATABASE IF NOT EXISTS transaction_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE transaction_db;

CREATE USER IF NOT EXISTS 'txuser'@'%' IDENTIFIED BY 'txpassword';
GRANT ALL PRIVILEGES ON transaction_db.* TO 'txuser'@'%';
FLUSH PRIVILEGES;

CREATE TABLE IF NOT EXISTS transactions (
    id              VARCHAR(36)     NOT NULL,
    client_id       VARCHAR(50)     NOT NULL,
    source_id       VARCHAR(100)    NOT NULL,
    source_type     VARCHAR(50)     NOT NULL,
    transaction_type VARCHAR(10)   NOT NULL,
    amount          DECIMAL(18,2)   NOT NULL,
    currency        VARCHAR(3)      NOT NULL DEFAULT 'ZAR',
    category_id     TINYINT         NOT NULL DEFAULT 99,
    category_name   VARCHAR(50)     NOT NULL DEFAULT 'Other',
    description     VARCHAR(500)    NOT NULL DEFAULT '',
    merchant_name   VARCHAR(200)    NOT NULL DEFAULT '',
    transaction_date DATETIME       NOT NULL,
    processed_date  DATETIME        NOT NULL,
    status          VARCHAR(20)     NOT NULL DEFAULT 'PROCESSED',
    created_at      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    INDEX idx_client_id (client_id),
    INDEX idx_category_id (category_id),
    INDEX idx_source_type (source_type),
    INDEX idx_transaction_date (transaction_date),
    INDEX idx_client_date (client_id, transaction_date DESC),
    INDEX idx_client_category (client_id, category_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS transaction_aggregations (
    id              BIGINT          NOT NULL AUTO_INCREMENT,
    client_id       VARCHAR(50)     NOT NULL,
    period          VARCHAR(20)     NOT NULL COMMENT 'ALL_TIME or MONTHLY',
    period_key      VARCHAR(20)     NOT NULL COMMENT 'ALL or yyyy-MM',
    total_debit     DECIMAL(18,2)   NOT NULL DEFAULT 0.00,
    total_credit    DECIMAL(18,2)   NOT NULL DEFAULT 0.00,
    transaction_count INT           NOT NULL DEFAULT 0,
    last_updated    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uq_client_period (client_id, period, period_key),
    INDEX idx_agg_client_id (client_id),
    INDEX idx_agg_period (period, period_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
