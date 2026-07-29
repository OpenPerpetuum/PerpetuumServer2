-- IMPROVEMENT-043: Hunter Drones with Self-Destruct Module
--
-- Consolidated, from-scratch content migration for this feature. Supersedes and replaces the following
-- migration files, whose fixes are all baked directly into the values below (those files should be
-- deleted once this one is applied): IMPROVEMENT-043-fix-moduleflag.sql,
-- IMPROVEMENT-043-hunter-drone-robot-parts.sql, IMPROVEMENT-043-hunter-drone-redesign.sql,
-- IMPROVEMENT-043-fix-hunter-ammo-stackable.sql.
--
-- All INSERTs are idempotent (IF NOT EXISTS guarded) and definitions/ids are always resolved
-- dynamically (by name, never hardcoded), per docs/content/claude_game_content_guide.md.
--
-- Corresponding C# (already committed, not part of this SQL):
--   src/Perpetuum/Modules/SelfDestructModule.cs
--   src/Perpetuum/Zones/Effects/SelfDestructDetonation.cs, SelfDestructCountdownEffect.cs
--   src/Perpetuum/Zones/NpcSystem/AI/HunterDrones/*.cs
--   src/Perpetuum/Zones/RemoteControl/HunterDrone.cs, TurretType.cs
--   src/Perpetuum/Modules/RemoteControl/HunterRemoteControllerModule.cs
--   src/Perpetuum.Bootstrapper/Modules/EntitiesModule.cs
--   src/Perpetuum.ExportedTypes/CategoryFlags.cs
--   src/Perpetuum/Units/Unit.cs (Unit.BypassZoneProtectionOnExplosion opt-out point)
--
-- Damage design: self-destruct deals no bespoke damage. Unit.OnDead always calls DoExplosion(), which
-- deals AoE damage scaled by the dying unit's own ArmorMax and current Core ratio
-- (src/Perpetuum/Units/Unit.cs's GetExplosionDamageBuilder:
-- damage = (sin(coreRatio * pi) + 1) * (armorMax * 0.1), peaking at coreRatio == 0.5).
-- SelfDestructDetonation.Arm drains Core to exactly 50% of CoreMax and applies a large
-- effect_core_recharge_time_modifier debuff for the countdown's duration so passive regen can't drift
-- the ratio away from that peak before detonation; Detonate is just owner.Kill(owner). This is why
-- definitionconfig rows below carry only action_delay, never damage_*/explosion_radius. DoExplosion()
-- also no-ops in Protected (Alpha/PvE-island) zones by default (so incidental deaths don't splash
-- damage in a safe zone) -- HunterDrone opts out via Unit.BypassZoneProtectionOnExplosion, since its
-- kamikaze detonation is a deliberate attack, not incidental splash.

-- ============================================================================
-- Part 1: effect + aggregate field rows for the self-destruct countdown.
--
-- effects.id/aggregatefields.id are IDENTITY(1,1) columns; SET IDENTITY_INSERT is required to insert
-- explicit ids that must match the C# enum values exactly (EffectType.cs / AggregateField.cs).
-- effectcategory is 0 (undefined): effectcategories' key is a [Flags] enum bit, not an auto id, and
-- nothing queries this effect by category.
-- ============================================================================

SET IDENTITY_INSERT effects ON;

IF NOT EXISTS (SELECT 1 FROM effects WHERE id = 140)
BEGIN
    INSERT INTO effects
        (id, name, description, effectcategory, duration, isaura, auraradius, ispositive, display, saveable)
    VALUES
        (140, N'effect_self_destruct_countdown', N'A self-destruct countdown is armed and ticking on this unit.',
         0, 0, 0, 0, 0, 1, 0);
END;

SET IDENTITY_INSERT effects OFF;

SET IDENTITY_INSERT aggregatefields ON;

-- self_destruct_config_* (760-764) are not currently read by any code path (the damage/radius values
-- they once carried between Arm() and Detonate() were removed in favor of DoExplosion() -- see the
-- damage design note above). Kept for schema completeness / consistency with the still-present
-- AggregateField enum members; candidates for removal alongside those enum members if this feature's
-- damage design is considered final.
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

