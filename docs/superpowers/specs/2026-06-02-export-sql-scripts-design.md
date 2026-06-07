# Export: Generate Full SQL Scripts for Seasons, Items, and Robots

**IMPROVEMENT-032**
Generated: 2026-06-02
Status: Approved

---

## 1. Problem

There is no way to extract a game entity as portable, self-contained SQL. Transferring content between server instances, creating backups of handcrafted entities, or sharing content with other operators requires direct DB access and manual query construction.

---

## 2. Solution Summary

Add an **Export** feature to the Admin Tool. An export button in each relevant panel (Seasons, Items, Robots) generates a complete, idempotent SQL script for the selected entity — capturing all dependent data — and presents it in a read-only dialog with Copy and Save As options. The script can be replayed on a clean database to recreate the entity from scratch.

Export is always **read-only**. No data is modified, no script is executed.

---

## 3. Export Scope

### 3.1 Season export

| Table | Pattern |
|---|---|
| `packages` | MERGE on natural key (name or equivalent — confirm against schema) |
| `package_items` | MERGE on `(package_id, definition)` |
| `equipment_sets` | MERGE on `id` |
| `entitydefaults` + full item chain | See §3.2 (one block per unique reward item / set member) |
| `seasons` | MERGE on `name` |
| `season_activity_rates` | DELETE + INSERT by `season_id` |
| `season_objectives` | MERGE on `(season_id, name)` |
| `season_tiers` | MERGE on `(season_id, tier_number)` |
| `season_leaderboard_rewards` | MERGE on `(season_id, rank_min, rank_max)` |

**Traversal:**
1. Load season record.
2. Load activity rates, objectives, tiers, leaderboard rewards.
3. Collect all `package_id` and `equipment_set_id` values from reward rows.
4. Load packages and `package_items`; collect unique `definition` IDs.
5. Load `equipment_sets`; load member definitions; collect unique `definition` IDs.
6. For each unique definition ID: emit a full item block via `ItemExporter` (deduplicated via `HashSet<int>`).
7. Emit season and reward rows after all prerequisites.

### 3.2 Item export (full chain)

Emitted in FK-safe order:

1. `DECLARE @defId` — resolves `entitydefaults.definitionname` → `definition` ID
2. `entitydefaults` MERGE on `definitionname`
3. `aggregatevalues` DELETE + INSERT by `definition` (field names resolved from `aggregatefields`)
4. `components` MERGE on `(definition, componentdefinition)` — component IDs resolved to names
5. `itemresearchlevels` DELETE + INSERT by `definition` — `calibrationprogram` resolved to name
6. `techtree` MERGE on `(parentdefinition, childdefinition)` — parent resolved to name
7. `techtreenodeprices` IF NOT EXISTS INSERT (no natural update key)
8. `prototypes` MERGE on `(definition, prototype)` — prototype resolved to name
9. `enablerextensions` DELETE + INSERT by `definition` — extension ID resolved to name
10. `beamassignment` DELETE + INSERT by `definition` — beam ID resolved to name
11. `definitionconfig` MERGE on `definition`

Tables with no rows for the definition are silently skipped.

**No transitive recipe closure.** Component items referenced in `components` are emitted as name-resolved ID lookups (`SELECT definition FROM entitydefaults WHERE definitionname = 'def_x'`) but are NOT themselves exported. They must already exist on the target DB.

### 3.3 Robot export

| Table | Pattern |
|---|---|
| Part `entitydefaults` + full item chain | One block per unique part definition (deduplicated) |
| `chassisbonus` | DELETE + INSERT by `definition` per part |
| `robottemplates` | MERGE on `name` |
| `robottemplaterelation` | DELETE + INSERT by `robottemplate` |

**Traversal:**
1. Load `robottemplates` row.
2. Load `robottemplaterelation`; collect unique part definition IDs.
3. For each part definition ID: emit full item block via `ItemExporter`.
4. Emit `chassisbonus` DELETE + INSERT per part.
5. Emit `robottemplates` MERGE.
6. Emit `robottemplaterelation` DELETE + INSERT.

---

## 4. Architecture

### 4.1 New files

```
Perpetuum.AdminTool/
└── Export/
    ├── SqlExportBuilder.cs          — shared idempotent SQL template helpers
    ├── ItemExporter.cs              — full item chain export
    ├── SeasonExporter.cs            — season chain export, delegates to ItemExporter
    ├── RobotExporter.cs             — robot template export, delegates to ItemExporter
    └── ExportDialog/
        ├── ExportDialogViewModel.cs
        └── ExportDialog.xaml
```

No new tables. No server-side changes.

### 4.2 Data flow

```
[Export button in panel]
        ↓
[*Exporter.ExportAsync(id, conn)]   — targeted SELECTs; builds List<RawSqlChange>
        ↓
[SqlScriptBuilder.Build(changes)]   — existing pipeline, unmodified
        ↓
[ExportDialogViewModel]             — presents script; Copy / Save As
```

### 4.3 Class responsibilities

