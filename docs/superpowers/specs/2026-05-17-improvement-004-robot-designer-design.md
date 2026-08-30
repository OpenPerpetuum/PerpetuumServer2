# IMPROVEMENT-004: Admin Tool Robot Designer — Design Spec

**Date:** 2026-05-17
**Branch:** p36.1
**Status:** Approved, ready for implementation

---

## 1. Goal

Add a **New Robot dialog** to the Admin Tool that lets operators create a complete robot definition (entity, parts, template, template relation) from one place, mirroring the existing New Item dialog (IMPROVEMENT-003).

---

## 2. Approach

**Option A — New dedicated dialog, existing panel ViewModels reused.**

- `NewRobotDialog` is a new `Window` (not an extension of `NewItemDialog`).
- The working `NewItemDialog` is untouched.
- `BasicPanelViewModel`, `StatsPanelViewModel`, `ProductionPanelViewModel`, `ResearchPanelViewModel`, `PropertyModifiersPanelViewModel`, and `OptionsVisualPanelViewModel` are reused directly (same classes, no duplication).
- A `NewRobotDialogViewModel` orchestrates all panels and save flow.
- A `RobotSqlBuilder` generates the full transaction SQL.

---

## 3. Entry Point

A **"New Robot..."** button is added to the Entities toolbar (in `EntitiesView.xaml`), alongside the existing "New Item..." button. The button triggers `OpenNewRobotDialogCommand` on `EntitiesViewModel`.

Pre-conditions before opening the dialog (same pattern as New Item):
- Entities and aggregate fields must be loaded; if not, `ReloadAsync()` is called first.
- Translation store must be configured (needed for translation key seeding).

---

## 4. Tab Structure

The dialog has **14 tabs**. Tabs 1–8 mirror `NewItemDialog` exactly. Tabs 9–14 are enabled only when **`IsRobot = true`**.

| # | Tab Header | Enabled When | Content |
|---|---|---|---|
| 1 | Basic | always | Same fields as NewItemDialog Basic tab + `IsRobot` checkbox |
| 2 | Calibration Template | IsCraftable | `BasicPanelViewModel(CalibrationTemplate)` |
| 3 | Prototype | IsCraftable && HasPrototype | `BasicPanelViewModel(Prototype)` |
| 4 | Stats | always | Robot entity `aggregatevalues` — reuses `StatsPanelViewModel` |
| 5 | Property Modifiers | always | Same as NewItemDialog |
| 6 | Production | IsCraftable | Same as NewItemDialog |
| 7 | Research & Tech Tree | IsCraftable | Same as NewItemDialog |
| 8 | Options & Visual | always | Same as NewItemDialog |
| 9 | Head | IsRobot | `BasicPanelViewModel(RobotPart)` + `StatsPanelViewModel` |
| 10 | Chassis | IsRobot | Same pattern |
| 11 | Leg | IsRobot | Same pattern |
| 12 | Inventory | IsRobot | Same pattern |
| 13 | Robot Template | IsRobot | Name (string, required) + Note (optional string) |
| 14 | Template Relation | IsRobot | itemScoreSum, raceId, missionLevel, missionLevelOverride, killEp, note |

### 4.1 `BasicPanelMode` extension

`BasicPanelMode` gets a new value: **`RobotPart`**.

In `RobotPart` mode:
- `DefinitionName` is auto-suggested from the robot's main definition name plus a suffix (`_head`, `_chassis`, `_leg`, `_inventory`) when the main name changes.
- `IsCraftable` and `HasPrototype` flags are not present (robot parts are not independently craftable).
- Validation: must start with `def_`, must not already exist.
- `CategoryFlags` is not required (parts typically inherit from the robot category — the operator can set it or leave 0).

### 4.2 Clone from existing robot (MVP scope)

The dialog header has a **"Clone from existing robot"** picker (ComboBox over enabled entities). In MVP, clone pre-fills the main entity panels only:
- Main robot entity → BasicPanel (definition name, category flags, stats, options)
- aggregatevalues for the robot entity → Stats panel
- calibrationprogram / prototype, production, research, tech tree, options via the same extended clone path as `NewItemDialog`
- robottemplaterelation fields → Template Relation panel (itemScoreSum, raceId, etc.)

**Deferred for a follow-up:** loading per-part stats into Head/Chassis/Leg/Inventory panels (requires parsing the genxy from robottemplates to resolve part definition IDs). In MVP, part panels start blank when cloning.

