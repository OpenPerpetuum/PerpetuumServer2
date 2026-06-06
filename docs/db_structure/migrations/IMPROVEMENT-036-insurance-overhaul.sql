-- IMPROVEMENT-036: Insurance System Overhaul
-- Apply once to the live database while the server is OFFLINE, before deploying the new build.
-- Run in order: table → procedure → clear stale policies → initial price population.

-- 1. Create insurance_config table
IF OBJECT_ID('dbo.insurance_config', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.insurance_config (
        param_name  NVARCHAR(64) NOT NULL PRIMARY KEY,
        param_value FLOAT        NOT NULL
    );
    INSERT INTO dbo.insurance_config (param_name, param_value) VALUES
        ('fee_pct',    0.10),
        ('payout_pct', 0.08);
END

-- 2. Create usp_RecalculateInsurancePrices
CREATE OR ALTER PROCEDURE dbo.usp_RecalculateInsurancePrices AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @fee_pct    FLOAT = (SELECT param_value FROM dbo.insurance_config WHERE param_name = 'fee_pct');
    DECLARE @payout_pct FLOAT = (SELECT param_value FROM dbo.insurance_config WHERE param_name = 'payout_pct');

    IF @fee_pct IS NULL OR @payout_pct IS NULL
        RAISERROR('insurance_config: fee_pct and payout_pct must both be set.', 16, 1);

    IF @payout_pct >= @fee_pct
        RAISERROR('insurance_config: payout_pct must be strictly less than fee_pct to keep insurance a NIC sink.', 16, 1);

    MERGE dbo.insuranceprices AS t
    USING (
        SELECT
            ed.definition,
            ROUND(vpc.production_cost_nic * @fee_pct,    0) AS fee,
            ROUND(vpc.production_cost_nic * @payout_pct, 0) AS payout
        FROM dbo.v_all_production_costs vpc
        JOIN dbo.entitydefaults ed
            ON ed.definitionname = vpc.product COLLATE DATABASE_DEFAULT
        WHERE ed.definition IN (SELECT definition FROM dbo.insuranceprices)
          AND vpc.production_cost_nic > 0
    ) AS s ON t.definition = s.definition
    WHEN MATCHED THEN
        UPDATE SET t.fee = s.fee, t.payout = s.payout;
END

-- 3. Clear all stale insurance policies (payout values are outdated; players repurchase at new rates)
DELETE FROM dbo.insurance;

-- 4. Populate insuranceprices immediately so the server cache loads correct values on first startup
EXEC dbo.usp_RecalculateInsurancePrices;