| Class | Does | Does not |
|---|---|---|
| `SqlExportBuilder` | Static helpers: `MergeBlock`, `DeleteInsertBlock`, `IfNotExistsInsert`, `DeclareIdVar` | Execute SQL, hold state |
| `ItemExporter` | Queries 10 item tables, builds ordered `RawSqlChange` list | Know about seasons or robots |
| `SeasonExporter` | Queries season tables + packages + sets, delegates per-item to `ItemExporter` | Duplicate-export already-seen definitions |
| `RobotExporter` | Queries robot tables, delegates per-part to `ItemExporter` | Know about seasons |
| `ExportDialogViewModel` | Holds script string, Copy command, Save As command | Generate SQL |

---

## 5. Idempotent SQL Patterns

### 5.1 Named-ID variable declaration

```sql
DECLARE @def_assault_bot INT = (
    SELECT definition FROM entitydefaults WHERE definitionname = 'def_assault_bot'
);
```

Every subsequent statement in the block references `@def_assault_bot` — no hardcoded integers.

### 5.2 MERGE (upsert by natural key)

Used for: `entitydefaults`, `components`, `prototypes`, `techtree`, `definitionconfig`, `season_tiers`, `season_leaderboard_rewards`, `packages`, `package_items`, `equipment_sets`, `seasons`, `season_objectives`.

```sql
MERGE entitydefaults AS target
USING (VALUES ('def_assault_bot', 134217728, ...)) AS src(definitionname, categoryflags, ...)
ON target.definitionname = src.definitionname
WHEN MATCHED THEN UPDATE SET categoryflags = src.categoryflags, ...
WHEN NOT MATCHED THEN INSERT (definitionname, categoryflags, ...)
    VALUES (src.definitionname, src.categoryflags, ...);
```

### 5.3 DELETE + INSERT (full replacement by FK)

Used for: `aggregatevalues`, `enablerextensions`, `beamassignment`, `itemresearchlevels`, `robottemplaterelation`, `chassisbonus`, `season_activity_rates`.

```sql
DELETE FROM aggregatevalues WHERE definition = @def_assault_bot;
INSERT INTO aggregatevalues (definition, field, value) VALUES
    (@def_assault_bot, (SELECT id FROM aggregatefields WHERE name = 'core_recharge'), 120.0),
    ...;
```

### 5.4 IF NOT EXISTS INSERT (identity-keyed, no natural update key)

Used for: `techtreenodeprices`.

```sql
IF NOT EXISTS (
    SELECT 1 FROM techtreenodeprices
    WHERE techtreeid = (SELECT id FROM techtree WHERE childdefinition = @def_assault_bot)
)
BEGIN
    INSERT INTO techtreenodeprices (techtreeid, pointtype, price)
    VALUES (
        (SELECT id FROM techtree WHERE childdefinition = @def_assault_bot),
        1, 5
    );
END
```

---

## 6. UI Surface

### 6.1 Export button placement

| Panel | Location | Enabled when |
|---|---|---|
| `SeasonsViewModel` | Existing toolbar | Season selected |
| `EntitiesViewModel` | Existing toolbar | Entity selected |
| `RobotTemplatesViewModel` | Existing toolbar | Template selected |

### 6.2 Export dialog

Single `ExportDialog` / `ExportDialogViewModel` shared by all entity types. Receives pre-generated script string and entity name; performs no SQL generation.

```
┌─ Export: Summer 2026 ──────────────────────────────────────────────────┐
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ -- Perpetuum.AdminTool export                                    │  │
│  │ -- Entity: Summer 2026 (season id 7)                            │  │
│  │ -- Generated: 2026-06-02 14:30:52 UTC                           │  │
│  │ -- Author: crahn.sect@gmail.com                                  │  │
│  │                                                                   │  │
│  │ SET XACT_ABORT ON;                                               │  │
│  │ BEGIN TRANSACTION;                                               │  │
│  │ ...                                                               │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                          [Copy]  [Save As...]  [Close]  │
└────────────────────────────────────────────────────────────────────────┘
```

- **Script text area** — read-only, monospace, scrollable.
- **Copy** — copies full script text to clipboard.
- **Save As** — `SaveFileDialog`, `*.sql` filter, default filename from `SqlScriptBuilder.BuildFileName(entityType, entityName)` (e.g. `season_Summer_2026_20260602_143052.sql`).
- **Close** — dismisses; no DB changes.
- A brief "Generating…" overlay on the parent panel is shown while export runs; the dialog opens only after the full script is ready. Partial scripts are never shown.

### 6.3 Error handling

If any query fails during export, a user-visible error message is shown on the parent panel. The export dialog is not opened with partial output.

---

## 7. Out of Scope

- Executing the generated script from within the Admin Tool.
- Importing / applying an external SQL file.
- Transitive recipe closure (component items are name-resolved references, not exported).
- Exporting NPC definitions, loot tables, or market configurations.
- Progress reporting for large exports (deferred to a future improvement if needed).