-- ============================================================================
-- Part 2: player-piloted SelfDestructModule.
--
-- attributeflags = 2097176 = onePerRobot(3) | activeModule(4) | forceOneCycle(21). forceOneCycle forces
-- every activation into ModuleStateType.Oneshot (ActiveModule.States.cs), guaranteeing OnAction() fires
-- exactly once per activation -- required since this definition has no cycle_time aggregatevalue.
-- No ammo_required: this module doesn't consume ammo.
--
-- moduleFlag=i8: moduleFlag is a SlotFlags bitmask (src/Perpetuum/Modules/SlotFlags.cs), not an
-- incrementing id -- RobotComponent.IsValidSlotTo requires every bit set in a module's moduleFlag to
-- also be set in the target slot's slotFlags mask. i8 = 0x8 = SlotFlags.head only (no size/specialized
-- bits) -- the convention used by every existing head-slot module with no size class (def_standard_
-- neuralyzer, all four def_standard_*_remote_controller modules).
--
-- cf_self_destruct_modules = 0x0000000000000D0F was added to CategoryFlags.cs as a new module-family
-- member (byte0=0x0F = "module" family under cf_robot_equipment, byte1=0x0D = next free subgroup after
-- cf_robot_enhancements' 0x0C); EntitiesModule.cs resolves SelfDestructModule strictly via
-- ByCategoryFlags<SelfDestructModule>(CategoryFlags.cf_self_destruct_modules) -- this value must stay in
-- sync with that enum member.
-- ============================================================================

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
         2097176, -- onePerRobot | activeModule | forceOneCycle -- see note above
         (SELECT value FROM categoryFlags WHERE name = 'cf_self_destruct_modules'),
         '#moduleFlag=i8#tier=$tierlevel_t1',
         N'Kamikaze self-destruct module: arms an un-cancellable delayed detonation that kills the owner.',
         1, 100, 500, 0, 100, N'def_standard_self_destruct_module', 1, 1, 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM definitionconfig
    WHERE definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module')
)
BEGIN
    INSERT INTO definitionconfig (definition, action_delay)
    VALUES
        ((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module'),
         8000); -- action_delay (ms); starting balance, not final tuning -- flag for playtesting.
                 -- SelfDestructModule.OnAction() falls back to an 8s default if this is ever 0/NULL, but
                 -- this row must still carry a positive value so that fallback path is never silently
                 -- relied upon in normal operation.
END;

-- ============================================================================
-- Part 3: categoryflags for the hunter drone family.
-- Exact hex values from src/Perpetuum.ExportedTypes/CategoryFlags.cs.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM categoryFlags WHERE name = 'cf_hunter_drones')
BEGIN
    INSERT INTO categoryFlags (value, name, note, hidden, isunique)
    VALUES (0x0000000000051101, 'cf_hunter_drones', 'Autonomous kamikaze hunter drone chassis (PvE/PvP)', 0, 0);
END;

IF NOT EXISTS (SELECT 1 FROM categoryFlags WHERE name = 'cf_hunter_drones_units')
BEGIN
    INSERT INTO categoryFlags (value, name, note, hidden, isunique)
    VALUES (0x000000000008120A, 'cf_hunter_drones_units', 'Hunter drone RCU ammo (PvE/PvP, one controller)', 0, 0);
END;

