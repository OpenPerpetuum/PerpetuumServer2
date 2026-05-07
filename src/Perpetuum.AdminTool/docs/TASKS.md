### Phase 1 — Skeleton, connection, login (no game-data UI yet)

- [x] Create Perpetuum.AdminTool WPF project & wire into solution
- [x] Implement settings persistence
- [x] Build connection settings dialog with test button
- [x] Build login dialog with admin access check
- [x] Build main window shell with apply-mode toggle and status bar
- [x] Implement ChangeQueue + SqlScriptBuilder skeleton

### Phase 2 — Translations

- [x] Build TranslationStore + TranslationRow
- [x] Build TranslationsViewModel
- [x] Build TranslationsView with dynamic-column DataGrid
- [x] Build AddKey and AddLanguage modal dialogs
- [x] Wire Translations tab into MainWindow

### Phase 3 — Entity defaults + item stats (the core)

- [x] Build SQL literal formatter
- [x] Build EntityRepository (async DB loader)
- [x] Build entity & stat observable row models
- [x] Build entity diff → IPendingChange emitter
- [x] Build EntitiesViewModel and EntityDetailViewModel
- [x] Build EntitiesView and EntityDetailView
- [x] Wire Entities tab into MainWindow & smoke build

### Phase 4 — Robot templates + loot tables

- [x] Build RobotTemplate row + repository + diff emitter
- [x] Build RobotTemplates UI with raw-Genxy editor
- [x] Build NpcLoot row + repository + diff emitter
- [x] Build NpcLoot UI with single-grid editor
- [x] Wire two new tabs into MainWindow & build

### Phase 4 — Polish (initial pass)

- [x] Add `note` column to RobotTemplateRow / Repository / TemplateChanges / View
- [x] Build RobotTemplateRelationRow + Repository (with entitydefaults & template name lookups)
- [x] Build TemplateRelationChanges (bulk diff INSERT/UPDATE/DELETE keyed by definition)
- [x] Build RobotTemplateRelationsViewModel + AddTemplateRelationRowViewModel
- [x] Build RobotTemplateRelationsView + AddTemplateRelationRowWindow
- [x] Wire Template relations tab into MainWindow & build

### Phase 4 — Polish (current pass)

- [x] **Task 1 — NpcLoot schema fix + diff bug fix.** Use `id` IDENTITY as PK in row/snapshot/repository/diff/UPDATE/DELETE/INSERT. Drop the spurious `(definition, lootdefinition)` uniqueness rejection. Show `Id` column in grid.
- [x] **Task 2+3 — NpcLoot: `definition` and `lootdefinition` editable as dropdowns.** Repo loads picks; VM hooks `PropertyChanged` to refresh display names; add dialog uses ComboBoxes; grid uses `DataGridTemplateColumn` with TextBlock display + ComboBox cell-edit (via `Common/BindingProxy.cs`).
- [x] **Task 4 — RobotTemplateRelations rows fully editable.** `Definition` is an `[ObservableProperty]`; diff keys baselines by `Original.Definition`; UPDATE WHERE uses old definition; SET emits `definition = new` when changed; SaveAll rejects PK collisions.
- [x] **Task 5 — RobotTemplateRelations definition dropdown.** `EntityPickItem` extracted to `Common/EntityPickItem.cs`; repo returns `EntityPicks`; add dialog and grid cell-edit both use ComboBox.

### Phase 5 — NPC groups (complete)

- [x] Build FlockConfiguration row + repository + diff emitter
- [x] Build PresenceConfiguration row + repository + diff emitter
- [x] Build Flocks UI
- [x] Build Presences UI
- [x] Build linking UI between flocks ↔ presences
- [x] Wire NPC-groups tabs into MainWindow

### Polish backlog (in execution order, decided 2026-05-06)

