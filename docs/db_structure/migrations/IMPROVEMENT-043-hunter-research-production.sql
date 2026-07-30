-- IMPROVEMENT-043 follow-up: named tiers, research, production for Self-Destruct Module and Hunter
-- Remote Controller, plus research/production for the existing Hunter Drone RCU ammo items.
--
-- Follow-up to docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql (still
-- unapplied), whose closing comment explicitly scoped out production recipes, research levels, tech
-- tree placement, and prototype linkage. Design: docs/superpowers/specs/2026-07-30-improvement-043-
-- hunter-research-production-design.md.
--
-- All INSERTs are idempotent and every definition/category/aggregatefield id is resolved dynamically by
-- name, per docs/content/claude_game_content_guide.md. Not applied to any DB by this commit -- generated
-- for manual review/application per standing project practice.

USE perpetuumsa
GO

-- ============================================================================
-- Part 1: Self-Destruct Module -- T1 fitting-cost fix + T2-T4 tiers, prototypes, calibration templates.
--
-- T1 (def_standard_self_destruct_module) currently has no cpu/core/powergrid_usage at all (missing from
-- the original migration). Baseline values below are a fresh starting-balance estimate for a simple
-- one-shot combat module (no directly comparable sibling exists) -- flagged for playtesting, same as
-- every other numeric value in this feature's history.
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av
    JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module')
      AND af.name = 'cpu_usage'
)
BEGIN
    DECLARE @sdT1Def INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @sdT1Def, id, v.value
    FROM aggregatefields af
    CROSS APPLY (VALUES
        ('cpu_usage', 40.0),
        ('core_usage', 50.0),
        ('powergrid_usage', 20.0)
    ) AS v(name, value)
    WHERE af.name = v.name;
END;
GO

-- T2 (named1)

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named1_self_destruct_module', 1,
         2097176,
         (SELECT value FROM categoryFlags WHERE name = 'cf_self_destruct_modules'),
         '#moduleFlag=i8#tier=$tierlevel_t2',
         N'Kamikaze self-destruct module: arms an un-cancellable delayed detonation that kills the owner.',
         1, 100, 450, 0, 100, N'def_standard_self_destruct_module', 1, 1, 2);
END;

IF NOT EXISTS (
    SELECT 1 FROM definitionconfig
    WHERE definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module')
)
BEGIN
    INSERT INTO definitionconfig (definition, action_delay)
    VALUES ((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module'), 7500);
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named1_self_destruct_module_pr', 1,
         2097176,
         (SELECT value FROM categoryFlags WHERE name = 'cf_self_destruct_modules'),
         '#moduleFlag=i8#tier=$tierlevel_t2_pr',
         N'Kamikaze self-destruct module: arms an un-cancellable delayed detonation that kills the owner.',
         1, 100, 400, 0, 100, N'def_standard_self_destruct_module', 1, 2, 2);
END;

IF NOT EXISTS (
    SELECT 1 FROM definitionconfig
    WHERE definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr')
)
BEGIN
    INSERT INTO definitionconfig (definition, action_delay)
    VALUES ((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr'), 7500);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module') AND af.name = 'cpu_usage'
)
BEGIN
    DECLARE @sdT2Def INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @sdT2Def, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES ('cpu_usage', 45.0), ('core_usage', 55.0), ('powergrid_usage', 22.0)) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr') AND af.name = 'cpu_usage'
)
BEGIN
    DECLARE @sdT2PrDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @sdT2PrDef, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES ('cpu_usage', 43.0), ('core_usage', 55.0), ('powergrid_usage', 21.0)) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_cprg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named1_self_destruct_module_cprg', 1, 1024,
         (SELECT value FROM categoryflags WHERE name = 'cf_module_calibration_programs'),
         '#tier=$tierlevel_t2', '', 1, 0.01, 0.1, 0, 100, N'calibration_program_desc', 0, 1, 2);
END;

-- T3 (named2)

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named2_self_destruct_module', 1,
         2097176,
         (SELECT value FROM categoryFlags WHERE name = 'cf_self_destruct_modules'),
         '#moduleFlag=i8#tier=$tierlevel_t3',
         N'Kamikaze self-destruct module: arms an un-cancellable delayed detonation that kills the owner.',
         1, 100, 450, 0, 100, N'def_standard_self_destruct_module', 1, 1, 3);
