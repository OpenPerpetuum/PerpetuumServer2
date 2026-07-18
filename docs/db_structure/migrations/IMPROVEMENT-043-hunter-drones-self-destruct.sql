-- IMPROVEMENT-043: Hunter Drones with Self-Destruct Module
-- Part 1: effect + aggregate field rows for the self-destruct countdown.
-- All INSERTs are idempotent (IF NOT EXISTS guarded). IDs below were verified against the
-- current highest values in src/Perpetuum.ExportedTypes/EffectType.cs and AggregateField.cs
-- (see plan Task 1, Steps 1-2) as of this migration's authoring; re-verify against the target
-- DB before applying if those enums have grown further in the meantime.
--
-- effects.id and aggregatefields.id are both IDENTITY(1,1) columns, so SET IDENTITY_INSERT
-- is required to insert explicit IDs that must match the C# enum values exactly (same pattern
-- as docs/Patches/p36.2/Features/EquipmentSets/effects_row.sql for effect_equipment_set_bonus).
--
-- effectcategory is set to 0 (undefined) rather than allocating a new effectcategories row:
-- effectcategories' primary key is `flag` (a bit in the EffectCategory [Flags] enum), not an
-- auto id, and nothing in this feature queries effects by category — the equipment-set-bonus
-- effect (id 139) uses the same 0/undefined category for the same reason.
--
-- aggregatefields.formula is an int that is cast directly to the AggregateFormula enum
-- (Modifier = 0, Add = 1, Inverse = 2) by AggregateFieldExtensions.GetFormula() — it is not a
-- string column, so the value below is the numeric AggregateFormula.Modifier value.

SET IDENTITY_INSERT effects ON;

IF NOT EXISTS (SELECT 1 FROM effects WHERE id = 140)
BEGIN
    INSERT INTO effects
        (id, name, description, effectcategory, duration, isaura, auraradius, ispositive, display, saveable)
    VALUES
        (140,
         N'effect_self_destruct_countdown',
         N'A self-destruct countdown is armed and ticking on this unit.',
         0,      -- effectcategory: undefined (no category-based queries need this effect)
         0,      -- duration: set per-activation via EffectBuilder.WithDuration(delay), not this default
         0,      -- isaura
         0,      -- auraradius
         0,      -- ispositive: it's a debuff/hazard, not a buff
         1,      -- display: visible countdown to the owner/nearby players
         0);     -- saveable: does not need to persist across server restarts
END;

SET IDENTITY_INSERT effects OFF;

SET IDENTITY_INSERT aggregatefields ON;

IF NOT EXISTS (SELECT 1 FROM aggregatefields WHERE id = 760)
BEGIN
    INSERT INTO aggregatefields (id, name, formula, measurementunit, moreisbetter)
    VALUES (760, 'self_destruct_config_explosion_radius', 0, 'meter', 1);
END;
IF NOT EXISTS (SELECT 1 FROM aggregatefields WHERE id = 761)
BEGIN
    INSERT INTO aggregatefields (id, name, formula, measurementunit, moreisbetter)
    VALUES (761, 'self_destruct_config_damage_chemical', 0, 'point', 1);
END;
IF NOT EXISTS (SELECT 1 FROM aggregatefields WHERE id = 762)
BEGIN
    INSERT INTO aggregatefields (id, name, formula, measurementunit, moreisbetter)
    VALUES (762, 'self_destruct_config_damage_explosive', 0, 'point', 1);
END;
IF NOT EXISTS (SELECT 1 FROM aggregatefields WHERE id = 763)
BEGIN
    INSERT INTO aggregatefields (id, name, formula, measurementunit, moreisbetter)
    VALUES (763, 'self_destruct_config_damage_kinetic', 0, 'point', 1);
END;
IF NOT EXISTS (SELECT 1 FROM aggregatefields WHERE id = 764)
BEGIN
    INSERT INTO aggregatefields (id, name, formula, measurementunit, moreisbetter)
    VALUES (764, 'self_destruct_config_damage_thermal', 0, 'point', 1);
END;

SET IDENTITY_INSERT aggregatefields OFF;
