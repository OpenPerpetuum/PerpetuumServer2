-- Pilot set: set_striker (IMPROVEMENT-025 validation content)
-- Idempotent: safe to run multiple times.
--
-- AggregateField IDs used:
--   17  = armor_max_modifier      (formula: Modifier — 1.05 = +5% max armor)
--   310 = resist_kinetic_modifier (formula: Modifier — 1.08 = +8% kinetic resist)
--
-- Member modules: all four tiers of medium armor plates.
-- Definition names are resolved dynamically via entitydefaults — no hardcoded IDs.

-- 1. Create the set (idempotent)
IF NOT EXISTS (SELECT 1 FROM equipment_sets WHERE name = N'set_striker')
    INSERT INTO equipment_sets (name) VALUES (N'set_striker');

DECLARE @set_id INT;
SELECT @set_id = set_id FROM equipment_sets WHERE name = N'set_striker';

-- 2. Assign member modules — all four medium armor plate tiers
-- Skips any member already present to remain idempotent.
INSERT INTO equipment_set_members (set_id, definition)
SELECT @set_id, ed.definition
FROM entitydefaults ed
WHERE ed.definitionname IN (
    N'def_standard_medium_armor_plate',
    N'def_named1_medium_armor_plate',
    N'def_named2_medium_armor_plate',
    N'def_named3_medium_armor_plate'
)
AND NOT EXISTS (
    SELECT 1
    FROM equipment_set_members esm
    WHERE esm.set_id = @set_id
      AND esm.definition = ed.definition
);

-- 3. 2-piece threshold: +5% max armor (aggregate_field = 17, bonus_value = 1.05)
IF NOT EXISTS (
    SELECT 1 FROM equipment_set_bonus_thresholds
    WHERE set_id = @set_id AND required_pieces = 2 AND aggregate_field = 17
)
    INSERT INTO equipment_set_bonus_thresholds (set_id, required_pieces, aggregate_field, bonus_value)
    VALUES (@set_id, 2, 17, 1.05);

-- 4. 4-piece threshold: +8% kinetic resist (aggregate_field = 310, bonus_value = 1.08)
IF NOT EXISTS (
    SELECT 1 FROM equipment_set_bonus_thresholds
    WHERE set_id = @set_id AND required_pieces = 4 AND aggregate_field = 310
)
    INSERT INTO equipment_set_bonus_thresholds (set_id, required_pieces, aggregate_field, bonus_value)
    VALUES (@set_id, 4, 310, 1.08);