END;

IF NOT EXISTS (
    SELECT 1 FROM definitionconfig
    WHERE definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module')
)
BEGIN
    INSERT INTO definitionconfig (definition, action_delay)
    VALUES ((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module'), 7000);
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named2_self_destruct_module_pr', 1,
         2097176,
         (SELECT value FROM categoryFlags WHERE name = 'cf_self_destruct_modules'),
         '#moduleFlag=i8#tier=$tierlevel_t3_pr',
         N'Kamikaze self-destruct module: arms an un-cancellable delayed detonation that kills the owner.',
         1, 100, 400, 0, 100, N'def_standard_self_destruct_module', 1, 2, 3);
END;

IF NOT EXISTS (
    SELECT 1 FROM definitionconfig
    WHERE definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr')
)
BEGIN
    INSERT INTO definitionconfig (definition, action_delay)
    VALUES ((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr'), 7000);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module') AND af.name = 'cpu_usage'
)
BEGIN
    DECLARE @sdT3Def INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @sdT3Def, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES ('cpu_usage', 50.0), ('core_usage', 60.0), ('powergrid_usage', 24.0)) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr') AND af.name = 'cpu_usage'
)
BEGIN
    DECLARE @sdT3PrDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @sdT3PrDef, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES ('cpu_usage', 48.0), ('core_usage', 60.0), ('powergrid_usage', 23.0)) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_cprg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named2_self_destruct_module_cprg', 1, 1024,
         (SELECT value FROM categoryflags WHERE name = 'cf_module_calibration_programs'),
         '#tier=$tierlevel_t3', '', 1, 0.01, 0.1, 0, 100, N'calibration_program_desc', 0, 1, 3);
END;

-- T4 (named3)

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named3_self_destruct_module', 1,
         2097176,
         (SELECT value FROM categoryFlags WHERE name = 'cf_self_destruct_modules'),
         '#moduleFlag=i8#tier=$tierlevel_t4',
         N'Kamikaze self-destruct module: arms an un-cancellable delayed detonation that kills the owner.',
         1, 100, 450, 0, 100, N'def_standard_self_destruct_module', 1, 1, 4);
END;

IF NOT EXISTS (
    SELECT 1 FROM definitionconfig
    WHERE definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module')
)
BEGIN
    INSERT INTO definitionconfig (definition, action_delay)
    VALUES ((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module'), 6500);
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named3_self_destruct_module_pr', 1,
         2097176,
         (SELECT value FROM categoryFlags WHERE name = 'cf_self_destruct_modules'),
         '#moduleFlag=i8#tier=$tierlevel_t4_pr',
         N'Kamikaze self-destruct module: arms an un-cancellable delayed detonation that kills the owner.',
         1, 100, 400, 0, 100, N'def_standard_self_destruct_module', 1, 2, 4);
END;

IF NOT EXISTS (
    SELECT 1 FROM definitionconfig
    WHERE definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr')
)
BEGIN
    INSERT INTO definitionconfig (definition, action_delay)
    VALUES ((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr'), 6500);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module') AND af.name = 'cpu_usage'
)
BEGIN
    DECLARE @sdT4Def INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @sdT4Def, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES ('cpu_usage', 55.0), ('core_usage', 65.0), ('powergrid_usage', 26.0)) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr') AND af.name = 'cpu_usage'
)
BEGIN
    DECLARE @sdT4PrDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @sdT4PrDef, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES ('cpu_usage', 53.0), ('core_usage', 65.0), ('powergrid_usage', 25.0)) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_cprg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named3_self_destruct_module_cprg', 1, 1024,
         (SELECT value FROM categoryflags WHERE name = 'cf_module_calibration_programs'),
         '#tier=$tierlevel_t4', '', 1, 0.01, 0.1, 0, 100, N'calibration_program_desc', 0, 1, 4);
END;

