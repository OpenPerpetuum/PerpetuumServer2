# IMPROVEMENT-043 follow-up: named tiers, research, production for Self-Destruct Module & Hunter Remote Controller — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add named T2-T4 tiers, prototypes, calibration templates, production recipes, research levels,
and tech tree placement for `def_standard_self_destruct_module` and `def_standard_hunter_remote_controller`
(currently standard-tier-only, per `IMPROVEMENT-043-hunter-drones-self-destruct.sql`), plus research/
production for the two existing Hunter Drone RCU ammo items.

**Architecture:** Pure content SQL, one new idempotent migration file:
`docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql`. No C# changes. Every
section resolves all definition/category/aggregatefield ids dynamically via subquery-by-name (never
hardcoded), per `docs/content/claude_game_content_guide.md`.

**Tech Stack:** T-SQL against SQL Server (`perpetuumsa` test DB), following patterns in
`E:\MyStuff\Projects\OPDB\Patches\Live_33\Raw_SQL\20_Command_Translators.sql` and the existing
`docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql`.

## Global Constraints

- No automated test suite exists in this repo (`docs/codebase/TESTING.md`) — "tests" for this plan mean
  running the migration SQL in a `BEGIN TRAN ... ROLLBACK` dry run against the test DB and verifying
  expected rows with `SELECT`, never a `COMMIT`. Per standing project practice (and this session's user
  instruction), **the migration is never applied for real** — only generated and dry-run-verified. The
  user applies it manually.
