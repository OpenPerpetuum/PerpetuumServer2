-- IMPROVEMENT-018: add scoring_mode to seasons
-- 0 = ActivityAndGlobal (default, preserves existing behaviour)
-- 1 = ObjectivesOnly
ALTER TABLE seasons
    ADD scoring_mode TINYINT NOT NULL DEFAULT 0;
