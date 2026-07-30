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

-- ============================================================================
-- Part 4: Hunter Drone RCU ammo (PvE/PvP) -- calibration templates. These stay single-tier, matching
-- def_mining_industrial_drone_unit / def_syndicate_attack_drone_unit (single-tier ammo behind a tiered
-- controller).
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve_cprg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_rcu_pve_cprg', 1, 1024,
         (SELECT value FROM categoryflags WHERE name = 'cf_ammo_rcu_calibration_programs'),
         '#tier=$tierlevel_t1', '', 1, 0.01, 0.1, 0, 100, N'calibration_program_desc', 0, 1, 1);
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp_cprg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_rcu_pvp_cprg', 1, 1024,
         (SELECT value FROM categoryflags WHERE name = 'cf_ammo_rcu_calibration_programs'),
         '#tier=$tierlevel_t1', '', 1, 0.01, 0.1, 0, 100, N'calibration_program_desc', 0, 1, 1);
END;
GO

-- ============================================================================
-- Part 5: Production/prototyping materials.
--
-- Self-destruct module and hunter remote controller reuse def_standard/named1/2/3_remote_command_
-- translator's own recipe verbatim (same material family, same head-slot RemoteControl-class module),
-- applied independently to each chain. Hunter Drone RCU ammo reuses def_syndicate_attack_drone_unit's
-- recipe (closest combat-drone analog).
-- ============================================================================

DECLARE @titanium INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_titanium');
DECLARE @axicol INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_axicol');
DECLARE @axicoline INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_axicoline');
DECLARE @espitium INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_espitium');
DECLARE @hydrobenol INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_hydrobenol');
DECLARE @unimetal INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_unimetal');
DECLARE @polynitrocol INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_polynitrocol');
DECLARE @polynucleit INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_polynucleit');
DECLARE @phlobotil INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_phlobotil');
DECLARE @robotshardBasic INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_robotshard_common_basic');
DECLARE @robotshardAdvanced INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_robotshard_common_advanced');
DECLARE @robotshardExpert INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_robotshard_common_expert');

DECLARE @sdT1 INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module');
DECLARE @sdT2 INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module');
DECLARE @sdT3 INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module');

DECLARE @hrcT1 INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller');
DECLARE @hrcT2 INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller');
DECLARE @hrcT3 INT = (SELECT TOP 1 definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller');

DECLARE @tempComponents TABLE (definition INT, componentdefinition INT, componentamount INT);

-- Self-Destruct Module

INSERT INTO @tempComponents (definition, componentdefinition, componentamount) VALUES
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module'), @titanium, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module'), @axicol, 250),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module'), @axicoline, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module'), @espitium, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module'), @sdT1, 1),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr'), @titanium, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr'), @axicol, 250),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr'), @axicoline, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr'), @espitium, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr'), @sdT1, 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr'), @robotshardBasic, 120),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module'), @titanium, 100),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module'), @axicol, 125),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module'), @axicoline, 100),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module'), @espitium, 300),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module'), @hydrobenol, 100),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module'), @sdT2, 1),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr'), @titanium, 100),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr'), @axicol, 125),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr'), @axicoline, 100),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr'), @espitium, 300),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr'), @hydrobenol, 100),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr'), @sdT2, 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr'), @robotshardBasic, 80),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr'), @robotshardAdvanced, 80),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module'), @titanium, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module'), @axicol, 250),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module'), @axicoline, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module'), @espitium, 400),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module'), @hydrobenol, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module'), @unimetal, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module'), @sdT3, 1),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr'), @titanium, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr'), @axicol, 250),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr'), @axicoline, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr'), @espitium, 400),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr'), @hydrobenol, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr'), @unimetal, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr'), @sdT3, 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr'), @robotshardBasic, 60),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr'), @robotshardAdvanced, 120),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr'), @robotshardExpert, 180),