Clone never overwrites the new definition names — it suggests them (original name with a numeric suffix or blank, letting the operator rename).

---

## 5. New ViewModels and Files

### New files (under `src/Perpetuum.AdminTool/`)

| File | Purpose |
|---|---|
| `NewRobot/RobotTemplatePanelViewModel.cs` | Name + Note for the new robottemplates row |
| `NewRobot/RobotTemplateRelationPanelViewModel.cs` | itemScoreSum, raceId, missionLevel, missionLevelOverride, killEp, note |
| `NewRobot/RobotSqlBuilder.cs` | Builds the full SQL transaction |
| `ViewModels/NewRobotDialogViewModel.cs` | Orchestrates all panels; IsRobot, IsCraftable, HasPrototype gating logic |
| `Views/NewRobotDialog.xaml` + `.xaml.cs` | The dialog Window |

### Modified files

| File | Change |
|---|---|
| `NewItem/BasicPanelMode.cs` | Add `RobotPart` enum value |
| `NewItem/BasicPanelViewModel.cs` | Handle `RobotPart` mode (auto-suggest suffix, relax CategoryFlags requirement) |
| `ViewModels/EntitiesViewModel.cs` | Add `OpenNewRobotDialogCommand` |
| `Views/EntitiesView.xaml` | Add "New Robot..." button to toolbar |

---

## 6. SQL Build Order (`RobotSqlBuilder`)

All SQL is emitted as a single batch. The batch uses SCOPE_IDENTITY() variables to chain the INSERTs.

```sql
BEGIN TRANSACTION;

-- 1. Robot entity
INSERT INTO entitydefaults (...) VALUES (...);
SET @robotDef = SCOPE_IDENTITY();

-- 2. Robot aggregatevalues
INSERT INTO aggregatevalues (definition, field, value) VALUES (@robotDef, ..., ...);
-- (repeated per stat row)

-- 3. Calibration Template (if IsCraftable)
INSERT INTO entitydefaults (...) VALUES (...);
SET @cprgDef = SCOPE_IDENTITY();

-- 4. Prototype (if IsCraftable && HasPrototype)
INSERT INTO entitydefaults (...) VALUES (...);
SET @prDef = SCOPE_IDENTITY();

-- 5. Robot parts (if IsRobot)
INSERT INTO entitydefaults (...) VALUES (...); SET @headDef = SCOPE_IDENTITY();
INSERT INTO entitydefaults (...) VALUES (...); SET @chassisDef = SCOPE_IDENTITY();
INSERT INTO entitydefaults (...) VALUES (...); SET @legDef = SCOPE_IDENTITY();
INSERT INTO entitydefaults (...) VALUES (...); SET @inventoryDef = SCOPE_IDENTITY();

-- 6. Part aggregatevalues (if IsRobot)
INSERT INTO aggregatevalues (definition, field, value) VALUES (@headDef, ..., ...);
-- (repeated per part per stat row)

-- 7. modulepropertymodifiers / aggregatemodifiers
INSERT INTO modulepropertymodifiers (...) VALUES (...);

-- 8. Production chain (if IsCraftable)
INSERT INTO components (...);
INSERT INTO productionduration (...);   -- if no existing row for category
INSERT INTO itemresearchlevels (...);
INSERT INTO techtree (...);
INSERT INTO techtreenodeprices (...);
DELETE FROM enablerextensions WHERE definition = @robotDef;
INSERT INTO enablerextensions (...);
INSERT INTO prototypes (...);           -- if HasPrototype

-- 9. definitionconfig (if configured)
INSERT INTO definitionconfig (definition, ...) VALUES (@robotDef, ...);

-- 10. Robot template (if IsRobot)
INSERT INTO robottemplates (name, description, note)
VALUES (
    @templateName,
    '#robot=i' + FORMAT(@robotDef, 'X')
    + '#head=i' + FORMAT(@headDef, 'X')
    + '#chassis=i' + FORMAT(@chassisDef, 'X')
    + '#leg=i' + FORMAT(@legDef, 'X')
    + '#container=i' + FORMAT(@inventoryDef, 'X'),
    @templateNote
);
SET @templateId = SCOPE_IDENTITY();

-- 11. Template relation (if IsRobot)
INSERT INTO robottemplaterelation
    (definition, templateid, itemscoresum, raceid, missionlevel, missionleveloverride, killep, note)
VALUES
    (@robotDef, @templateId, @itemScoreSum, @raceId, @missionLevel, @missionLevelOverride, @killEp, @note);

COMMIT;
```