IF NOT EXISTS (SELECT 1 FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers')
BEGIN
    INSERT INTO categoryFlags (value, name, note, hidden, isunique)
    VALUES (0x00000000060C040F, 'cf_hunter_remote_controllers', 'Hunter drone remote controller module', 0, 0);
END;

-- ============================================================================
-- Part 4: shared component parts (head/chassis/leg/inventory) for both drone variants.
--
-- Stats copied verbatim from def_syndicate_attack_drone's real parts (attack drone chosen as the base
-- for speed: speed_max 3.083 vs. an assault-drone-based 1.847), except armor_max, bumped from attack
-- drone's stock 1500 to 4400 (matching assault drone) -- DoExplosion()'s damage scales directly with
-- ArmorMax, and stock attack drone armor would produce a weak detonation. core_max is left at attack
-- drone's stock 240: the Core-drain-to-50%-of-CoreMax design (see damage design note above) makes the
-- damage multiplier ratio-based, not size-based, so a larger core pool buys nothing here.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_head')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_head', 1,
         1024, -- nonStackable -- matches def_syndicate_attack_drone_head verbatim
         (SELECT value FROM categoryFlags WHERE name = 'cf_robot_head'),
         '#height=f0.01#slotFlags=48,8,8,8', -- copied verbatim from def_syndicate_attack_drone_head
         N'Hunter drone head component (attack-drone-based).',
         1, 3.0, 300.0, 1, 100.0, 0, NULL, NULL);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_head')
      AND af.name = 'blob_emission'
)
BEGIN
    DECLARE @headDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_head');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @headDef, id, v.value
    FROM aggregatefields af
    CROSS APPLY (VALUES
        ('blob_emission', 4.0),
        ('blob_emission_radius', 15.0),
        ('blob_level_high', 280.0),
        ('blob_level_low', 80.0),
        ('cpu_max', 155.0),
        ('detection_strength', 125.0),
        ('locked_targets_max', 3.0),
        ('locking_range', 19.0),
        ('locking_time', 12500.0),
        ('sensor_strength', 100.0),
        ('stealth_strength', 105.0)
    ) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_chassis')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_chassis', 1,
         1024, -- nonStackable -- matches def_syndicate_attack_drone_chassis verbatim
         (SELECT value FROM categoryFlags WHERE name = 'cf_robot_chassis'),
         '#height=f0.45#slotFlags=4451,6d1,451,6d1', -- copied verbatim from def_syndicate_attack_drone_chassis
         N'Hunter drone chassis component (attack-drone-based, armor bumped for detonation damage).',
         1, 8.0, 8400.0, 1, 100.0, 0, NULL, NULL);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_chassis')
      AND af.name = 'armor_max'
)
BEGIN
    DECLARE @chassisDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_chassis');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @chassisDef, id, v.value
    FROM aggregatefields af
    CROSS APPLY (VALUES
        ('ammo_reload_time', 10000.0),
        ('armor_max', 4400.0), -- bumped from attack drone's stock 1500.0 -- see Part 4 note above
        ('core_max', 240.0), -- attack drone stock -- damage multiplier is ratio-based, not size-based
        ('core_recharge_time', 120.0),
        ('mine_detection_range', 7.0),
        ('powergrid_max', 150.0),
        ('reactor_radiation', 3.0),
        ('resist_chemical', 45.0),
        ('resist_explosive', 45.0),
        ('resist_kinetic', 45.0),
        ('resist_thermal', 45.0),
        ('signature_radius', 4.5)
    ) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_leg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_leg', 1,
         1024, -- nonStackable -- matches def_syndicate_attack_drone_leg verbatim
         (SELECT value FROM categoryFlags WHERE name = 'cf_robot_leg'),
         '#height=f0.35#slotFlags=420,20,20', -- copied verbatim from def_syndicate_attack_drone_leg
         N'Hunter drone leg component (attack-drone-based, faster than an assault-drone base).',
         1, 3.0, 1700.0, 1, 100.0, 0, NULL, NULL);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_leg')
      AND af.name = 'speed_max'
)
BEGIN
    DECLARE @legDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_leg');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @legDef, id, v.value
    FROM aggregatefields af
    CROSS APPLY (VALUES
        ('slope', 56.0),
        ('speed_max', 3.083)
    ) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_inventory')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_inventory', 1,
         4195336, -- onePerRobot | nonStackable | nonrelocatable -- matches def_syndicate_attack_drone_inventory verbatim
         (SELECT value FROM categoryFlags WHERE name = 'cf_robot_inventory'),
         '#capacity=f120.0',
         N'Hunter drone inventory component.',
         1, 0.0, 0.0, 0, 100.0, 0, NULL, NULL);
END;

