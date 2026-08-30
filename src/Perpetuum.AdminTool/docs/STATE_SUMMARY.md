# Perpetuum AdminTool — Project State

## Active phase

**Polish backlog complete.** All in-scope items (A, B, C, D, F, G) and the post-G refinements done. Awaiting user direction on next phase.

## Project layout

- `src/Perpetuum.AdminTool/` — WPF .NET 8 x64-Windows project.
- References: `Perpetuum`, `Perpetuum.ExportedTypes`, `CommunityToolkit.Mvvm 8.3.2`, `Microsoft.Data.SqlClient 6.0.1`, `Newtonsoft.Json 13.0.3`.
- Build: `dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Debug -p:Platform=x64`.
- Last verified: 0 errors, 2 SHA1 warnings (intentional — `Data/Authenticator.cs`).

## Completed phases

### Phase 1 — Skeleton, settings, login, change pipeline
- `%AppData%\PerpetuumAdminTool\settings.json`. SHA1 admin auth (`accLevel >= gameAdmin`).
- `Editing/{IPendingChange, RawSqlChange, ChangeQueue, SqlScriptBuilder, ChangeApplier}.cs`.
- Direct DB and SQL-script apply modes.

### Phase 2 — Translations
- `Translations/{LanguageCatalog, TranslationRow, TranslationStore}.cs` over `<GameRoot>/customDictionary/*.json`. UTF-8 no BOM.

### Phase 3 — Entity defaults + item stats
- `Entities/{EntityDefaultRow, EntityDefaultSnapshot, StatRow, AggregateFieldInfo, EntityRepository}.cs` + `Editing/EntityChanges.cs`.
- IDENTITY INSERT pattern: per-row `NewIdToken`, single batch `DECLARE @new_def_<token>`.
- DELETE flagged `IsDestructive=true`. Destructive-commit guard in `MainViewModel.CommitAsync`.

### Phase 4 — Robot templates + NPC loot + relations
- Templates raw-Genxy editor; relations + NPC-loot grids with diff-keyed-by-PK.

### Phase 5 — NPC groups
- `Npc/{FlockRow, PresenceRow, FlockRepository, PresenceRepository}.cs` + `Editing/{FlockChanges, PresenceChanges}.cs`.
- Presence → Flocks linking modal.

### Polish backlog (A → G)

- **A** — Shared `Common/LookupCache.cs` (entitydefaults + robottemplates `ObservableCollection`s + name dicts) on `AppSession.Lookups`. Refresh on app start, per-tab Reload, post-Direct-DB-commit.
- **B** — Template-relations `templateid` editable dropdown (XAML-only; data layer already supported it).
- **C** — Auto-create translation key on new `entitydefaults`. `ChangeQueue.PendingNewEntityNames` collects names at queue time; `MainViewModel.ApplyPendingTranslationKeys()` drains at commit success — adds key to `TranslationStore`, English (lang 0) = name, saves JSON.
- **D** — `categoryflags` hierarchy navigation. `Entities/{CategoryFlagsNode, CategoryFlagsHierarchy}.cs`. TreeView panel on Entities tab; `MatchesFilter` AND-combines text + category.
- **E** — Skipped (2026-05-06).
- **F** — `entitydefaults.options` editable plain text (descoped from structured editor). UPDATE diff covers it.
- **G** — Robot template structured editor modal. `Templates/{RobotTemplateEditorEntity, RobotTemplateEditorRepository}.cs` + `ViewModels/{RobotTemplateEditorViewModel, RobotTemplateSlotViewModel}.cs` + `Views/RobotTemplateEditorWindow.xaml(.cs)`. See *Robot-template editor* below.

### Post-G refinements (2026-05-06)

- TwoWay-binding crash on `Run Text` against read-only `Display` → `TextBlock` + `Mode=OneWay` + `StringFormat`.
- Ammoable detection now uses `attributeflags.ammo_required` (bit 18) via `EntityAttributeFlags.HasFlag`.
- Ammo target switched to `options.ammoType` (verified in real DB rows; the dump key `ammoCategoryFlags` is a runtime-only field).
- Hang-on-empty-ammo-target: early-return when target is 0; manual entry still works.
- `EntityPickItem` enriched with `CategoryFlags` and `Enabled`. Cache SQL pulls them.
- `enabled` is now editable end-to-end on entitydefaults. `EntityRepository` no longer hard-filters `WHERE enabled=1`. `EntityChanges` INSERT/UPDATE handle it. `EntityDetailView` got the checkbox.
- Per-part category filters in the structured editor (see below).

## Robot-template editor (current shape)

**Modal opened via "Structured edit…" button on `RobotTemplatesView`.** Round-trips Genxy in/out of the row's `Description`. Owns top-level keys `robot/head/chassis/leg/container/{head,chassis,leg}Modules`; everything else (e.g. `items`) preserved through `_passthrough` dict.

**Part dropdowns** — five `ObservableCollection<RobotTemplateEditorEntity>`:
- `RobotPicks` → `cf_robots = 0x1`
- `HeadPicks` → `cf_robot_head = 0x150`
- `ChassisPicks` → `cf_robot_chassis = 0x250`
- `LegPicks` → `cf_robot_leg = 0x350`
- `ContainerPicks` → `cf_robot_inventory = 0x30915`

