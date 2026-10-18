-- Transaction Aggregation System — Stored Procedures
-- MySQL 8.0

USE transaction_db;

DELIMITER $$

-- ============================================================
-- PROCESSOR PROCEDURES (write path)
-- ============================================================

DROP PROCEDURE IF EXISTS usp_UpsertAggregation$$

-- Inserts the transaction and updates its ALL_TIME + MONTHLY aggregations in one
-- transaction. Aggregations are only touched when the transaction is new, so
-- re-delivered or retried events never double-count.
DROP PROCEDURE IF EXISTS usp_InsertTransaction$$
CREATE PROCEDURE usp_InsertTransaction(
    IN Payload JSON
)
BEGIN
    DECLARE v_id            VARCHAR(36);
    DECLARE v_client_id     VARCHAR(50);
    DECLARE v_source_id     VARCHAR(100);
    DECLARE v_source_type   VARCHAR(50);
    DECLARE v_tx_type       VARCHAR(10);
    DECLARE v_amount        DECIMAL(18,2);
    DECLARE v_currency      VARCHAR(3);
    DECLARE v_category_id   TINYINT;
    DECLARE v_category_name VARCHAR(50);
    DECLARE v_description   VARCHAR(500);
    DECLARE v_merchant      VARCHAR(200);
    DECLARE v_tx_date       DATETIME;
    DECLARE v_proc_date     DATETIME;
    DECLARE v_status        VARCHAR(20);
    DECLARE v_debit         DECIMAL(18,2);
    DECLARE v_credit        DECIMAL(18,2);
    DECLARE v_existing      INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        DECLARE v_error_code    INT;
        DECLARE v_error_message TEXT;
        GET DIAGNOSTICS CONDITION 1 v_error_code = MYSQL_ERRNO, v_error_message = MESSAGE_TEXT;
        ROLLBACK;
        SELECT v_error_code AS ErrorCode, v_error_message AS ErrorMessage;
    END;

    SET v_id            = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.Id'));
    SET v_client_id     = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.ClientId'));
    SET v_source_id     = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.SourceId'));
    SET v_source_type   = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.SourceType'));
    SET v_tx_type       = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.TransactionType'));
    SET v_amount        = JSON_EXTRACT(Payload, '$.Amount');
    SET v_currency      = COALESCE(JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.Currency')), 'ZAR');
    SET v_category_id   = JSON_EXTRACT(Payload, '$.CategoryId');
    SET v_category_name = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.CategoryName'));
    SET v_description   = COALESCE(JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.Description')), '');
    SET v_merchant      = COALESCE(JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.MerchantName')), '');
    SET v_tx_date       = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.TransactionDate'));
    SET v_proc_date     = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.ProcessedDate'));
    SET v_status        = COALESCE(JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.Status')), 'PROCESSED');
    SET v_debit         = IF(v_tx_type = 'Debit', v_amount, 0);
    SET v_credit        = IF(v_tx_type = 'Debit', 0, v_amount);

    -- Idempotency keys on id, so a blank id would make every later event look like a duplicate.
    IF v_id IS NULL OR TRIM(v_id) = '' THEN
        SIGNAL SQLSTATE '45000' SET MYSQL_ERRNO = 1644, MESSAGE_TEXT = 'Id is required';
    END IF;

    START TRANSACTION;

    SELECT COUNT(*) INTO v_existing FROM transactions WHERE id = v_id FOR UPDATE;

    IF v_existing = 0 THEN
        INSERT INTO transactions (
            id, client_id, source_id, source_type, transaction_type,
            amount, currency, category_id, category_name,
            description, merchant_name, transaction_date, processed_date, status
        ) VALUES (
            v_id, v_client_id, v_source_id, v_source_type, v_tx_type,
            v_amount, v_currency, v_category_id, v_category_name,
            v_description, v_merchant, v_tx_date, v_proc_date, v_status
        );

        INSERT INTO transaction_aggregations (client_id, period, period_key, total_debit, total_credit, transaction_count)
        VALUES
            (v_client_id, 'ALL_TIME', 'ALL',                         v_debit, v_credit, 1),
            (v_client_id, 'MONTHLY',  DATE_FORMAT(v_tx_date, '%Y-%m'), v_debit, v_credit, 1)
        ON DUPLICATE KEY UPDATE
            total_debit       = total_debit + VALUES(total_debit),
            total_credit      = total_credit + VALUES(total_credit),
            transaction_count = transaction_count + VALUES(transaction_count),
            last_updated      = CURRENT_TIMESTAMP;
    END IF;

    COMMIT;

    SELECT 0 AS ErrorCode, NULL AS ErrorMessage;
END$$

-- ============================================================
-- API PROCEDURES (read path)
-- ============================================================

DROP PROCEDURE IF EXISTS usp_FetchTransactions$$
CREATE PROCEDURE usp_FetchTransactions(
    IN Payload JSON
)
BEGIN
    DECLARE v_client_id   VARCHAR(50);
    DECLARE v_source_type VARCHAR(50);
    DECLARE v_category_id TINYINT;
    DECLARE v_tx_type     VARCHAR(10);
    DECLARE v_min_amount  DECIMAL(18,2);
    DECLARE v_max_amount  DECIMAL(18,2);
    DECLARE v_from_date   DATETIME;
    DECLARE v_to_date     DATETIME;
    DECLARE v_page        INT;
    DECLARE v_page_size   INT;
    DECLARE v_sort_by     VARCHAR(50);
    DECLARE v_sort_dir    VARCHAR(4);
    DECLARE v_offset      INT;

    SET v_client_id   = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.ClientId'));
    SET v_source_type = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.SourceType'));
    SET v_category_id = JSON_EXTRACT(Payload, '$.CategoryId');
    SET v_tx_type     = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.TransactionType'));
    SET v_min_amount  = JSON_EXTRACT(Payload, '$.MinAmount');
    SET v_max_amount  = JSON_EXTRACT(Payload, '$.MaxAmount');
    SET v_from_date   = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.FromDate'));
    SET v_to_date     = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.ToDate'));
    SET v_page        = COALESCE(JSON_EXTRACT(Payload, '$.Page'), 1);
    SET v_page_size   = COALESCE(JSON_EXTRACT(Payload, '$.PageSize'), 20);
    SET v_sort_by     = COALESCE(JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.SortBy')), 'TransactionDate');
    SET v_sort_dir    = IF(UPPER(JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.SortOrder'))) = 'ASC', 'ASC', 'DESC');
    SET v_offset      = (v_page - 1) * v_page_size;

    SELECT
        id              AS Id,
        client_id       AS ClientId,
        source_id       AS SourceId,
        source_type     AS SourceType,
        transaction_type AS TransactionType,
        amount          AS Amount,
        currency        AS Currency,
        category_id     AS CategoryId,
        category_name   AS CategoryName,
        description     AS Description,
        merchant_name   AS MerchantName,
        transaction_date AS TransactionDate,
        processed_date  AS ProcessedDate,
        status          AS Status
    FROM transactions
    WHERE
        (v_client_id   IS NULL OR client_id     = v_client_id)
        AND (v_source_type IS NULL OR source_type   = v_source_type)
        AND (v_category_id IS NULL OR category_id   = v_category_id)
        AND (v_tx_type     IS NULL OR transaction_type = v_tx_type)
        AND (v_min_amount  IS NULL OR amount >= v_min_amount)
        AND (v_max_amount  IS NULL OR amount <= v_max_amount)
        AND (v_from_date   IS NULL OR transaction_date >= v_from_date)
        AND (v_to_date     IS NULL OR transaction_date <= v_to_date)
    ORDER BY
        CASE WHEN v_sort_by = 'Amount'  AND v_sort_dir = 'ASC'  THEN amount END ASC,
        CASE WHEN v_sort_by = 'Amount'  AND v_sort_dir = 'DESC' THEN amount END DESC,
        CASE WHEN v_sort_by <> 'Amount' AND v_sort_dir = 'ASC'  THEN transaction_date END ASC,
        CASE WHEN v_sort_by <> 'Amount' AND v_sort_dir = 'DESC' THEN transaction_date END DESC,
        id
    LIMIT v_page_size OFFSET v_offset;

    SELECT COUNT(*) AS TotalCount
    FROM transactions
    WHERE
        (v_client_id   IS NULL OR client_id     = v_client_id)
        AND (v_source_type IS NULL OR source_type   = v_source_type)
        AND (v_category_id IS NULL OR category_id   = v_category_id)
        AND (v_tx_type     IS NULL OR transaction_type = v_tx_type)
        AND (v_min_amount  IS NULL OR amount >= v_min_amount)
        AND (v_max_amount  IS NULL OR amount <= v_max_amount)
        AND (v_from_date   IS NULL OR transaction_date >= v_from_date)
        AND (v_to_date     IS NULL OR transaction_date <= v_to_date);
END$$

DROP PROCEDURE IF EXISTS usp_FetchTransactionById$$
CREATE PROCEDURE usp_FetchTransactionById(
    IN Payload JSON
)
BEGIN
    DECLARE v_id VARCHAR(36);

    SET v_id = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.Id'));

    SELECT
        id              AS Id,
        client_id       AS ClientId,
        source_id       AS SourceId,
        source_type     AS SourceType,
        transaction_type AS TransactionType,
        amount          AS Amount,
        currency        AS Currency,
        category_id     AS CategoryId,
        category_name   AS CategoryName,
        description     AS Description,
        merchant_name   AS MerchantName,
        transaction_date AS TransactionDate,
        processed_date  AS ProcessedDate,
        status          AS Status
    FROM transactions
    WHERE id = v_id
    LIMIT 1;
END$$

DROP PROCEDURE IF EXISTS usp_FetchClientTransactions$$
CREATE PROCEDURE usp_FetchClientTransactions(
    IN ClientId VARCHAR(50),
    IN Payload  JSON
)
BEGIN
    DECLARE v_source_type VARCHAR(50);
    DECLARE v_category_id TINYINT;
    DECLARE v_tx_type     VARCHAR(10);
    DECLARE v_min_amount  DECIMAL(18,2);
    DECLARE v_max_amount  DECIMAL(18,2);
    DECLARE v_from_date   DATETIME;
    DECLARE v_to_date     DATETIME;
    DECLARE v_page        INT;
    DECLARE v_page_size   INT;
    DECLARE v_offset      INT;

    SET v_source_type = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.SourceType'));
    SET v_category_id = JSON_EXTRACT(Payload, '$.CategoryId');
    SET v_tx_type     = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.TransactionType'));
    SET v_min_amount  = JSON_EXTRACT(Payload, '$.MinAmount');
    SET v_max_amount  = JSON_EXTRACT(Payload, '$.MaxAmount');
    SET v_from_date   = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.FromDate'));
    SET v_to_date     = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.ToDate'));
    SET v_page        = COALESCE(JSON_EXTRACT(Payload, '$.Page'), 1);
    SET v_page_size   = COALESCE(JSON_EXTRACT(Payload, '$.PageSize'), 20);
    SET v_offset      = (v_page - 1) * v_page_size;

    SELECT
        id AS Id, client_id AS ClientId, source_id AS SourceId,
        source_type AS SourceType, transaction_type AS TransactionType,
        amount AS Amount, currency AS Currency,
        category_id AS CategoryId, category_name AS CategoryName,
        description AS Description, merchant_name AS MerchantName,
        transaction_date AS TransactionDate, processed_date AS ProcessedDate, status AS Status
    FROM transactions
    WHERE
        client_id = ClientId
        AND (v_source_type IS NULL OR source_type   = v_source_type)
        AND (v_category_id IS NULL OR category_id   = v_category_id)
        AND (v_tx_type     IS NULL OR transaction_type = v_tx_type)
        AND (v_min_amount  IS NULL OR amount >= v_min_amount)
        AND (v_max_amount  IS NULL OR amount <= v_max_amount)
        AND (v_from_date   IS NULL OR transaction_date >= v_from_date)
        AND (v_to_date     IS NULL OR transaction_date <= v_to_date)
    ORDER BY transaction_date DESC, id DESC
    LIMIT v_page_size OFFSET v_offset;

    SELECT COUNT(*) AS TotalCount
    FROM transactions
    WHERE
        client_id = ClientId
        AND (v_source_type IS NULL OR source_type   = v_source_type)
        AND (v_category_id IS NULL OR category_id   = v_category_id)
        AND (v_tx_type     IS NULL OR transaction_type = v_tx_type)
        AND (v_min_amount  IS NULL OR amount >= v_min_amount)
        AND (v_max_amount  IS NULL OR amount <= v_max_amount)
        AND (v_from_date   IS NULL OR transaction_date >= v_from_date)
        AND (v_to_date     IS NULL OR transaction_date <= v_to_date);
END$$

DROP PROCEDURE IF EXISTS usp_FetchAggregationSummary$$
CREATE PROCEDURE usp_FetchAggregationSummary()
BEGIN
    SELECT
        SUM(a.total_debit)                                          AS TotalDebit,
        SUM(a.total_credit)                                         AS TotalCredit,
        SUM(a.transaction_count)                                    AS TotalTransactionCount,
        COUNT(DISTINCT a.client_id)                                 AS UniqueClientCount,
        (SELECT category_name FROM transactions GROUP BY category_name ORDER BY COUNT(*) DESC LIMIT 1) AS TopCategory,
        (SELECT source_type   FROM transactions GROUP BY source_type   ORDER BY COUNT(*) DESC LIMIT 1) AS TopSource,
        CASE WHEN SUM(a.transaction_count) > 0
             THEN ROUND((SUM(a.total_debit) + SUM(a.total_credit)) / SUM(a.transaction_count), 2)
             ELSE 0 END                                             AS AverageTransactionAmount
    FROM transaction_aggregations a
    WHERE a.period = 'ALL_TIME';
END$$

DROP PROCEDURE IF EXISTS usp_FetchClientAggregation$$
CREATE PROCEDURE usp_FetchClientAggregation(
    IN Payload JSON
)
BEGIN
    DECLARE v_client_id VARCHAR(50);

    SET v_client_id = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.ClientId'));

    SELECT
        client_id                                           AS ClientId,
        SUM(total_debit)                                    AS TotalDebit,
        SUM(total_credit)                                   AS TotalCredit,
        SUM(transaction_count)                              AS TransactionCount,
        ROUND(SUM(total_credit) - SUM(total_debit), 2)     AS NetPosition,
        MAX(last_updated)                                   AS LastUpdated
    FROM transaction_aggregations
    WHERE client_id = v_client_id AND period = 'ALL_TIME'
    GROUP BY client_id;
END$$

DROP PROCEDURE IF EXISTS usp_FetchClientAggregationByCategory$$
CREATE PROCEDURE usp_FetchClientAggregationByCategory(
    IN Payload JSON
)
BEGIN
    DECLARE v_client_id VARCHAR(50);
    DECLARE v_total     DECIMAL(18,2);

    SET v_client_id = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.ClientId'));
    SET v_total     = (SELECT COALESCE(SUM(amount), 1) FROM transactions WHERE client_id = v_client_id AND transaction_type = 'Debit');

    SELECT
        v_client_id                                                         AS ClientId,
        category_id                                                         AS CategoryId,
        category_name                                                       AS CategoryName,
        ROUND(SUM(amount), 2)                                               AS TotalAmount,
        COUNT(*)                                                            AS TransactionCount,
        ROUND(SUM(amount) / NULLIF(v_total, 0) * 100, 2)                   AS Percentage
    FROM transactions
    WHERE client_id = v_client_id AND transaction_type = 'Debit'
    GROUP BY category_id, category_name
    ORDER BY TotalAmount DESC;
END$$

DROP PROCEDURE IF EXISTS usp_FetchClientAggregationBySource$$
CREATE PROCEDURE usp_FetchClientAggregationBySource(
    IN Payload JSON
)
BEGIN
    DECLARE v_client_id VARCHAR(50);

    SET v_client_id = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.ClientId'));

    SELECT
        client_id                                               AS ClientId,
        source_type                                             AS SourceType,
        ROUND(SUM(CASE WHEN transaction_type = 'Debit'  THEN amount ELSE 0 END), 2) AS TotalDebit,
        ROUND(SUM(CASE WHEN transaction_type = 'Credit' THEN amount ELSE 0 END), 2) AS TotalCredit,
        COUNT(*)                                                AS TransactionCount
    FROM transactions
    WHERE client_id = v_client_id
    GROUP BY client_id, source_type
    ORDER BY TransactionCount DESC;
END$$

DROP PROCEDURE IF EXISTS usp_FetchClientMonthlyAggregation$$
CREATE PROCEDURE usp_FetchClientMonthlyAggregation(
    IN Payload JSON
)
BEGIN
    DECLARE v_client_id VARCHAR(50);

    SET v_client_id = JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.ClientId'));

    SELECT
        client_id                                               AS ClientId,
        period_key                                              AS Period,
        total_debit                                             AS TotalDebit,
        total_credit                                            AS TotalCredit,
        transaction_count                                       AS TransactionCount,
        ROUND(total_credit - total_debit, 2)                    AS NetPosition
    FROM transaction_aggregations
    WHERE client_id = v_client_id AND period = 'MONTHLY'
    ORDER BY period_key DESC;
END$$

DROP PROCEDURE IF EXISTS usp_FetchTopSpenders$$
CREATE PROCEDURE usp_FetchTopSpenders(
    IN Payload JSON
)
BEGIN
    DECLARE v_top_n      INT;
    DECLARE v_period_key VARCHAR(20);

    SET v_top_n      = COALESCE(JSON_EXTRACT(Payload, '$.TopN'), 10);
    SET v_period_key = NULLIF(JSON_UNQUOTE(JSON_EXTRACT(Payload, '$.PeriodKey')), '');

    SELECT
        client_id                                               AS ClientId,
        ROUND(SUM(total_debit), 2)                              AS TotalSpend,
        SUM(transaction_count)                                  AS TransactionCount,
        RANK() OVER (ORDER BY SUM(total_debit) DESC)            AS `Rank`
    FROM transaction_aggregations
    WHERE
        period = CASE WHEN v_period_key IS NULL THEN 'ALL_TIME' ELSE 'MONTHLY' END
        AND (v_period_key IS NULL OR period_key = v_period_key)
    GROUP BY client_id
    ORDER BY TotalSpend DESC
    LIMIT v_top_n;
END$$

DELIMITER ;