-- ============================================================================
-- Part 5: two chassis (PvE/PvP), sharing the Part 4 component parts.
-- No chassisModules on either robottemplates row: HunterDrone has no equipped weapons -- its only
-- attack is the kamikaze detonation (see damage design note above).
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pve')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, note, enabled, volume, mass, hidden, health, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_pve', 1,
         1024, -- nonStackable
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_drones'),
         N'Autonomous kamikaze drone chassis (PvE): hunts Niani NPCs and self-destructs on contact.',
         1, 50.0, 200.0, 0, 1500.0, 0, 1, 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM definitionconfig dc
    WHERE dc.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pve')
)
BEGIN
    INSERT INTO definitionconfig (definition, action_delay)
    VALUES ((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pve'), 8000);
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pvp')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, note, enabled, volume, mass, hidden, health, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_pvp', 1,
         1024, -- nonStackable
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_drones'),
         N'Autonomous kamikaze drone chassis (PvP): hunts hostile-standing players and self-destructs on contact.',
         1, 50.0, 200.0, 0, 1500.0, 0, 1, 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM definitionconfig dc
    WHERE dc.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pvp')
)
BEGIN
    INSERT INTO definitionconfig (definition, action_delay)
    VALUES ((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pvp'), 8000);
END;

IF NOT EXISTS (SELECT 1 FROM robottemplates WHERE name = 'standard_hunter_drone_pve')
BEGIN
    DECLARE @robotPveDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pve');
    DECLARE @tplHeadDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_head');
    DECLARE @tplChassisDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_chassis');
    DECLARE @tplLegDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_leg');
    DECLARE @tplInventoryDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_inventory');

    DECLARE @pveDescription NVARCHAR(200) =
        '#robot=i' + FORMAT(@robotPveDef, 'x') +
        '#head=i' + FORMAT(@tplHeadDef, 'x') +
        '#chassis=i' + FORMAT(@tplChassisDef, 'x') +
        '#leg=i' + FORMAT(@tplLegDef, 'x') +
        '#container=i' + FORMAT(@tplInventoryDef, 'x');

    INSERT INTO robottemplates (name, description) VALUES ('standard_hunter_drone_pve', @pveDescription);
END;

IF NOT EXISTS (
    SELECT 1 FROM robottemplaterelation
    WHERE definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pve')
)
BEGIN
    INSERT INTO robottemplaterelation (definition, templateid, itemscoresum, raceid, missionlevel, missionleveloverride, killep, note)
    VALUES (
        (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pve'),
        (SELECT id FROM robottemplates WHERE name = 'standard_hunter_drone_pve'),
        0, 0, NULL, NULL, NULL,
        N'Hunter drone (PvE autonomous kamikaze drone)'
    );
END;

IF NOT EXISTS (SELECT 1 FROM robottemplates WHERE name = 'standard_hunter_drone_pvp')
BEGIN
    DECLARE @robotPvpDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pvp');
    DECLARE @tplHeadDef2 INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_head');
    DECLARE @tplChassisDef2 INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_chassis');
    DECLARE @tplLegDef2 INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_leg');
    DECLARE @tplInventoryDef2 INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_inventory');

    DECLARE @pvpDescription NVARCHAR(200) =
        '#robot=i' + FORMAT(@robotPvpDef, 'x') +
        '#head=i' + FORMAT(@tplHeadDef2, 'x') +
        '#chassis=i' + FORMAT(@tplChassisDef2, 'x') +
        '#leg=i' + FORMAT(@tplLegDef2, 'x') +
        '#container=i' + FORMAT(@tplInventoryDef2, 'x');

    INSERT INTO robottemplates (name, description) VALUES ('standard_hunter_drone_pvp', @pvpDescription);
END;

IF NOT EXISTS (
    SELECT 1 FROM robottemplaterelation
    WHERE definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pvp')
)
BEGIN
    INSERT INTO robottemplaterelation (definition, templateid, itemscoresum, raceid, missionlevel, missionleveloverride, killep, note)
    VALUES (
        (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pvp'),
        (SELECT id FROM robottemplates WHERE name = 'standard_hunter_drone_pvp'),
        0, 0, NULL, NULL, NULL,
        N'Hunter drone (PvP autonomous kamikaze drone)'
    );
END;

-- ============================================================================
-- Part 6: single merged remote controller module.
--
-- One controller class + one shared ammoCategoryFlags registration, with drone variant selected
-- per-ammo via TurretType/TurretId -- mirrors IndustrialRemoteControllerModule's pattern (one class,
-- multiple ammo items picking the variant), not one controller subclass per variant.
--
-- attributeflags = 2359320 = onePerRobot(3) | activeModule(4) | ammo_required(18) | forceOneCycle(21) --
-- matches def_standard_assault_remote_controller (and every sibling RemoteControllerModule) verbatim.
-- ammo_required specifically matters beyond matching the sibling: Perpetuum.AdminTool's
-- RobotTemplateEditorEntity.IsAmmoable treats this bit as the authoritative "needs ammo" signal (not
-- options.ammoCapacity/ammoType), so without it the structured editor won't offer an ammo dropdown.
--
-- moduleFlag=i8: same SlotFlags.head-only bitmask as Part 2's SelfDestructModule.
-- ammoType=L8120a: every ammoable module's options carries an ammoType equal to its ammo's categoryFlags
-- value, hex-encoded with no leading zeroes and GenxyToken.Long-prefixed ('L', parsed as hex) --
-- cf_hunter_drones_units = 0x000000000008120A, hence L8120a (matches EntitiesModule.cs's
-- ByCategoryFlags<HunterRemoteControllerModule>(..., ammoCategoryFlags: cf_hunter_drones_units)). Not
-- read by server-side code, but Perpetuum.AdminTool's RobotTemplateSlotViewModel filters ammo
-- candidates by it -- omitting it breaks that tooling.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_remote_controller', 1,
         2359320, -- onePerRobot | activeModule | ammo_required | forceOneCycle -- see note above
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers'),
         '#moduleFlag=i8#tier=$tierlevel_t1#ammoCapacity=i1#ammoType=L8120a',
         N'Deploys an autonomous hunter drone -- PvE ammo hunts Niani NPCs, PvP ammo hunts hostile-standing players.',
         1, 100, 500, 0, 100, N'def_standard_hunter_remote_controller', 1, 1, 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller')
      AND af.name = 'detection_range'
)
BEGIN
    DECLARE @controllerDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @controllerDef, id, v.value
    FROM aggregatefields af
    CROSS APPLY (VALUES
        ('detection_range', 100.0),
        ('remote_control_bandwidth_max', 1.0),
        ('remote_control_operational_range', 150.0),
        ('remote_control_lifetime', 1800000.0),
        ('cycle_time', 5000.0)
    ) AS v(name, value)
    WHERE af.name = v.name;
END;

-- ============================================================================
-- Part 7: two ammo items (PvE/PvP), sharing cf_hunter_drones_units -- matches how all four
-- def_*_attack_drone_unit race variants share one category with one controller class.
--
-- attributeflags = 2048 (alwaysStackable) on both: required by Container.RemoveItemByDefinition's
-- `ed.AttributeFlags.AlwaysStackable.ThrowIfFalse(ErrorCodes.DefinitionNotSupported)` guard, exercised
-- when reloading the controller module from cargo -- confirmed live against every other RCU drone-charge
-- ammo item (def_mining_industrial_drone_unit, def_harvesting_industrial_drone_unit,
-- def_syndicate_attack_drone_unit and race siblings), which all use 2048.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve')
BEGIN
    DECLARE @pveChassisDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pve');
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_rcu_pve', 1,
         2048, -- alwaysStackable -- see note above
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_drones_units'),
         '#turretType=$HunterDronePvE#turretId=i' + FORMAT(@pveChassisDef, 'x'),
         N'Hunter drone RCU charge (PvE).',
         1, 10.0, 50.0, 0, 100.0, 1, 1, 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN entitydefaults ed ON ed.definition = av.definition
    JOIN aggregatefields af ON af.id = av.field
    WHERE ed.definitionname = 'def_standard_hunter_drone_rcu_pve' AND af.name = 'remote_control_bandwidth_usage'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve'), id, 1.0
    FROM aggregatefields WHERE name = 'remote_control_bandwidth_usage';
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp')
BEGIN
    DECLARE @pvpChassisDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_pvp');
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_rcu_pvp', 1,
         2048, -- alwaysStackable -- see note above
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_drones_units'),
         '#turretType=$HunterDronePvP#turretId=i' + FORMAT(@pvpChassisDef, 'x'),
         N'Hunter drone RCU charge (PvP).',
         1, 10.0, 50.0, 0, 100.0, 1, 1, 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN entitydefaults ed ON ed.definition = av.definition
    JOIN aggregatefields af ON af.id = av.field
    WHERE ed.definitionname = 'def_standard_hunter_drone_rcu_pvp' AND af.name = 'remote_control_bandwidth_usage'
)
BEGIN
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp'), id, 1.0
    FROM aggregatefields WHERE name = 'remote_control_bandwidth_usage';
END;

-- Intentionally scoped to entity definitions + config only -- not production recipes, research levels,
-- tech tree placement, or prototype linkage. All purchasable=1 definitions above (the RCU ammo and the
-- controller module) are therefore not yet obtainable through any in-game flow (no production recipe,
-- no market listing, no tech tree node) -- purchasable/enabled flags describe intent for when that
-- follow-up content exists, not a claim that these items are reachable today.