-- Hunter Remote Controller (identical recipe shape, own tier chain)

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller'), @titanium, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller'), @axicol, 250),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller'), @axicoline, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller'), @espitium, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller'), @hrcT1, 1),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_pr'), @titanium, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_pr'), @axicol, 250),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_pr'), @axicoline, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_pr'), @espitium, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_pr'), @hrcT1, 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_pr'), @robotshardBasic, 120),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller'), @titanium, 100),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller'), @axicol, 125),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller'), @axicoline, 100),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller'), @espitium, 300),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller'), @hydrobenol, 100),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller'), @hrcT2, 1),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr'), @titanium, 100),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr'), @axicol, 125),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr'), @axicoline, 100),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr'), @espitium, 300),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr'), @hydrobenol, 100),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr'), @hrcT2, 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr'), @robotshardBasic, 80),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr'), @robotshardAdvanced, 80),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller'), @titanium, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller'), @axicol, 250),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller'), @axicoline, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller'), @espitium, 400),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller'), @hydrobenol, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller'), @unimetal, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller'), @hrcT3, 1),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr'), @titanium, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr'), @axicol, 250),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr'), @axicoline, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr'), @espitium, 400),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr'), @hydrobenol, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr'), @unimetal, 200),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr'), @hrcT3, 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr'), @robotshardBasic, 60),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr'), @robotshardAdvanced, 120),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr'), @robotshardExpert, 180),

-- Hunter Drone RCU ammo (PvE/PvP), each independently, def_syndicate_attack_drone_unit's recipe

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve'), @titanium, 500),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve'), @unimetal, 25),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve'), @axicoline, 500),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve'), @espitium, 50),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve'), @polynitrocol, 500),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve'), @polynucleit, 500),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve'), @phlobotil, 500),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp'), @titanium, 500),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp'), @unimetal, 25),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp'), @axicoline, 500),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp'), @espitium, 50),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp'), @polynitrocol, 500),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp'), @polynucleit, 500),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp'), @phlobotil, 500);

MERGE components AS Target
USING (SELECT definition, componentdefinition, componentamount FROM @tempComponents) AS Source
ON (Target.definition = Source.definition AND Target.componentdefinition = Source.componentdefinition)
WHEN MATCHED THEN
    UPDATE SET Target.componentamount = Source.componentamount
WHEN NOT MATCHED BY TARGET THEN
    INSERT (definition, componentdefinition, componentamount)
    VALUES (Source.definition, Source.componentdefinition, Source.componentamount);
GO

-- ============================================================================
-- Part 6: Research levels.
--
-- Self-destruct module & hunter remote controller: standard tier researches directly on itself;
-- named1/2/3 research on their _pr prototype -- matches def_standard/named1/2/3_remote_command_
-- translator and _industrial_remote_controller exactly (researchlevel 5/6/7/8).
-- Hunter Drone RCU ammo: single-tier, researches directly on itself (researchlevel 5) -- matches
-- def_mining_industrial_drone_unit / def_syndicate_attack_drone_unit.
-- ============================================================================

DECLARE @tempResearch TABLE (definition INT, researchlevel INT, calibrationprogram INT, enabled BIT);

INSERT INTO @tempResearch (definition, researchlevel, calibrationprogram, enabled) VALUES
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module'), 5,
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module_cprg'), 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr'), 6,
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_cprg'), 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr'), 7,
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_cprg'), 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr'), 8,
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_cprg'), 1),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller'), 5,
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller_cprg'), 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_pr'), 6,
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_cprg'), 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr'), 7,
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_cprg'), 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr'), 8,
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_cprg'), 1),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve'), 5,
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve_cprg'), 1),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp'), 5,
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp_cprg'), 1);

MERGE itemresearchlevels AS Target
USING (SELECT definition, researchlevel, calibrationprogram, enabled FROM @tempResearch) AS Source
ON (Target.definition = Source.definition)
WHEN MATCHED THEN
    UPDATE SET
        Target.researchlevel = Source.researchlevel,
        Target.calibrationprogram = Source.calibrationprogram,
        Target.enabled = Source.enabled