-- Standard (T1) calibration template -- did not exist before this migration.

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module_cprg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_self_destruct_module_cprg', 1, 1024,
         (SELECT value FROM categoryflags WHERE name = 'cf_module_calibration_programs'),
         '#tier=$tierlevel_t1', '', 1, 0.01, 0.1, 0, 100, N'calibration_program_desc', 0, 1, 1);
END;
GO

-- ============================================================================
-- Part 3: Hunter Remote Controller -- T1 fitting-cost fix + T2-T4 tiers, prototypes, calibration templates.
--
-- cpu/core/powergrid baseline reused from def_standard_assault_remote_controller (closest real
-- combat-role controller sibling): cpu 250 / core 150 / powergrid 65.
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller')
      AND af.name = 'cpu_usage'
)
BEGIN
    DECLARE @hrcT1Def INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @hrcT1Def, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES ('cpu_usage', 250.0), ('core_usage', 150.0), ('powergrid_usage', 65.0)) AS v(name, value)
    WHERE af.name = v.name;
END;
GO

-- T2 (named1)

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named1_hunter_remote_controller', 1,
         2359320,
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers'),
         '#moduleFlag=i8#tier=$tierlevel_t2#ammoCapacity=i1#ammoType=L8120a',
         N'Deploys an autonomous hunter drone -- PvE ammo hunts Niani NPCs, PvP ammo hunts hostile-standing players.',
         1, 100, 450, 0, 100, N'def_standard_hunter_remote_controller', 1, 1, 2);
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_pr')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named1_hunter_remote_controller_pr', 1,
         2359320,
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers'),
         '#moduleFlag=i8#tier=$tierlevel_t2_pr#ammoCapacity=i1#ammoType=L8120a',
         N'Deploys an autonomous hunter drone -- PvE ammo hunts Niani NPCs, PvP ammo hunts hostile-standing players.',
         1, 100, 400, 0, 100, N'def_standard_hunter_remote_controller', 1, 2, 2);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller') AND af.name = 'detection_range'
)
BEGIN
    DECLARE @hrcT2Def INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @hrcT2Def, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES
        ('detection_range', 110.0), ('remote_control_bandwidth_max', 1.0),
        ('remote_control_operational_range', 165.0), ('remote_control_lifetime', 1980000.0),
        ('cycle_time', 4500.0), ('cpu_usage', 260.0), ('core_usage', 155.0), ('powergrid_usage', 67.0)
    ) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_pr') AND af.name = 'detection_range'
)
BEGIN
    DECLARE @hrcT2PrDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_pr');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @hrcT2PrDef, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES
        ('detection_range', 110.0), ('remote_control_bandwidth_max', 1.0),
        ('remote_control_operational_range', 180.0), ('remote_control_lifetime', 2160000.0),
        ('cycle_time', 4500.0), ('cpu_usage', 255.0), ('core_usage', 155.0), ('powergrid_usage', 66.0)
    ) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_cprg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named1_hunter_remote_controller_cprg', 1, 1024,
         (SELECT value FROM categoryflags WHERE name = 'cf_module_calibration_programs'),
         '#tier=$tierlevel_t2', '', 1, 0.01, 0.1, 0, 100, N'calibration_program_desc', 0, 1, 2);
END;

