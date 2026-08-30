-- IMPROVEMENT-009: Targeted Objectives (Mining & Harvesting)
-- Run once against the game database before deploying the updated server binary.

ALTER TABLE season_objectives ADD target_definition_id INT NULL;
