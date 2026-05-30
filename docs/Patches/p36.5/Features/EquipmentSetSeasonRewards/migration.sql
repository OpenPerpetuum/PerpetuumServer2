-- Equipment Set Season Rewards Migration (IMPROVEMENT-033)
-- Run once against the game database before deploying the updated server binary.

-- Make package_id nullable on tables where it was NOT NULL
ALTER TABLE season_tiers               ALTER COLUMN package_id INT NULL;
ALTER TABLE season_leaderboard_rewards ALTER COLUMN package_id INT NULL;

-- Add equipment_set_id to all three season reward tables
ALTER TABLE season_tiers               ADD equipment_set_id INT NULL REFERENCES equipment_sets(set_id);
ALTER TABLE season_objectives          ADD equipment_set_id INT NULL REFERENCES equipment_sets(set_id);
ALTER TABLE season_leaderboard_rewards ADD equipment_set_id INT NULL REFERENCES equipment_sets(set_id);
