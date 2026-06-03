-- IMPROVEMENT-039: Economy Health Statistics
-- Apply once to the live database before deploying the matching server and Admin Tool builds.
-- Tables: run once only (will error if tables already exist — that is intentional).
-- Procedure: CREATE OR ALTER — safe to re-run.

CREATE TABLE economy_daily_snapshot (
    id            INT IDENTITY(1,1) PRIMARY KEY,
    snapshot_date DATE   NOT NULL,
    total_nic     BIGINT NOT NULL,
    CONSTRAINT UQ_economy_daily_snapshot_date UNIQUE (snapshot_date)
);

CREATE TABLE economy_price_index_basket (
    id         INT IDENTITY(1,1) PRIMARY KEY,
    definition INT          NOT NULL,
    weight     DECIMAL(5,2) NOT NULL DEFAULT 1.0
);

CREATE OR ALTER PROCEDURE usp_RecordEconomySnapshot AS
BEGIN
    DECLARE @snapshot_date DATE   = CAST(GETUTCDATE() AS DATE);
    DECLARE @total_nic     BIGINT =
        ISNULL((SELECT SUM(CAST(credit AS BIGINT)) FROM characters
                WHERE active = 1 AND deletedAt IS NULL), 0)
      + ISNULL((SELECT SUM(CAST(wallet AS BIGINT)) FROM corporations
                WHERE active = 1 AND defaultcorp = 0), 0);

    MERGE economy_daily_snapshot AS t
    USING (SELECT @snapshot_date AS snapshot_date, @total_nic AS total_nic) AS s
    ON t.snapshot_date = s.snapshot_date
    WHEN MATCHED     THEN UPDATE SET total_nic = s.total_nic
    WHEN NOT MATCHED THEN INSERT (snapshot_date, total_nic)
                          VALUES (s.snapshot_date, s.total_nic);
END