WHEN NOT MATCHED BY TARGET THEN
    INSERT (definition, researchlevel, calibrationprogram, enabled)
    VALUES (Source.definition, Source.researchlevel, Source.calibrationprogram, Source.enabled);
GO

-- ============================================================================
-- Part 7: Tech tree placement.
--
-- Group 'common2' -- same group as remote_command_translator/industrial/support controller chains.
-- Verified live: techtree rows at y=36-45 in this group were empty before this migration.
-- Self-destruct module sits at y=36 (one row below remote_command_translator's y=35), same x positions
-- (1-4) as that chain. Hunter remote controller continues the branch at y=37, parented off the standard
-- self-destruct module node. Both Hunter Drone RCU ammo items hang off the standard hunter remote
-- controller node as siblings at x=2 (matching how mining/harvesting drone units both hang off
-- def_standard_industrial_remote_controller), at y=38/39 respectively.
-- ============================================================================

DECLARE @ttGroup INT = (SELECT TOP 1 id FROM [techtreegroups] WHERE name = 'common2');

DECLARE @tempTechtree TABLE (parentdefinition INT, childdefinition INT, groupID INT, x INT, y INT, enablerextensionid INT);

INSERT INTO @tempTechtree (parentdefinition, childdefinition, groupID, x, y, enablerextensionid) VALUES
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_cpu_upgrade'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module'), @ttGroup, 1, 36, NULL),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module'), @ttGroup, 2, 36, NULL),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module'), @ttGroup, 3, 36, NULL),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module'), @ttGroup, 4, 36, NULL),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller'), @ttGroup, 1, 37, NULL),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller'), @ttGroup, 2, 37, NULL),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller'), @ttGroup, 3, 37, NULL),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller'), @ttGroup, 4, 37, NULL),

((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve'), @ttGroup, 2, 38, NULL),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp'), @ttGroup, 2, 39, NULL);

MERGE techtree AS Target
USING (SELECT parentdefinition, childdefinition, groupID, x, y, enablerextensionid FROM @tempTechtree) AS Source
ON (Target.childdefinition = Source.childdefinition AND Target.groupID = Source.groupID)
WHEN MATCHED THEN
    UPDATE SET
        Target.parentdefinition = Source.parentdefinition,
        Target.x = Source.x,
        Target.y = Source.y,
        Target.enablerextensionid = Source.enablerextensionid
WHEN NOT MATCHED BY TARGET THEN
    INSERT (parentdefinition, childdefinition, groupID, x, y, enablerextensionid)
    VALUES (Source.parentdefinition, Source.childdefinition, Source.groupID, Source.x, Source.y, Source.enablerextensionid);
GO

-- ============================================================================
-- Part 8: Research cost.
--
-- Self-destruct module & hunter remote controller reuse the universal T1-T4 controller-chain scheme,
-- identical across remote_command_translator/industrial/support_remote_controller in the live DB.
-- Hunter Drone RCU ammo (PvE/PvP, independently) reuses def_syndicate_attack_drone_unit's cost.
-- ============================================================================

DECLARE @ttCommon INT = (SELECT TOP 1 id FROM techtreepointtypes WHERE name = 'common');
DECLARE @ttHitech INT = (SELECT TOP 1 id FROM techtreepointtypes WHERE name = 'hitech');

DECLARE @def INT;

-- Self-Destruct Module

SET @def = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_self_destruct_module');
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttCommon)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttCommon, 25000);

SET @def = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module');
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttCommon)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttCommon, 50000);

SET @def = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module');
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttCommon)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttCommon, 75000);

SET @def = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module');
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttCommon)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttCommon, 100000);
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttHitech)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttHitech, 50000);

-- Hunter Remote Controller

SET @def = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_remote_controller');
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttCommon)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttCommon, 25000);

