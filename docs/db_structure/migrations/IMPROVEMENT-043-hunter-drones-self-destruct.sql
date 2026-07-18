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
         1, 100, 500, 0, 100, N'def_standard_self_destruct_module', 1, 1, 1); -- TierType.Normal = 1 (was incorrectly 'standard', a varchar, in the int tiertype column)
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

-- Part 3: HunterDrone chassis + shared RCU ammo + HunterRemoteController (PvE/PvP) entity definitions.
--
-- IMPORTANT -- this part deviates from the plan document's Task 7 draft SQL (and from this task's own
-- brief, which echoes that stale draft) in several structural ways, all required to match the real,
-- already-committed C# from Tasks 4/5/6 (verified by reading source directly, not the outdated draft):
--
-- 1. ONE shared HunterDrone chassis + ONE shared RCU ammo item, not two PvE/PvP chassis rows.
--    HunterDrone.TargetFaction (src/Perpetuum/Zones/RemoteControl/HunterDrone.cs:18) is a settable
--    property assigned by the SPAWNING MODULE
--    (src/Perpetuum/Modules/RemoteControl/HunterRemoteControllerModule.cs:31:
--    `hunterDrone.TargetFaction = GetTargetFaction();`), not baked into the chassis entitydefaults row.
--    TurretType.cs (src/Perpetuum/Zones/RemoteControl/TurretType.cs) only has ONE `HunterDrone` member
--    (Task 4 collapsed the original design spec's `HunterDronePvE`/`HunterDronePvP` split into a single
--    enum value plus a runtime `TargetFaction` field), and Task 6's own report
--    (.superpowers/sdd/task-6-report.md, "Design decision" section) states explicitly that "both modules
--    spawn the same HunterDrone RCU ammo/chassis ... Task 7's content SQL ... can point both at the same
--    hunter-drone RCU ammo item." A single ammo item's `options` can only encode one `turretId`
--    (EntityDefaultOptions.TurretId, EntityDefaultOptions.cs:119-128), so "one shared ammo item"
--    necessarily means "one shared chassis" -- two functionally-identical chassis rows sharing
--    categoryflags=cf_hunter_drones would be inert duplication no code path would ever select. Both
--    CategoryFlags.cs and EntitiesModule.cs implement the PvE/PvP split ONLY on the two controller-module
--    categories (cf_hunter_remote_controllers_pve / _pvp) -- there is only one cf_hunter_drones and one
--    cf_hunter_drones_units member each.
--
-- 2. The AoE/detonation config values (explosion_radius, damage_chemical/explosive/kinetic/thermal,
--    action_delay) belong on the HunterDrone CHASSIS's own definitionconfig row, read directly via
--    `Drone.ED.Config.*` inside HunterSelfDestructAI.Enter() -> SelfDestructDetonation.Arm(...)
--    (src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/HunterSelfDestructAI.cs:43-58). Contrary to the
--    original design spec text ("carries a SelfDestructModule instance in its head slot"), Task 5's real,
--    already-committed AI does not attach or activate a SelfDestructModule item on the drone at all -- it
--    calls a static `SelfDestructDetonation.Arm` helper directly, sourcing the same six definitionconfig
--    columns Task 2 already used for the player module (verified by reading
--    HunterRemoteControllerModule.CreateAndConfigureRcu in full: it never equips any module on the spawned
--    HunterDrone). No `components` table linkage is needed.
--
-- 3. `EntityDefaultOptions.TurretType`/`.TurretId` deserialize via GenXY type-tagged tokens
--    (EntityDefaultOptions.cs:119-149), and GenxyConverter.Serialize (GenXY/GenxyConverter.cs:289-311)
--    always writes an explicit type-token character before a value (`$` = String, `i` = Integer --
--    GenXY/GenxyToken.cs). GenxyReader.ReadValue (GenXY/GenxyReader.cs:77-143) consumes exactly one
--    character as the type token and starts the value read from the NEXT character; an untyped
--    `#turretType=HunterDrone` (as both this task's brief and the plan draft wrote it) would be misparsed
--    as token 'H' (unrecognized -> falls through to the plain-string branch) with the value string
--    starting from 'unterDrone' (the leading 'H' is consumed as the token char and discarded) --
--    Enum.Parse(TurretType, "unterDrone") would then throw the first time this ammo item's options were
--    read. Corrected to `#turretType=$HunterDrone` (explicit String token), matching how the only other
--    GenXY string value in this migration is written (`tier=$tierlevel_t1`, Part 2 above). `turretId`
--    uses the `i` (Integer) token per EntityDefaultOptions.TurretId and GenxyWriter.WriteHexInteger's
--    lowercase-hex-no-padding format (`{0:x}`, GenXY/GenxyWriter.cs:59-67); since the chassis's
--    `entitydefaults.definition` id is only known after that row is inserted (IDENTITY), it is resolved
--    dynamically via `FORMAT(@id, 'x')` (same lowercase-hex-no-padding output as C#'s `{0:x}`), never
--    hardcoded, per CLAUDE.md's content rules. Requires SQL Server 2012+ for `FORMAT()`.
--
-- 4. `entitydefaults.tiertype`/`.tierlevel` are `int` columns (database_schema_documentation.md,
--    `entitydefaults` section), cast to the `TierType` C# enum by
--    src/Perpetuum/EntityFramework/EntityDefaultReader.cs:73
--    (`(TierType)(record.GetValue<int?>("tiertype") ?? 0)`). Task 2's already-committed Part 2 above
--    writes the STRING literal `'standard'` into this int column for `def_standard_self_destruct_module`
--    -- that is a real data-type mismatch that will throw a SQL conversion error ("Conversion failed when
--    converting the varchar value 'standard' to data type int") the first time this migration is actually
--    run against a real SQL Server. Flagged here as a concern for a follow-up fix to Part 2 (out of this
--    task's scope: Part 2 was already committed by a previous task, and this task is purely additive) --
--    Part 3 below does NOT repeat the mistake: it uses `TierType.Normal` (`= 1`,
--    src/Perpetuum.ExportedTypes/TierType.cs) for tiertype and `1` for tierlevel (matching the unrelated
--    GenXY `tier=$tierlevel_t1` options token, which is a separate tier-family label, not the same axis
--    as this int column).
--
-- 5. `RemoteControllerModule`/`ActiveModule` need more than `moduleFlag`/`tier` in `options` to actually
--    function: ActiveModule.Ammo.cs:14,20 (`IsAmmoable => ammoCategoryFlags > 0 && AmmoCapacity > 0`,
--    `AmmoCapacity = ED.Options.AmmoCapacity`) means the controller module's OWN entitydefaults.options
--    must include `ammoCapacity`, or `GetAmmo()` always returns null and `OnAction()` can never spawn a
--    drone (`ammoCategoryFlags` itself is a compile-time C# constructor parameter wired up in
--    EntitiesModule.cs, not read from `options` at runtime -- confirmed via ActiveModule.cs:21-38, so no
--    `ammoType`/`ammoCategoryFlags` options key is needed, unlike what the AdminTool's own docs use for
--    its editor-only convenience). Also added `aggregatevalues` rows the base
--    `RemoteControllerModule`/`ActiveModule` actually read at runtime beyond the brief-flagged
--    `detection_range` (Task 6's `HunterRemoteControllerModule` ModuleProperty):
--    `remote_control_bandwidth_max` / `remote_control_operational_range` / `remote_control_lifetime`
--    (RemoteControllerModule.cs:45-52; all default to 0 with no row present --
--    AggregateFieldExtensions.GetDefaultValue, src/Perpetuum/AggregateFieldExtensions.cs:41-45 -- which
--    would leave the module permanently bandwidth-starved and instantly despawn anything it spawns) and
--    `cycle_time` (ActiveModule.cs:26-27's built-in `CycleTimeProperty`, present on every ActiveModule
--    subclass including this one). A real nonzero `cycle_time` was chosen here rather than
--    `SelfDestructModule`'s `forceOneCycle` attributeflag, since repeated drone-spawn attempts are a
--    normal, bandwidth-gated client-driven action for every sibling RemoteControllerModule
--    (Assault/Tactical/Industrial/Support), not a single-arm-only correctness concern like the player
--    kamikaze module. `core_usage`/`optimal_range`/`falloff` (also present on every ActiveModule /
--    ranged ActiveModule) are left at their zero/default value -- a free-activation, no-lock-range-check
--    balance gap, not a functional blocker, consistent with this migration's "starting balance, not final
--    tuning" numbers elsewhere -- flagged for playtesting, not fixed here.
--
-- All new `aggregatevalues` field ids below are resolved dynamically by `aggregatefields.name` (assumed
-- to match each field's C# enum member name, per the same generated-code convention already established
-- by Task 1's own new effects/aggregatefields rows), per
-- docs/content/claude_game_content_guide.md section 11's explicit "never hardcode IDs" instruction --
-- verify these `name` values against a live DB before applying (none of `detection_range` (599),
-- `remote_control_bandwidth_max` (672), `remote_control_bandwidth_usage` (674),
-- `remote_control_operational_range` (675), `remote_control_lifetime` (677), or `cycle_time` (110) are
-- created by this migration -- they are pre-existing legacy AggregateField ids, well below the new
-- 760-764 block Task 1 appended, so they must already have real rows in any live target DB).
--
-- Balance numbers below (damage, radius, delay, detection/operational range, bandwidth, lifetime, cycle
-- time, ammo capacity) are starting placeholders for playtesting, not final tuning, consistent with
-- Part 2's own damage/radius/delay placeholders. None of this has been verified against a live DB, and
-- this script has not been applied to any database.

-- 3a. New categoryFlags rows -- exact hex values copied from src/Perpetuum.ExportedTypes/CategoryFlags.cs
-- (all four added by Task 6), same pattern as Part 2's cf_self_destruct_modules row above.

IF NOT EXISTS (SELECT 1 FROM categoryFlags WHERE name = 'cf_hunter_drones')
BEGIN
    INSERT INTO categoryFlags (value, name, note, hidden, isunique)
    VALUES (0x0000000000051101, 'cf_hunter_drones', 'Autonomous kamikaze hunter drone chassis (shared by PvE/PvP controllers)', 0, 0);
END;

IF NOT EXISTS (SELECT 1 FROM categoryFlags WHERE name = 'cf_hunter_drones_units')
BEGIN
    INSERT INTO categoryFlags (value, name, note, hidden, isunique)
    VALUES (0x000000000008120A, 'cf_hunter_drones_units', 'Hunter drone RCU ammo (shared by PvE/PvP controllers)', 0, 0);
END;

IF NOT EXISTS (SELECT 1 FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers_pve')
BEGIN
    INSERT INTO categoryFlags (value, name, note, hidden, isunique)
    VALUES (0x00000000060C040F, 'cf_hunter_remote_controllers_pve', 'PvE hunter drone remote controller module', 0, 0);
END;

IF NOT EXISTS (SELECT 1 FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers_pvp')
BEGIN
    INSERT INTO categoryFlags (value, name, note, hidden, isunique)
    VALUES (0x00000000070C040F, 'cf_hunter_remote_controllers_pvp', 'PvP hunter drone remote controller module', 0, 0);
END;

-- 3b. HunterDrone chassis (shared by both PvE and PvP controllers -- see deviation #1 above).

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone', 1,
         0, -- no module-only attribute flags apply to a robot chassis
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_drones'),
         NULL, -- no GenXY options needed on the chassis itself; TargetFaction is assigned programmatically
               -- by whichever controller module spawns it (see deviation #1 above)
         N'Autonomous kamikaze drone chassis: hunts Niani NPCs when spawned by the PvE controller, or hostile-standing players when spawned by the PvP controller, and self-destructs on contact.',
         1, 50, 200, 0, 1500, N'def_standard_hunter_drone', 0, 1, 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM definitionconfig
    WHERE definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone')
)
BEGIN
    INSERT INTO definitionconfig
        (definition, explosion_radius, damage_chemical, damage_explosive, damage_kinetic, damage_thermal, action_delay)
    VALUES
        ((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone'),
         15,     -- explosion_radius (meters); read via Drone.ED.Config.explosion_radius
         2000,   -- damage_chemical
         2000,   -- damage_explosive
         2000,   -- damage_kinetic
         2000,   -- damage_thermal
         8000);  -- action_delay (ms); HunterSelfDestructAI.Enter() falls back to an 8s default (with a
                 -- logged warning) if this is ever 0/NULL/negative, but this row must still carry a
                 -- positive value so that fallback path is never silently relied upon in normal operation.
END;

-- 3c. Shared RCU ammo item. `turretId` is the chassis's own (dynamically-resolved) definition id,
-- hex-encoded to match GenxyWriter.WriteHexInteger's format exactly (see deviation #3 above).

DECLARE @hunterDroneDefinition INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone');
DECLARE @hunterDroneTurretIdHex VARCHAR(20) = FORMAT(@hunterDroneDefinition, 'x');

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_rcu', 1,
         0,
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_drones_units'),
         '#turretType=$HunterDrone#turretId=i' + @hunterDroneTurretIdHex,
         N'Remote-control charge for deploying an autonomous hunter drone. Loaded into a PvE or PvP hunter remote controller module.',
         1, 10, 50, 0, 100, N'def_standard_hunter_drone_rcu', 1, 1, 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu')
      AND af.name = 'remote_control_bandwidth_usage'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu'), id, 1
    FROM aggregatefields WHERE name = 'remote_control_bandwidth_usage';
END;

-- 3d. HunterRemoteControllerModulePvE definition.
-- attributeflags = 16 = activeModule (bit 4) only -- see deviation #5 above for why forceOneCycle is not
-- also set here (unlike SelfDestructModule's 2097168 in Part 2).
-- moduleFlag i90a continues Part 2's i909 numbering; verify both i90a and i90b (PvP, below) are free
-- against a live DB before applying, same caveat Part 2 carries for i909.

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pve')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_remote_controller_pve', 1,
         16, -- activeModule (bit 4)
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers_pve'),
         '#moduleFlag=i90a#tier=$tierlevel_t1#ammoCapacity=i1',
         N'Deploys an autonomous hunter drone that hunts and self-destructs on Niani NPCs.',
         1, 100, 500, 0, 100, N'def_standard_hunter_remote_controller_pve', 1, 1, 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pve')
      AND af.name = 'detection_range'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pve'), id, 100
    FROM aggregatefields WHERE name = 'detection_range';
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pve')
      AND af.name = 'remote_control_bandwidth_max'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pve'), id, 1
    FROM aggregatefields WHERE name = 'remote_control_bandwidth_max';
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pve')
      AND af.name = 'remote_control_operational_range'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pve'), id, 150
    FROM aggregatefields WHERE name = 'remote_control_operational_range';
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pve')
      AND af.name = 'remote_control_lifetime'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pve'), id, 1800000
    FROM aggregatefields WHERE name = 'remote_control_lifetime';
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pve')
      AND af.name = 'cycle_time'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pve'), id, 5000
    FROM aggregatefields WHERE name = 'cycle_time';
END;

-- 3e. HunterRemoteControllerModulePvP definition -- identical shape/values to 3d, distinct category and
-- moduleFlag (per Task 6: PvE vs. PvP is a module-level split, not a stat split).

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pvp')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_remote_controller_pvp', 1,
         16, -- activeModule (bit 4)
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers_pvp'),
         '#moduleFlag=i90b#tier=$tierlevel_t1#ammoCapacity=i1',
         N'Deploys an autonomous hunter drone that hunts and self-destructs on hostile-standing players.',
         1, 100, 500, 0, 100, N'def_standard_hunter_remote_controller_pvp', 1, 1, 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pvp')
      AND af.name = 'detection_range'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pvp'), id, 100
    FROM aggregatefields WHERE name = 'detection_range';
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pvp')
      AND af.name = 'remote_control_bandwidth_max'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pvp'), id, 1
    FROM aggregatefields WHERE name = 'remote_control_bandwidth_max';
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pvp')
      AND af.name = 'remote_control_operational_range'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pvp'), id, 150
    FROM aggregatefields WHERE name = 'remote_control_operational_range';
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pvp')
      AND af.name = 'remote_control_lifetime'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pvp'), id, 1800000
    FROM aggregatefields WHERE name = 'remote_control_lifetime';
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pvp')
      AND af.name = 'cycle_time'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_pvp'), id, 5000
    FROM aggregatefields WHERE name = 'cycle_time';
END;

-- This is intentionally scoped to entity definitions + config only -- not production recipes, research
-- levels, tech tree placement, or prototype linkage (guide sections 8-17). Per the design spec's own
-- "Content Required" section, tech tree placement is conditional ("if the items are researchable/
-- craftable") and needs a live-DB decision about where these fit in the existing tech tree, which
-- requires data not available in the docs -- flagging this explicitly rather than fabricating it, per
-- CLAUDE.md's instruction to ask rather than guess when dynamic resolution needs live data not available
-- here. All `purchasable = 1` definitions above (the RCU ammo and both controller modules) are therefore
-- not actually obtainable through any in-game flow yet (no production recipe, no market listing, no tech
-- tree node) -- purchasable/enabled flags describe intent for when that follow-up content exists, not a
-- claim that these items are reachable today.