- [x] **Task A — Shared lookup cache.** `Common/LookupCache.cs` holds `Entities` (entitydefaults) + `Templates` (robottemplates) `ObservableCollection`s plus name-lookup dicts. Owned by `AppSession.Lookups`, exposed on every consumer VM as `Lookups`. Initial refresh on `MainViewModel` ctor; refresh on Entities/RobotTemplates/NpcLoot/Relations/Flocks tab reload; refresh after Direct-DB commit. Repositories no longer load picks themselves.
- [x] **Task B — Template-relations `templateid` dropdown.** XAML-only change: `Template id` int column made read-only; `Template name` is now a `DataGridTemplateColumn` (TextBlock display + ComboBox cell-edit, bound to `Lookups.Templates`, writes to `TemplateId`). Diff layer + name-resolution wiring already supported it.
- [x] **Task C — Auto-create translation key on new `entitydefaults.definitionName`.** `ChangeQueue.PendingNewEntityNames` collects names when `EntityDetailViewModel.Save()` queues an INSERT for an `IsNew` row. `MainViewModel.ApplyPendingTranslationKeys()` drains the list at commit success (both modes) — adds each to `TranslationStore` (skipping existing keys silently), pre-fills English (lang 0) with the name itself, then `Save()`s the translation files. Trigger only fires on new-row creation, not on rename.
- [x] **Task D — Decompose `categoryflags` into hierarchy navigation.** New `Entities/{CategoryFlagsNode, CategoryFlagsHierarchy}.cs` build a tree from `Perpetuum.ExportedTypes.CategoryFlags`. Parent rule: clear the highest non-zero byte (e.g. `0x010B01` → `0x0B01` → `0x01`). `EntitiesView.xaml` got a third left-side TreeView panel. Selecting a node sets `EntitiesViewModel.SelectedCategoryNode`; `MatchesFilter` AND-combines the category test (`ContainsOrEquals`) with the existing text filter. Toolbar adds "Show all" (clears category) and "Apply to entity" (sets the selected entity row's `CategoryFlags`).
- [~] **Task E — Bulk multi-row edits in entities grid.** Skipped (2026-05-06) — not needed at this stage.
- [x] **Task F — Genxy `options` field editable** (descoped 2026-05-06: no structured editor — plain text). `EntityDetailView.xaml` options TextBox now two-way + editable. `EntityChanges.BuildEntityUpdate` adds `AddIfChanged("options", o.Options, row.Options)` so edits emit `UPDATE entitydefaults SET options = N'...'`.
- [x] **Task G — Robot template structured editor.** Modal opened from "Structured edit…" button on `RobotTemplatesView`. `Templates/{RobotTemplateEditorEntity, RobotTemplateEditorRepository}.cs` load `entitydefaults` (incl. `categoryflags`, `attributeflags`, `enabled`) and parse `options` for `moduleFlag`, `ammoType`, `ammoCapacity`, `slotFlags`. `ViewModels/{RobotTemplateEditorViewModel, RobotTemplateSlotViewModel}.cs` + `Views/RobotTemplateEditorWindow.xaml(.cs)`.
  - **Part dropdowns.** Five separate `ObservableCollection`s — RobotPicks (cf_robots = 0x1), HeadPicks (cf_robot_head = 0x150), ChassisPicks (cf_robot_chassis = 0x250), LegPicks (cf_robot_leg = 0x350), ContainerPicks (cf_robot_inventory = 0x30915). Each filtered by `Enabled=true` + hierarchical `IsCategory(root)` match.
  - **Slot rows.** Built from chosen part's `options.slotFlags`. Module dropdown filtered by `(module.moduleFlag & slotFlag) == module.moduleFlag` (+ specialized-bit check), `Enabled=true`.
  - **Ammoable detection.** `EntityAttributeFlags(attributeflags).HasFlag(ammo_required)` (bit 18).
  - **Ammo dropdown.** Hierarchical `IsCategory(module.options.ammoType)` + `Enabled=true`. Empty when target is 0; ComboBox stays editable for manual entry.
  - **Cargo `items`.** Round-tripped through a `_passthrough` dict; not editable.

### Post-Task-G refinements (2026-05-06)

- [x] **Bug — Entities tab category-tree TwoWay binding crash.** `<Run Text="{Binding ...}">` defaults to TwoWay against the read-only `CategoryFlagsNode.Display`. Replaced inline `Run`s with a single `TextBlock` using `Mode=OneWay` + `StringFormat`.
- [x] **Editor ammo gating.** Switched "ammoable" detection from `options.ammoCategoryFlags > 0 && options.ammoCapacity > 0` to `attributeflags.HasFlag(ammo_required)`. `RobotTemplateEditorRepository` now also reads `attributeflags`.
- [x] **Editor ammo target field.** Switched ammo-filter target from `options.ammoCategoryFlags` to `options.ammoType` (the actual key in DB options). Filter is hierarchical `IsCategory(target)` mask test against entity's `categoryflags`. Locally re-implements `Perpetuum.CategoryFlagsExtensions.GetCategoryFlagsMask` since the server extension's static ctor binds to the `Db` facade.
- [x] **Hang on ammoable selection.** When `target == 0`, the mask collapses and every entity passes — populating the ObservableCollection with the full ~10k row dump hangs the UI thread. Early-return: empty `AmmoPicks`, controls remain enabled for manual entry.
- [x] **Cache enrichment — CategoryFlags + Enabled.** `EntityPickItem` now carries `CategoryFlags` and `Enabled`. `LookupCache` SQL selects them.
- [x] **Editor entity carries Enabled.** `RobotTemplateEditorEntity.Enabled`. Repository SELECT adds `enabled`. Editor's part / module / ammo filters all skip `!Enabled`.
- [x] **`enabled` editable on entitydefaults.** Dropped `WHERE enabled = 1` from `EntityRepository.LoadDefaultsAsync` so disabled rows show in the Entities tab. `EntityDefaultRow` / `Snapshot` / `ApplySnapshot` / `RefreshOriginalFromCurrent` / `CreateNew` carry `Enabled` (default true). `EntityChanges`: INSERT uses `row.Enabled` (was hardcoded `1`); UPDATE diffs `enabled` via `AddIfChanged`. `EntityDetailView` got an `enabled` checkbox.
- [x] **Per-part category filters.** Each part dropdown bound to its own filtered list (see Task G description above).

### Dropped

- `npcflockloot` editor — confirmed not needed (2026-05-06).