SET @def = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller');
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttCommon)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttCommon, 50000);

SET @def = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller');
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttCommon)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttCommon, 75000);

SET @def = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller');
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttCommon)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttCommon, 100000);
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttHitech)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttHitech, 50000);

-- Hunter Drone RCU ammo

SET @def = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve');
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttCommon)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttCommon, 50000);
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttHitech)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttHitech, 40000);

SET @def = (SELECT definition FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp');
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttCommon)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttCommon, 50000);
IF NOT EXISTS (SELECT 1 FROM techtreenodeprices WHERE definition = @def AND pointtype = @ttHitech)
    INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@def, @ttHitech, 40000);
GO

-- ============================================================================
-- Part 9: Prototype linkage.
-- ============================================================================

DECLARE @tempPrototypes TABLE (definition INT, prototype INT);

INSERT INTO @tempPrototypes (definition, prototype) VALUES
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_self_destruct_module_pr')),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_self_destruct_module_pr')),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_self_destruct_module_pr')),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named1_hunter_remote_controller_pr')),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named2_hunter_remote_controller_pr')),
((SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller'),
 (SELECT definition FROM entitydefaults WHERE definitionname = 'def_named3_hunter_remote_controller_pr'));

MERGE prototypes AS Target
USING (SELECT definition, prototype FROM @tempPrototypes) AS Source
ON (Target.definition = Source.definition)
WHEN MATCHED THEN
    UPDATE SET Target.prototype = Source.prototype
WHEN NOT MATCHED BY TARGET THEN
    INSERT (definition, prototype)
    VALUES (Source.definition, Source.prototype);
GO

-- ============================================================================
-- Part 10: Decalibration / production duration, keyed by category (whole-category rows).
--
-- cf_self_destruct_modules / cf_hunter_remote_controllers: identical to every controller-family category
-- (cf_remote_controllers, cf_industrial_remote_controllers, cf_support_remote_controllers,
-- cf_tactical_remote_controllers, cf_assault_remote_controllers) in the live DB.
-- cf_hunter_drones_units: identical to every other cf_*_drones_units category.
-- ============================================================================

DECLARE @catFlags BIGINT;

SET @catFlags = (SELECT TOP 1 value FROM categoryFlags WHERE name = 'cf_self_destruct_modules');
IF NOT EXISTS (SELECT 1 FROM productiondecalibration WHERE categoryflag = @catFlags)
    INSERT INTO productiondecalibration (categoryflag, distorsionmin, distorsionmax, decrease) VALUES (@catFlags, 0.003, 0.005, 1);
IF NOT EXISTS (SELECT 1 FROM productionduration WHERE category = @catFlags)
    INSERT INTO productionduration (category, durationmodifier) VALUES (@catFlags, 2);

SET @catFlags = (SELECT TOP 1 value FROM categoryFlags WHERE name = 'cf_hunter_remote_controllers');
IF NOT EXISTS (SELECT 1 FROM productiondecalibration WHERE categoryflag = @catFlags)
    INSERT INTO productiondecalibration (categoryflag, distorsionmin, distorsionmax, decrease) VALUES (@catFlags, 0.003, 0.005, 1);
IF NOT EXISTS (SELECT 1 FROM productionduration WHERE category = @catFlags)
    INSERT INTO productionduration (category, durationmodifier) VALUES (@catFlags, 2);

SET @catFlags = (SELECT TOP 1 value FROM categoryFlags WHERE name = 'cf_hunter_drones_units');
IF NOT EXISTS (SELECT 1 FROM productiondecalibration WHERE categoryflag = @catFlags)
    INSERT INTO productiondecalibration (categoryflag, distorsionmin, distorsionmax, decrease) VALUES (@catFlags, 0.001, 0.0015, 0.3);
IF NOT EXISTS (SELECT 1 FROM productionduration WHERE category = @catFlags)
    INSERT INTO productionduration (category, durationmodifier) VALUES (@catFlags, 0.2);
GO
