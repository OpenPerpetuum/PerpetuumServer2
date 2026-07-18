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

-- Part 2: SelfDestructModule entity definition + tunable config.
--
-- IMPORTANT — this part deviates from the plan document's Task 2 draft SQL in two structural ways,
-- both required for the script to even run against the real schema (verified against
-- docs/db_structure/database_schema_documentation.md, not just the plan's paraphrase):
--
-- 1. `definitionconfig` is a wide, one-row-per-definition table keyed by the numeric `definition`
--    column (FK to entitydefaults.definition, UNIQUE index IX_definitionconfig) with one real column
--    per config value (item_work_range, explosion_radius, damage_chemical, action_delay, ...). It is
--    NOT a key/value table — there is no definitionname/configkey/configvalue EAV shape anywhere in
--    the schema. Every other real migration/plan in this repo that touches this table
--    (docs/superpowers/plans/2026-05-16-improvement-003-item-designer.md,
--    docs/superpowers/plans/2026-05-17-improvement-004-robot-designer.md,
--    docs/content/claude_game_content_guide.md section 22) uses
--    `INSERT INTO definitionconfig (definition, <realcolumn>, ...) VALUES (...)`. The column is
--    `action_delay` (int, milliseconds), not `ActionDelay` — `DefinitionConfig.ActionDelay` (the C#
--    property SelfDestructModule.cs reads) wraps it as TimeSpan.FromMilliseconds(action_delay) and
--    throws ErrorCodes.ServerError if the row or column is NULL.
--
-- 2. categoryFlags.value is not an arbitrary "next free integer" — CategoryFlags (C#) is a [Flags]
--    enum whose values are hierarchical byte-packed IDs (see CategoryFlagsExtensions.IsCategory /
--    GetCategoryFlagsMask), and the entity factory
--    (src/Perpetuum.Bootstrapper/Modules/EntitiesModule.cs) resolves SelfDestructModule strictly via
--    ByCategoryFlags<SelfDestructModule>(CategoryFlags.cf_self_destruct_modules) — an exact numeric
--    match against a real C# enum member. A sequential `MAX(value)+1` id (the plan draft's approach)
--    would not correspond to any enum member and the module would never be constructed as
--    SelfDestructModule (it would silently fall back to a generic Module with no OnAction()).
--    `cf_self_destruct_modules = 0x0000000000000D0F` was added to
--    src/Perpetuum.ExportedTypes/CategoryFlags.cs as a new module-family member (byte0 = 0x0F =
--    "module" family shared by cf_robot_equipment's whole subtree, byte1 = 0x0D = next free subgroup
--    after cf_robot_enhancements' 0x0C) — the literal below must stay in sync with that enum member.
--
-- attributeflags = 2097168 = (1 << 4) activeModule | (1 << 21) forceOneCycle (AttributeFlags.cs).
-- forceOneCycle is not cosmetic: ActiveModule.States.cs's IdleState.SwitchTo forces every activation
-- of this module into ModuleStateType.Oneshot regardless of what the client requests, which is what
-- guarantees OnAction() fires exactly once per activation. This definition intentionally has no
-- aggregatevalues row for cycle_time (defaults to 0 via GetPropertyModifier), so without forceOneCycle
-- an AutoRepeat activation would call OnAction() on every module Update() tick — combined with the
-- Arm()/IsArmed() TOCTOU window Task 1 flagged (EffectHandler's pending-effects queue may not be
-- drained yet when IsArmed() is checked on the very next tick), that could in principle produce two
-- independent countdowns from a single activation. forceOneCycle removes the repeated-tick trigger
-- entirely, so the per-activation IsArmed() guard in SelfDestructModule.OnAction() only has to cope
-- with a single OnAction() call per activation, which it does.
--
-- Damage values (2000/type), radius (15m), and action_delay (8000ms) are starting balance numbers, not
-- final tuning — flag for playtesting. `moduleFlag=i909` reuses the numbering convention shown in
-- docs/content/claude_game_content_guide.md's example (`#moduleFlag=i908#tier=...`); verify `i909`
-- isn't already assigned to another module before applying, same as the categoryFlags/attributeflags
-- values above — none of this has been verified against a live DB, and this script has not been
-- applied to any database.

IF NOT EXISTS (SELECT 1 FROM categoryFlags WHERE name = 'cf_self_destruct_modules')
BEGIN
    INSERT INTO categoryFlags (value, name, note, hidden, isunique)
    VALUES (0x0000000000000D0F, 'cf_self_destruct_modules', 'Self-destruct / kamikaze modules', 0, 0);
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_self_destruct_module', 1,
         2097168, -- activeModule (bit 4) | forceOneCycle (bit 21) -- see note above
         (SELECT value FROM categoryFlags WHERE name = 'cf_self_destruct_modules'),
         '#moduleFlag=i909#tier=$tierlevel_t1',
         N'Kamikaze self-destruct module: arms an un-cancellable delayed AoE detonation that kills the owner.',
         1, 100, 500, 0, 100, N'def_standard_self_destruct_module', 1, 'standard', 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM definitionconfig
    WHERE definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module')
)
BEGIN
    INSERT INTO definitionconfig
        (definition, explosion_radius, damage_chemical, damage_explosive, damage_kinetic, damage_thermal, action_delay)
    VALUES
        ((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module'),
         15,     -- explosion_radius (meters)
         2000,   -- damage_chemical
         2000,   -- damage_explosive
         2000,   -- damage_kinetic
         2000,   -- damage_thermal
         8000);  -- action_delay (ms); SelfDestructModule.OnAction() falls back to an 8s default if this
                 -- is ever 0/NULL, but this row must still carry a positive value so that fallback path
                 -- is never silently relied upon in normal operation.
END;
