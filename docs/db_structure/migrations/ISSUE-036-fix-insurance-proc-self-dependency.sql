-- ISSUE-036: Fix recurring "nesting level exceeded (limit 32)" failure in usp_RecalculateInsurancePrices
-- Apply once to the live database. Safe to re-run (CREATE OR ALTER is idempotent).
--
-- Root cause: IMPROVEMENT-036-insurance-overhaul.sql created usp_RecalculateInsurancePrices with
-- CREATE OR ALTER PROCEDURE ... END immediately followed, in the SAME BATCH (no GO), by:
--     DELETE FROM dbo.insurance;
--     EXEC dbo.usp_RecalculateInsurancePrices;
-- SQL Server's deferred-name-resolution dependency parser recorded that trailing EXEC as part of
-- the module's own dependency set, producing a bogus self-referencing row in
-- sys.sql_expression_dependencies (the procedure depends on itself). Confirmed via:
--     SELECT referenced_entity_name FROM sys.sql_expression_dependencies
--     WHERE referencing_id = OBJECT_ID('dbo.usp_RecalculateInsurancePrices');
-- which returned usp_RecalculateInsurancePrices itself alongside the real dependencies.
--
-- Combined with v_all_production_costs already sitting close to SQL Server's 32-level
-- stored-procedure/function/trigger/view nesting ceiling (per ISSUE-029's ~28-level headroom
-- note), that extra bogus self-dependency is what pushes execution over the limit -> error 217.
-- sp_recompile does NOT clear this; only re-issuing CREATE (OR ALTER) PROCEDURE as the sole
-- statement in its own batch recalculates dependencies correctly and removes the self-reference.
--
-- Verified in the local up-to-date-with-live test DB: EXEC usp_RecalculateInsurancePrices failed
-- with error 217 before this fix, and succeeded immediately after re-creating the procedure body
-- below in isolation (no trailing statements in the same batch).
--
-- This migration does not change the procedure's logic at all — it is byte-for-byte the same
-- body as docs/db_structure/stored_procedures/dbo.usp_RecalculateInsurancePrices.sql, issued
-- alone in its own batch so SQL Server recomputes a clean dependency list.

CREATE OR ALTER PROCEDURE dbo.usp_RecalculateInsurancePrices AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @fee_pct    FLOAT = (SELECT param_value FROM dbo.insurance_config WHERE param_name = 'fee_pct');
    DECLARE @payout_pct FLOAT = (SELECT param_value FROM dbo.insurance_config WHERE param_name = 'payout_pct');

    IF @fee_pct IS NULL OR @payout_pct IS NULL
    BEGIN
        RAISERROR('insurance_config: fee_pct and payout_pct must both be set.', 16, 1);
        RETURN;
    END

/*
    IF @payout_pct >= @fee_pct
    BEGIN
        RAISERROR('insurance_config: payout_pct must be strictly less than fee_pct to keep insurance a NIC sink.', 16, 1);
        RETURN;
    END
*/

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
GO

-- Verification (expect 4 rows: entitydefaults, insurance_config, insuranceprices,
-- v_all_production_costs — NOT usp_RecalculateInsurancePrices itself):
-- SELECT referenced_entity_name FROM sys.sql_expression_dependencies
-- WHERE referencing_id = OBJECT_ID('dbo.usp_RecalculateInsurancePrices');
--
-- Then confirm the procedure now runs cleanly:
-- EXEC dbo.usp_RecalculateInsurancePrices;
