-- IMPROVEMENT-022: Randomised Daily Objective Pool
-- Adds daily_objectives_per_day to the seasons table.
-- NULL = all daily objectives active every day (no behaviour change).
-- A positive integer = draw exactly N daily objectives per UTC day
-- using a deterministic seed derived from (season_id, day).

BEGIN TRANSACTION;

ALTER TABLE dbo.seasons
    ADD daily_objectives_per_day smallint NULL;

COMMIT;