Each filtered by `Enabled = true` + hierarchical `IsCategory(root)` (mask-up-through-highest-non-zero-byte).

**Slot rows** — driven by chosen part's `options.slotFlags` (int[]). Per slot:
- Module dropdown: `Enabled` + `(module.moduleFlag & slotFlag) == module.moduleFlag` + specialized-bit (1 << 11) check, mirroring `Perpetuum.Robots.RobotComponent.IsValidSlotTo`.
- Ammoable detection: `attributeflags.ammo_required` (bit 18) on the chosen module.
- Ammo dropdown: `Enabled` + hierarchical `IsCategory(module.options.ammoType)` over candidate `categoryflags`. Empty when target is 0.

**On Save** — re-serializes via `GenxyConverter.Serialize`, writes to `Row.Description`. User must still click main "Save changes" → main "Commit" to push to DB.

## Schema cheat-sheet (verified from db_structure)

- `entitydefaults` — `definition` (PK IDENTITY), `definitionname` (UNIQUE), `categoryflags`/`attributeflags` (bigint), `mass`/`volume` (float NULL), `health` (float, default 100), `quantity` (int, default 1, ≥1), `hidden`/`purchasable`/`enabled` (bit, default 1 for enabled), `tiertype`/`tierlevel` (nullable int), `options` (varchar(max) NULL, Genxy), `note` (nvarchar(2048) NULL), `descriptiontoken` (nvarchar(100) NULL).
- `robottemplates` — `id` (PK IDENTITY), `name` (varchar(50), UNIQUE), `description` (varchar(max), Genxy template), `note` (nvarchar(2000) NULL).
- `robottemplaterelation` — PK `definition`, `templateid` (FK→robottemplates), scoring + nullable mission/kill EP, `note`. UNIQUE `(definition, templateid)`.
- `npcloot` — `id` (PK IDENTITY), `definition`/`lootdefinition` (FK→entitydefaults), quantities, `probability`, flags. PK is `id` alone.
- `npcflock` — `id` (PK IDENTITY); flock + spawn config; FK to `npcpresence` and `entitydefaults`.
- `npcpresence` — `id` (PK IDENTITY); zone + spawn metadata; many nullable ints.
- `aggregatefields` — `id` (PK matching `AggregateField` enum), read-only.
- `aggregatevalues` — composite PK `(definition, field)`, `value` (double).
- `<GameRoot>/customDictionary/{langId}.json` — flat string→string map; lang 0 = English.

## Patterns & conventions

- **Diff approach.** Each editable row carries an `Original` snapshot; `Save()` compares current vs `Original`, emits only changed-column UPDATEs.
- **IDENTITY INSERT.** Per-row 8-char-hex `NewIdToken`, single combined SQL block with `DECLARE @new_<table>_<token> INT = SCOPE_IDENTITY();` and dependent inserts in same batch.
- **Editable PK + diff.** Key the baseline dict by `row.Original.Pk` (load-time value), not current. UPDATE WHERE uses `Original.Pk`; SET emits the column when current ≠ `Original`. After save, re-key by current PK.
- **DataGrid + cell-edit ComboBox bound to a VM-level collection.** Use `<common:BindingProxy x:Key="VmProxy" Data="{Binding}"/>` resource on the UserControl. Cell templates live outside the visual tree, so `RelativeSource FindAncestor` is unreliable.
- **Dialog buttons** use code-behind `Click` handlers (NOT `[RelayCommand]` with `Window` param).
- **Converters** use static `Instance` field via `{x:Static common:X.Instance}`.
- **`<Run Text="...">` defaults to TwoWay binding.** Bind read-only-source values via `TextBlock` `Mode=OneWay` instead.
- **Lookup-cache refresh** fires post-Direct-DB-commit and from per-tab Reload buttons. New rows default to `enabled = 1`, so they show up in selectors automatically after the next refresh.
- **Server extension caveat.** `Perpetuum.CategoryFlagsExtensions` has a static ctor that binds to the `Db` facade we deliberately avoid. Re-implement bit math locally rather than calling its methods.

## Constraints

- .NET 8, x64 Windows only.
- Server may be running concurrently (test server only — confirmation modals, no live-edit guard).
- Cannot run WPF in this sandbox — every iteration ends with "user please launch and verify".
- `Data/Authenticator.cs` SHA1 hashing is intentional — leave alone.
- `Settings/ConnectionSettings.cs` defaults are user-modified — leave alone.
- No automated tests; verification = build + manual user walk-through.
- Asks explicit confirmation before each polish/phase task.

## Remaining work

None scoped. Possible future passes (not committed):

- Bulk multi-row edits in entities grid (Task E was deferred).
- Genxy options-bag structured editor (Task F was descoped to plain text).
- `npcflockloot` editor.
- Categoryflags tree exposed in selectors beyond the Entities tab.
- "Apply to multi-selection" actions on the categoryflags tree.

## Open questions

None — all prior questions resolved per `DECISIONS.md`.