- Test DB connection: `sqlcmd -S "DESKTOP-8LUE5OF\MSSQLSERVER2019" -d perpetuumsa -E -C -W -s"|" -Q "..."`
  (from `src/Perpetuum.ServerService2/data/perpetuum.ini`'s `ConnectionString`).
- Every INSERT must be idempotent: `IF NOT EXISTS (...) BEGIN INSERT ... END` for `entitydefaults`,
  calibration templates, and `aggregatevalues` (corrected 2026-07-30 after Task 1 review: the real
  `20_Command_Translators.sql` convention uses `IF NOT EXISTS`/`ELSE UPDATE` for `aggregatevalues`, not
  `MERGE` — matches the existing hunter migration's own style too); `MERGE` for `components`,
  `itemresearchlevels`, `techtree`, `prototypes`; plain `IF NOT EXISTS ... INSERT` for
  `techtreenodeprices`, `productiondecalibration`, `productionduration` (matching
  `20_Command_Translators.sql` exactly).
- Never hardcode a `definition`/`categoryflags`/`aggregatefields` id — always resolve via
  `(SELECT ... FROM entitydefaults/categoryFlags/aggregatefields WHERE name = '...')` subqueries.
- Do not commit anything to git during this work (per `CLAUDE.md`: "Do not make commits unless explicitly
  asked") — the plan's steps stop at "verify dry run", no `git commit` steps are included.
- `(tiertype, tierlevel)` convention, confirmed live against `def_standard/named1/2/3_industrial_remote_controller`:
  standard = `(1, 1)`, named1 = `(1, 2)` / named1_pr = `(2, 2)`, named2 = `(1, 3)` / named2_pr = `(2, 3)`,
  named3 = `(1, 4)` / named3_pr = `(2, 4)`. Calibration templates (`_cprg`) are always `(1, N)` regardless
  of whether their tier has a prototype.
- `descriptiontoken` is shared across an entire item family's tiers (confirmed: every tier of
  `def_standard/named1/2/3_remote_command_translator` uses the same `def_remote_command_translator_desc`
  token) — so all self-destruct-module tiers reuse the existing
  `def_standard_self_destruct_module` token, and all hunter-remote-controller tiers reuse
  `def_standard_hunter_remote_controller`. Calibration templates always use the generic
  `calibration_program_desc` token (confirmed shared by every `_cprg` row in the DB).

---

### Task 1: Self-Destruct Module — fitting cost on T1, plus T2-T4 tiers, prototypes, calibration templates

**Files:**
- Create: `docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql`

**Interfaces:**
- Produces: entity definitions `def_named1/2/3_self_destruct_module`,
  `def_named1/2/3_self_destruct_module_pr`, `def_standard/named1/2/3_self_destruct_module_cprg` —
  consumed by Tasks 4 (components), 5 (research levels), 6 (tech tree), 8 (prototype linkage).
  Also adds `cpu_usage`/`core_usage`/`powergrid_usage` aggregatevalues to the existing
  `def_standard_self_destruct_module`.

- [ ] **Step 1: Create the migration file with header and Part 1 (T1 fitting-cost fix)**

```sql
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
```

- [ ] **Step 2: Append T2-T4 self-destruct module entitydefaults + prototypes + calibration templates**

```sql
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
```

- [ ] **Step 3: Dry-run verify against the test DB (rolled back, nothing persisted)**

Run:
```bash
sqlcmd -S "DESKTOP-8LUE5OF\MSSQLSERVER2019" -d perpetuumsa -E -C -W -s"|" -Q "
BEGIN TRAN;
:r \"E:\MyStuff\Projects\PerpetuumServer2\docs\db_structure\migrations\IMPROVEMENT-043-hunter-research-production.sql\"
SELECT definitionname, tiertype, tierlevel, mass FROM entitydefaults WHERE definitionname LIKE 'def_%self_destruct_module%' ORDER BY definitionname;
SELECT ed.definitionname, af.name, av.value FROM aggregatevalues av JOIN entitydefaults ed ON ed.definition=av.definition JOIN aggregatefields af ON af.id=av.field WHERE ed.definitionname LIKE 'def_%self_destruct_module%' AND af.name IN ('cpu_usage','core_usage','powergrid_usage') ORDER BY ed.definitionname;
ROLLBACK;
"
```
Expected: 11 `entitydefaults` rows (T1 unchanged + 3 named tiers + 3 prototypes + 4 cprgs = matches:
T1, named1, named1_pr, named2, named2_pr, named3, named3_pr, and 4 `_cprg` rows), each with correct
`tiertype`/`tierlevel`/`mass`; `cpu_usage`/`core_usage`/`powergrid_usage` present for all 7 non-cprg
definitions including the existing T1 row. No errors. Since this ran inside `sqlcmd -Q` with a `:r`
include, if the shell doesn't support `:r` in `-Q` mode, instead pass the file directly:
`sqlcmd -S "DESKTOP-8LUE5OF\MSSQLSERVER2019" -d perpetuumsa -E -C -i "<path-to-file>" -v` wrapping the
file's own `BEGIN TRAN`/`ROLLBACK` around a temporary copy, or simplest: append the verification
`SELECT`s and a `ROLLBACK` directly to the end of a **scratch copy** of the migration file (never the
real one) for this dry run.

---

### Task 2: Hunter Remote Controller — fitting cost on T1, plus T2-T4 tiers, prototypes, calibration templates

**Files:**
- Modify: `docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql` (append after Task 1's `GO`)

**Interfaces:**
- Produces: `def_named1/2/3_hunter_remote_controller`, `def_named1/2/3_hunter_remote_controller_pr`,
  `def_standard/named1/2/3_hunter_remote_controller_cprg` — consumed by Tasks 4, 5, 6, 8. Also adds
  `cpu_usage`/`core_usage`/`powergrid_usage` to the existing `def_standard_hunter_remote_controller`.

- [ ] **Step 1: Append T1 fitting-cost fix**

```sql
-- ============================================================================
-- Part 2: Hunter Remote Controller -- T1 fitting-cost fix + T2-T4 tiers, prototypes, calibration templates.
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
```

- [ ] **Step 2: Append T2-T4 hunter remote controller entitydefaults + prototypes + calibration templates**

```sql
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
```

- [ ] **Step 3: Dry-run verify (scratch copy + BEGIN TRAN/ROLLBACK, same method as Task 1 Step 3)**

Verify: 11 `entitydefaults` rows for `def_%hunter_remote_controller%` (T1 + 3 named tiers + 3 prototypes +
4 cprgs), each tier's `detection_range`/`remote_control_operational_range`/`remote_control_lifetime`/
`cycle_time`/`cpu_usage`/`core_usage`/`powergrid_usage` aggregatevalues present and matching the table in
the design doc. No errors.

---

### Task 3: Hunter Drone RCU ammo — calibration templates

**Files:**
- Modify: `docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql`

**Interfaces:**
- Produces: `def_standard_hunter_drone_rcu_pve_cprg`, `def_standard_hunter_drone_rcu_pvp_cprg` — consumed
  by Task 5 (research levels).

- [ ] **Step 1: Append calibration templates for both RCU ammo items**

```sql
-- ============================================================================
-- Part 3: Hunter Drone RCU ammo (PvE/PvP) -- calibration templates. These stay single-tier, matching
-- def_mining_industrial_drone_unit / def_syndicate_attack_drone_unit (single-tier ammo behind a tiered
-- controller).
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pve_cprg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_rcu_pve_cprg', 1, 1024,
         (SELECT value FROM categoryflags WHERE name = 'cf_module_calibration_programs'),
         '#tier=$tierlevel_t1', '', 1, 0.01, 0.1, 0, 100, N'calibration_program_desc', 0, 1, 1);
END;

IF NOT EXISTS (SELECT 1 FROM entitydefaults WHERE definitionname = 'def_standard_hunter_drone_rcu_pvp_cprg')
BEGIN
    INSERT INTO entitydefaults
        (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
    VALUES
        ('def_standard_hunter_drone_rcu_pvp_cprg', 1, 1024,
         (SELECT value FROM categoryflags WHERE name = 'cf_module_calibration_programs'),
         '#tier=$tierlevel_t1', '', 1, 0.01, 0.1, 0, 100, N'calibration_program_desc', 0, 1, 1);
END;
GO
```

- [ ] **Step 2: Dry-run verify**

Verify both `_cprg` rows exist with `categoryflags = cf_module_calibration_programs` and
`descriptiontoken = 'calibration_program_desc'`.

---

### Task 4: Production materials (components) for all new items

**Files:**
- Modify: `docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql`

**Interfaces:**
- Consumes: all entitydefaults from Tasks 1-3, plus pre-existing `def_titanium`, `def_axicol`,
  `def_axicoline`, `def_espitium`, `def_hydrobenol`, `def_unimetal`, `def_polynitrocol`,
  `def_polynucleit`, `def_phlobotil`, `def_robotshard_common_basic/advanced/expert` (all verified to
  exist live).
- Produces: `components` rows — consumed by Task 9's final validation only (no downstream task depends
  on this directly).

- [ ] **Step 1: Append the components MERGE block (self-destruct module, hunter remote controller, and both RCU ammo items)**

```sql
-- ============================================================================
-- Part 4: Production/prototyping materials.
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
```

- [ ] **Step 2: Dry-run verify**

```sql
SELECT ed.definitionname, comp.definitionname AS component, c.componentamount
FROM components c
JOIN entitydefaults ed ON ed.definition = c.definition
JOIN entitydefaults comp ON comp.definition = c.componentdefinition
WHERE ed.definitionname LIKE 'def_%self_destruct_module%'
   OR ed.definitionname LIKE 'def_%hunter_remote_controller%'
   OR ed.definitionname LIKE 'def_standard_hunter_drone_rcu_%'
ORDER BY ed.definitionname;
```
Expected: every named tier/prototype references its immediate prior tier as a 1x component; every
prototype additionally carries robotshard components at the documented amounts; both RCU ammo items
carry the 7-material attack-drone-unit recipe. No missing component (every `componentdefinition` resolves
to a real, non-NULL `definition`) — a NULL here means a material name typo, which must be fixed before
proceeding.

---

### Task 5: Research levels (itemresearchlevels)

**Files:**
- Modify: `docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql`

**Interfaces:**
- Consumes: entitydefaults + cprg from Tasks 1-3.
- Produces: `itemresearchlevels` rows.

- [ ] **Step 1: Append the itemresearchlevels MERGE block**

```sql
-- ============================================================================
-- Part 5: Research levels.
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
```

- [ ] **Step 2: Dry-run verify**

```sql
SELECT ed.definitionname, irl.researchlevel, cprg.definitionname AS calib, irl.enabled
FROM itemresearchlevels irl
JOIN entitydefaults ed ON ed.definition = irl.definition
JOIN entitydefaults cprg ON cprg.definition = irl.calibrationprogram
WHERE ed.definitionname LIKE 'def_%self_destruct_module%'
   OR ed.definitionname LIKE 'def_%hunter_remote_controller%'
   OR ed.definitionname LIKE 'def_standard_hunter_drone_rcu_%'
ORDER BY ed.definitionname;
```
Expected: exactly 10 rows, research levels 5/6/7/8 as specified, each `calib` pointing at the matching
`_cprg` definition.

---

### Task 6: Tech tree placement

**Files:**
- Modify: `docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql`

**Interfaces:**
- Consumes: entitydefaults from Tasks 1-3, plus pre-existing `def_standard_cpu_upgrade` and the
  `common2` techtreegroup.
- Produces: `techtree` rows.

- [ ] **Step 1: Append the techtree MERGE block**

```sql
-- ============================================================================
-- Part 6: Tech tree placement.
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
```

- [ ] **Step 2: Dry-run verify — no coordinate overlap, parents resolve**

```sql
SELECT tt.x, tt.y, p.definitionname AS parent, c.definitionname AS child
FROM techtree tt
JOIN entitydefaults c ON c.definition = tt.childdefinition
LEFT JOIN entitydefaults p ON p.definition = tt.parentdefinition
JOIN techtreegroups tg ON tg.id = tt.groupID
WHERE tg.name = 'common2' AND tt.y BETWEEN 30 AND 45
ORDER BY tt.y, tt.x;
```
Expected: 10 new rows at y=36-39 as specified, no duplicate `(x,y)` pairs within the same y, every
`parent` non-NULL (a NULL parent means a definition-name typo in one of the `SELECT definition FROM
entitydefaults WHERE definitionname = ...` subqueries — must be fixed before proceeding, per the content
guide's "Tech Tree: No overlapping coordinates / Parent exists" checklist item).

---

### Task 7: Research cost (techtreenodeprices)

**Files:**
- Modify: `docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql`

**Interfaces:**
- Consumes: entitydefaults from Tasks 1-2, pre-existing `techtreepointtypes` rows `common`/`hitech`.
- Produces: `techtreenodeprices` rows.

- [ ] **Step 1: Append the techtreenodeprices block**

```sql
-- ============================================================================
-- Part 7: Research cost.
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
```

- [ ] **Step 2: Dry-run verify**

```sql
SELECT ed.definitionname, tp.name AS pointtype, ttp.amount
FROM techtreenodeprices ttp
JOIN entitydefaults ed ON ed.definition = ttp.definition
JOIN techtreepointtypes tp ON tp.id = ttp.pointtype
WHERE ed.definitionname IN (
    'def_standard_self_destruct_module','def_named1_self_destruct_module','def_named2_self_destruct_module','def_named3_self_destruct_module',
    'def_standard_hunter_remote_controller','def_named1_hunter_remote_controller','def_named2_hunter_remote_controller','def_named3_hunter_remote_controller',
    'def_standard_hunter_drone_rcu_pve','def_standard_hunter_drone_rcu_pvp')
ORDER BY ed.definitionname;
```
Expected: 14 rows total (4+1 for self-destruct T4's extra hitech row, 4+1 for hunter RC T4, 2+2 for RCU
ammo) matching the amounts above.

---

### Task 8: Prototype linkage + decalibration/production duration

**Files:**
- Modify: `docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql`

**Interfaces:**
- Consumes: entitydefaults from Tasks 1-2, pre-existing `cf_self_destruct_modules`,
  `cf_hunter_remote_controllers`, `cf_hunter_drones_units` categoryflags.
- Produces: `prototypes`, `productiondecalibration`, `productionduration` rows.

- [ ] **Step 1: Append prototypes MERGE + decalibration/duration blocks**

```sql
-- ============================================================================
-- Part 8: Prototype linkage.
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
-- Part 9: Decalibration / production duration, keyed by category (whole-category rows).
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
```

- [ ] **Step 2: Dry-run verify**

```sql
SELECT ed.definitionname, p.prototype
FROM prototypes p JOIN entitydefaults ed ON ed.definition = p.definition
WHERE ed.definitionname LIKE 'def_named%self_destruct_module' OR ed.definitionname LIKE 'def_named%hunter_remote_controller';

SELECT cf.name, pd.distorsionmin, pd.distorsionmax, pd.decrease
FROM productiondecalibration pd JOIN categoryFlags cf ON cf.value = pd.categoryflag
WHERE cf.name IN ('cf_self_destruct_modules','cf_hunter_remote_controllers','cf_hunter_drones_units');

SELECT cf.name, pd.durationmodifier
FROM productionduration pd JOIN categoryFlags cf ON cf.value = pd.category
WHERE cf.name IN ('cf_self_destruct_modules','cf_hunter_remote_controllers','cf_hunter_drones_units');
```
Expected: 6 `prototypes` rows (3 self-destruct, 3 hunter RC); 3 `productiondecalibration` rows and 3
`productionduration` rows with the values above.

---

### Task 9: Full-file dry run + backlog update

**Files:**
- Modify: `docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql` (no further
  content changes — this task only validates and documents)
- Modify: `docs/backlog/improvements.md` (append a note to the existing IMPROVEMENT-043 entry)

**Interfaces:** none (final integration validation).

- [ ] **Step 1: Run the complete migration file inside `BEGIN TRAN ... ROLLBACK` against the test DB**

Copy the finished file into a scratch copy (`<scratchpad>/IMPROVEMENT-043-hunter-research-production-dryrun.sql`)
wrapped as:
```sql
BEGIN TRAN;
-- (paste full migration file content here)
SELECT COUNT(*) AS entitydefaults_added FROM entitydefaults WHERE definitionname IN (
    'def_named1_self_destruct_module','def_named1_self_destruct_module_pr','def_named1_self_destruct_module_cprg',
    'def_named2_self_destruct_module','def_named2_self_destruct_module_pr','def_named2_self_destruct_module_cprg',
    'def_named3_self_destruct_module','def_named3_self_destruct_module_pr','def_named3_self_destruct_module_cprg',
    'def_standard_self_destruct_module_cprg',
    'def_named1_hunter_remote_controller','def_named1_hunter_remote_controller_pr','def_named1_hunter_remote_controller_cprg',
    'def_named2_hunter_remote_controller','def_named2_hunter_remote_controller_pr','def_named2_hunter_remote_controller_cprg',
    'def_named3_hunter_remote_controller','def_named3_hunter_remote_controller_pr','def_named3_hunter_remote_controller_cprg',
    'def_standard_hunter_remote_controller_cprg',
    'def_standard_hunter_drone_rcu_pve_cprg','def_standard_hunter_drone_rcu_pvp_cprg');
SELECT COUNT(*) AS components_rows FROM components c JOIN entitydefaults ed ON ed.definition = c.definition
    WHERE ed.definitionname LIKE 'def_%self_destruct_module%' OR ed.definitionname LIKE 'def_%hunter_remote_controller%' OR ed.definitionname LIKE 'def_standard_hunter_drone_rcu_%';
SELECT COUNT(*) AS research_rows FROM itemresearchlevels irl JOIN entitydefaults ed ON ed.definition = irl.definition
    WHERE ed.definitionname LIKE 'def_%self_destruct_module%' OR ed.definitionname LIKE 'def_%hunter_remote_controller%' OR ed.definitionname LIKE 'def_standard_hunter_drone_rcu_%';
SELECT COUNT(*) AS techtree_rows FROM techtree tt JOIN entitydefaults c ON c.definition = tt.childdefinition
    WHERE c.definitionname LIKE 'def_%self_destruct_module%' OR c.definitionname LIKE 'def_%hunter_remote_controller%' OR c.definitionname LIKE 'def_standard_hunter_drone_rcu_%';
ROLLBACK;
```
Run via: `sqlcmd -S "DESKTOP-8LUE5OF\MSSQLSERVER2019" -d perpetuumsa -E -C -i "<scratch-file-path>" -W -s"|"`

Expected: 0 errors end-to-end (running the whole file top-to-bottom, not just isolated sections —
catches any cross-section dependency ordering mistakes); `entitydefaults_added = 22`;
`components_rows = 98` (self-destruct chain: named1 5 + named1_pr 6 + named2 6 + named2_pr 8 + named3 7 +
named3_pr 10 = 42; hunter RC chain: identical shape = 42; RCU ammo: pve 7 + pvp 7 = 14; total
42+42+14 = 98 — recount against the actual `@tempComponents` literal list in Task 4 if this doesn't
match); `research_rows = 10`; `techtree_rows = 10`. `ROLLBACK` confirms nothing was persisted.

- [ ] **Step 2: Re-run the whole file a second time (still rolled back) to confirm idempotency**

Run the exact same scratch file again. Expected: identical row counts (every `IF NOT EXISTS`/`MERGE`
guard correctly no-ops on rows the same transaction already inserted earlier in the same run — this is
the standard idempotency check used throughout this feature's history, e.g. the "Migration consolidation"
entry in `docs/backlog/improvements.md`).

- [ ] **Step 3: Update the IMPROVEMENT-043 backlog entry**

Append a dated note to `docs/backlog/improvements.md`'s existing `## IMPROVEMENT-043` section (after the
"Migration consolidation" note, before `### Problem`), describing this follow-up:

```markdown
**Research/production follow-up:** Added named T2-T4 tiers, prototypes, and calibration templates for
both `def_standard_self_destruct_module` and `def_standard_hunter_remote_controller` (previously
standard-tier-only), plus calibration templates, research levels, and production materials for the two
existing Hunter Drone RCU ammo items -- all via a new migration,
`docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql` (still unapplied to any
DB, per standing practice). Tech tree branch placed in the `common2` group: self-destruct module chain at
(x=1-4, y=36), directly under `remote_command_translator` (y=35) at the same x positions; hunter remote
controller chain at y=37, parented off the standard self-destruct module node; both Hunter Drone RCU ammo
items as siblings at (x=2, y=38/39) off the standard hunter remote controller node. Design:
`docs/superpowers/specs/2026-07-30-improvement-043-hunter-research-production-design.md`. T1 of both
modules also gained `cpu_usage`/`core_usage`/`powergrid_usage` aggregatevalues they were previously
missing entirely. Verified via a full-file `BEGIN TRAN`/`ROLLBACK` dry run against the live test DB
(0 errors, idempotent on a second run) -- not applied.
```

- [ ] **Step 4: Report completion to the user**

Summarize: migration file location, dry-run verification results, and that manual application to any
real DB is still the user's decision (never applied automatically). List the manual validation steps
from the design doc's "Manual validation steps" section as the next thing for the user to do once they
apply it themselves.
