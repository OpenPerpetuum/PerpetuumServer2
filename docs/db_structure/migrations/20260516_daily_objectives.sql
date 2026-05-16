-- IMPROVEMENT-006: Daily Objectives
-- Adds is_daily and package_id to season_objectives.
-- Adds day_window to season_objective_progress and rebuilds its PK.

BEGIN TRANSACTION;

-- 1. Extend season_objectives
ALTER TABLE dbo.season_objectives
    ADD is_daily   bit NOT NULL DEFAULT 0,
        package_id int NULL;

-- 2. Add day_window (existing rows get sentinel '1900-01-01')
ALTER TABLE dbo.season_objective_progress
    ADD day_window date NOT NULL DEFAULT '19000101';

-- 3. Drop old PK (character_id, season_id, objective_id)
ALTER TABLE dbo.season_objective_progress
    DROP CONSTRAINT PK_season_objective_progress;

-- 4. New PK includes day_window
ALTER TABLE dbo.season_objective_progress
    ADD CONSTRAINT PK_season_objective_progress
    PRIMARY KEY (character_id, season_id, objective_id, day_window);

COMMIT;