-- T3 (named2)

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named2_hunter_remote_controller', 1,
         2359320,
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers'),
         '#moduleFlag=i8#tier=$tierlevel_t3#ammoCapacity=i1#ammoType=L8120a',
         N'Deploys an autonomous hunter drone -- PvE ammo hunts Niani NPCs, PvP ammo hunts hostile-standing players.',
         1, 100, 450, 0, 100, N'def_standard_hunter_remote_controller', 1, 1, 3);
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named2_hunter_remote_controller_pr', 1,
         2359320,
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers'),
         '#moduleFlag=i8#tier=$tierlevel_t3_pr#ammoCapacity=i1#ammoType=L8120a',
         N'Deploys an autonomous hunter drone -- PvE ammo hunts Niani NPCs, PvP ammo hunts hostile-standing players.',
         1, 100, 400, 0, 100, N'def_standard_hunter_remote_controller', 1, 2, 3);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller') AND af.name = 'detection_range'
)
BEGIN
    DECLARE @hrcT3Def INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @hrcT3Def, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES
        ('detection_range', 120.0), ('remote_control_bandwidth_max', 1.0),
        ('remote_control_operational_range', 180.0), ('remote_control_lifetime', 2160000.0),
        ('cycle_time', 4000.0), ('cpu_usage', 270.0), ('core_usage', 160.0), ('powergrid_usage', 69.0)
    ) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr') AND af.name = 'detection_range'
)
BEGIN
    DECLARE @hrcT3PrDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @hrcT3PrDef, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES
        ('detection_range', 120.0), ('remote_control_bandwidth_max', 1.0),
        ('remote_control_operational_range', 195.0), ('remote_control_lifetime', 2340000.0),
        ('cycle_time', 4000.0), ('cpu_usage', 265.0), ('core_usage', 160.0), ('powergrid_usage', 68.0)
    ) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_cprg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named2_hunter_remote_controller_cprg', 1, 1024,
         (SELECT value FROM categoryflags WHERE name = 'cf_module_calibration_programs'),
         '#tier=$tierlevel_t3', '', 1, 0.01, 0.1, 0, 100, N'calibration_program_desc', 0, 1, 3);
END;

-- T4 (named3)

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named3_hunter_remote_controller', 1,
         2359320,
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers'),
         '#moduleFlag=i8#tier=$tierlevel_t4#ammoCapacity=i1#ammoType=L8120a',
         N'Deploys an autonomous hunter drone -- PvE ammo hunts Niani NPCs, PvP ammo hunts hostile-standing players.',
         1, 100, 450, 0, 100, N'def_standard_hunter_remote_controller', 1, 1, 4);
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named3_hunter_remote_controller_pr', 1,
         2359320,
         (SELECT value FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers'),
         '#moduleFlag=i8#tier=$tierlevel_t4_pr#ammoCapacity=i1#ammoType=L8120a',
         N'Deploys an autonomous hunter drone -- PvE ammo hunts Niani NPCs, PvP ammo hunts hostile-standing players.',
         1, 100, 400, 0, 100, N'def_standard_hunter_remote_controller', 1, 2, 4);
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller') AND af.name = 'detection_range'
)
BEGIN
    DECLARE @hrcT4Def INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @hrcT4Def, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES
        ('detection_range', 130.0), ('remote_control_bandwidth_max', 1.0),
        ('remote_control_operational_range', 195.0), ('remote_control_lifetime', 2340000.0),
        ('cycle_time', 3500.0), ('cpu_usage', 280.0), ('core_usage', 165.0), ('powergrid_usage', 71.0)
    ) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (
    SELECT 1 FROM aggregatevalues av JOIN aggregatefields af ON af.id = av.field
    WHERE av.definition = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr') AND af.name = 'detection_range'
)
BEGIN
    DECLARE @hrcT4PrDef INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr');
    INSERT INTO aggregatevalues (definition, field, value)
    SELECT @hrcT4PrDef, id, v.value FROM aggregatefields af
    CROSS APPLY (VALUES
        ('detection_range', 130.0), ('remote_control_bandwidth_max', 1.0),
        ('remote_control_operational_range', 210.0), ('remote_control_lifetime', 2520000.0),
        ('cycle_time', 3500.0), ('cpu_usage', 275.0), ('core_usage', 165.0), ('powergrid_usage', 70.0)
    ) AS v(name, value)
    WHERE af.name = v.name;
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_cprg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_named3_hunter_remote_controller_cprg', 1, 1024,
         (SELECT value FROM categoryflags WHERE name = 'cf_module_calibration_programs'),
         '#tier=$tierlevel_t4', '', 1, 0.01, 0.1, 0, 100, N'calibration_program_desc', 0, 1, 4);
END;

-- Standard (T1) calibration template -- did not exist before this migration.

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_cprg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_remote_controller_cprg', 1, 1024,
         (SELECT value FROM categoryflags WHERE name = 'cf_module_calibration_programs'),
         '#tier=$tierlevel_t1', '', 1, 0.01, 0.1, 0, 100, N'calibration_program_desc', 0, 1, 1);
END;
GO
