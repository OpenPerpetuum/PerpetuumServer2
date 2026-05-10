# Project Plan

## Goal

Build Admin Tool for Perpetuum that allows administrators to manage the game effectively by creating, editing, and deleting game entities such as items, robots, their configurations, NPCs and their loot.

## Architecture

**New project**: `src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj` — WPF, .NET 8, x64-Windows, MVVM with CommunityToolkit.Mvvm.
**References (read-only consumers, no edits to existing code)**:
`Perpetuum.ExportedTypes` → `AggregateField` enum, `AccessLevel`, shared types
`Perpetuum` → `GenxyConverter` (templates use Genxy), `EntityDefault` types, `AggregateFieldExtensions`
**What we deliberately do *not* reuse**: the static `Db` facade. Its connection factory is set at server startup. The tool will use its own `SqlConnection` so it is self-contained and cannot interfere with a running server's state.

## Persisted app data (per machine, not in repo)

`%AppData%\\PerpetuumAdminTool\\settings.json`:
- DB connection: server / database / SQL auth user+password (or integrated)
- GameRoot path (so we can find `customDictionary/`)
- SQL-script output directory
- Default mode (Direct DB / SQL Script)

## Apply-mode workflow

Two modes in the main window, switchable any time:
**Direct DB** — every committed edit shows a confirm dialog with the SQL preview before running. Wraps each commit in a transaction.
**SQL Script** — edits accumulate in a `ChangeQueue`. "Export" produces one combined transactional `.sql` file in the configured output dir.

Both modes share one code path: an `IPendingChange` is rendered to SQL by a single `SqlScriptBuilder`. Direct mode just executes it instead of writing to disk.

## Phase breakdown

I'll build it in five phases. We agree on each before starting the next.

### Phase 1 — Skeleton, connection, login (no game-data UI yet)

- Create `Perpetuum.AdminTool` project, add to `PerpetuumServer2.sln`
- Settings load/save (`AppSettings`, `ConnectionSettings`)
- Connection settings dialog (server/db/auth, "Test connection" button)
- Login dialog (email + password) → query `dbo.accounts`, require `accLevel >= gameAdmin`
- Main window shell with mode toggle (Direct DB / SQL Script), status bar (mode + connected-as), empty tab host for later modules
- `ChangeQueue` + `SqlScriptBuilder` skeleton (no real changes yet, but plumbed end-to-end with a stub change so we can verify Direct vs Script)

### Phase 2 — Translations

- `TranslationService` that loads every `<GameRoot>/customDictionary/*.json` at startup, exposes `Get(key, lang)` and `AllKeys`
- Translations editor tab: grid with one row per key, columns per language (English first), edit/add key, add language file
- Save writes back to the per-language JSON files (no DB involvement, no script)
- Translations are then available for Phase 3 to show human-readable names alongside `entitydefaults.definitionName` keys

### Phase 3 — Entity defaults + item stats (the core)

- "Entities" tab: filterable grid over `dbo.entitydefaults` (by category flags, name, definition id), with translated label column
- Detail editor for selected row: name, mass, volume, health, quantity, categoryflags, attributeflags, plus a sub-editor for the Genxy `options` bag (key/value list)
- "Stats" sub-panel: grid of `aggregatevalues` rows for the current definition (field picker from `AggregateField` enum, formula+unit shown read-only from `aggregatefields`, value editable). Add/edit/delete rows
- Each commit becomes one or more `IPendingChange` entries → routed to Direct DB confirm dialog or SQL queue

### Phase 4 — Robot templates + loot tables

- Templates: grid over `dbo.robottemplates`, editor decodes the Genxy description into a structured form (head/chassis/leg + module slots + cargo) using `GenxyConverter`, re-encodes on save
- Loot: grid over `dbo.lootitems` keyed by `containereid`; editor lets you pick a container and add/edit/remove rows
 
### Phase 5 — NPC groups (flocks & presences)

- Browse `FlockConfiguration` rows, edit member count / spawn origin / behavior / boss info
- Link presences (where they spawn) to flocks (what spawns)