The `RawSqlChange` description is: `"Create new robot: {robotDefinitionName}"`.

---

## 7. Validation

`NewRobotDialogViewModel.Validate()` checks in order:
1. `BasicPanel.HasErrors` → "Basic tab has errors"
2. If `IsCraftable`: `CalibrationPanel.HasErrors` → "Calibration Template tab has errors"
3. If `IsCraftable && HasPrototype`: `PrototypePanel.HasErrors` → "Prototype tab has errors"
4. `StatsPanel.HasDuplicateFields()` → "Stats tab: duplicate aggregate field"
5. If `IsRobot`: each part BasicPanel `HasErrors` → "Head/Chassis/Leg/Inventory tab has errors"
6. If `IsRobot`: each part StatsPanelViewModel `HasDuplicateFields()` → "Head/Chassis/Leg/Inventory Stats: duplicate field"
7. If `IsRobot` && `string.IsNullOrWhiteSpace(TemplatePanelViewModel.Name)` → "Robot Template tab: name is required"
8. If `IsCraftable`: `ProductionPanel.HasDuplicateIngredients()`, `ResearchPanel.HasDuplicatePointTypes()`
9. `OptionsVisualPanel.HasDuplicateConfigColumns()`, `OptionsVisualPanel.ValidateTintValues()`

---

## 8. Translation Seeding

After save (same `TranslationStore` pattern as `NewItemDialogViewModel.SeedTranslations()`), seed keys for:
- Robot: `DefinitionName`, `DescriptionToken`
- If `IsCraftable`: CalibrationPanel `DefinitionName`, `DescriptionToken`
- If `IsCraftable && HasPrototype`: PrototypePanel `DefinitionName`, `DescriptionToken`
- If `IsRobot`: Head, Chassis, Leg, Inventory `DefinitionName`, `DescriptionToken`

---

## 9. Save Modes

Same two modes as `NewItemDialog`:
- **SqlScript mode**: writes a `.sql` file to `AppSettings.SqlOutputDirectory`. Filename: `{robotDefinitionName}_{yyyyMMdd_HHmmss}.sql`.
- **Direct apply mode**: executes via `ChangeApplier`, then calls `_lookupCache.RefreshAllAsync`.

---

## 10. Risks and Constraints

- `RobotPart` mode in `BasicPanelViewModel` must not break existing `Main`, `CalibrationTemplate`, or `Prototype` mode paths — add-only change to the enum/switch.
- The genxy `FORMAT(@id, 'X')` produces uppercase hex without leading zeros. Verify this matches the format expected by the server's `GenxyConverter` and existing robot template descriptions before finalising the builder.
- Clone flow for robots requires loading part definitions from the genxy description, which means parsing the existing template's genxy string. This is extra complexity — if the clone flow for robots is scoped out initially, it should be noted as a follow-up.
- `robottemplaterelation.definition` is the PK — the new robot definition must be inserted before the relation row.

---

## 11. Out of Scope

- Chassis bonuses (`chassisbonus` table) — not included in this designer; can be a follow-up.
- Beam assignments (`beamassignment`) — not included; can be a follow-up.
- Paint / visual slots beyond what `definitionconfig` already covers in Options & Visual.
- Clone flow for robots: the "Clone from existing robot" picker header is in scope for the MVP, but the full robot-specific clone path (parsing genxy to resolve part definitions and loading per-part stats) is deferred to a follow-up. In MVP, clone pre-fills only the main entity panels (BasicPanel, Stats, Options & Visual) from the selected entity; part panels start blank.

---

## 12. Manual Validation Steps

1. Open Entities tab; confirm "New Robot..." button appears alongside "New Item...".
2. Open dialog; confirm tabs 1–8 behave identically to `NewItemDialog`.
3. Check `IsRobot`; confirm tabs 9–14 become enabled.
4. Fill all tabs; click Save in SqlScript mode; inspect the generated `.sql` file:
   - Confirm all 4 part entities are inserted.
   - Confirm `robottemplates` row uses `FORMAT(@robotDef,'X')` etc. for the genxy.
   - Confirm `robottemplaterelation` row uses `@robotDef` and `@templateId`.
5. Apply the script to a test DB; confirm all rows are present and the server loads the robot without errors.
6. Repeat in Direct Apply mode; confirm the LookupCache refreshes and the new robot appears in the Entities list.
7. Confirm `NewItemDialog` still works after the `BasicPanelMode.RobotPart` change.
