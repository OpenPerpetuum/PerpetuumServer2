# Item Designer — Design Spec

**Improvement:** IMPROVEMENT-003  
**Date:** 2026-05-16  
**Branch:** TBD  
**Status:** Approved for implementation

---

## 1. Overview

The Item Designer is a modal WPF dialog launched from the existing Entities tab. It provides a guided, full-coverage workflow for creating new game items from scratch, including all dependent entities (Calibration Template, Prototype) and all related DB tables required for a fully integrated craftable item.

The dialog uses a tabbed layout. A **Clone from existing** picker at the top of the dialog pre-fills all tabs from a chosen source item and displays original values as read-only reference alongside every editable field throughout.

---

## 2. Scope

Covers creation of:
- Main item entity (`entitydefaults`, `aggregatevalues`, `modulepropertymodifiers`, `aggregatemodifiers`)
- Calibration Template sub-entity (`entitydefaults` for `_cprg`)
- Prototype sub-entity (`entitydefaults` for `_pr`, plus `prototypes` table linkage)
- Production chain (`components`, `productionduration`)
- Research & tech tree (`itemresearchlevels`, `techtree`, `techtreenodeprices`, `enablerextensions`)
- Visual/options configuration (`entitydefaults.options`, `definitionconfig`)
- Translation key seeding (existing `TranslationStore`)

Does **not** cover robot creation (see IMPROVEMENT-004).

---

## 3. Architecture

### 3.1 New Files

| File | Purpose |
|---|---|
| `src/Perpetuum.AdminTool/Views/NewItemDialog.xaml` + `.xaml.cs` | Modal WPF Window |
| `src/Perpetuum.AdminTool/ViewModels/NewItemDialogViewModel.cs` | Single VM owning all tab data |

### 3.2 Existing Files Modified

| File | Change |
|---|---|
| `ViewModels/EntitiesViewModel.cs` | Add `OpenNewItemDialogCommand` |
| `Views/EntitiesView.xaml` | Add "New Item" button |
| `Entities/EntityRepository.cs` | Add `CreateAsync(...)` method covering full transaction |

### 3.3 Existing Files Reused Unchanged

- `Entities/EntityDefaultRow.cs`, `StatRow.cs`, `AggregateFieldInfo.cs`
- `Common/LookupCache.cs`
- `Packages/PackageItemPickItem.cs` — `GetTierLabel` reused for ingredient/item pickers
- `Translations/TranslationStore.cs` — `TryAddKey` + `Save` called on wizard save

---

## 4. Tab Structure

| # | Tab | Tables / Target | Active when |
|---|---|---|---|
| 1 | Basic | `entitydefaults` (main item) | Always |
| 2 | Calibration Template | `entitydefaults` (`_cprg`) | Craftable |
| 3 | Prototype | `entitydefaults` (`_pr`) + `prototypes` | Craftable + Has Prototype |
| 4 | Stats | `aggregatevalues` | Always |
| 5 | Property Modifiers | `modulepropertymodifiers`, `aggregatemodifiers` | Always |
| 6 | Production | `components`, `productionduration` | Craftable |
| 7 | Research & Tech Tree | `itemresearchlevels`, `techtree`, `techtreenodeprices`, `enablerextensions` | Craftable |
| 8 | Options & Visual | `entitydefaults.options`, `definitionconfig` | Always |

**Gating rules enforced at save time** (not just UI visibility):
- If `Craftable = false`: data from tabs 2, 3, 6, 7 is discarded regardless of content
- If `Has Prototype = false`: data from tab 3 is discarded
- If `Has definition config = false` (tab 8): no `definitionconfig` row is written

---

## 5. Clone from Existing

A **Clone source** item picker sits in the dialog header, always visible above all tabs. It is populated from `LookupCache.Entities` (enabled items only, translated name + tier tag via `PackageItemPickItem.GetTierLabel`).

Selecting a clone source:
- Pre-fills all editable fields across all tabs
- Shows original clone-source values as read-only greyed labels beside every editable field

---

## 6. Tab Designs

### 6.1 Tab 1 — Basic

Two-column layout: editable field on left, original clone value (read-only) on right.

| Field | Control | Default | Notes |
|---|---|---|---|
| `definitionname` | Text input | — | Required; `def_` prefix enforced with deduplication (no `def_def_`); uniqueness validated against loaded entity list |
| `categoryflags` | Category tree picker | — | Required; shows translated text per entry; DB values compared against `CategoryFlags` enum — missing enum entries flagged: *"Code entry missing — enum update required"* |
| `attributeflags` | Existing flags editor (reused from Entities tab) | 0 | Bitmask checkbox editor |
| `enabled` | Checkbox | true | |
| `purchasable` | Checkbox | true | |
| `hidden` | Checkbox | false | |
| `quantity` | Integer input | 1 | |
| `mass` | Float input | 0 | |
| `volume` | Float input | 0 | |
| `health` | Float input | 100 | |
| `tiertype` | Dropdown (None / T1–T4) | None | Nullable |
| `tierlevel` | Integer input | — | Nullable; only active when `tiertype` is set |
| `descriptiontoken` | Text input | `<name_without_def_prefix>_desc` | `_desc` postfix enforced with deduplication; auto-suggested from `definitionname` on change; editable |
| `note` | Multi-line text | — | Optional |
| **Craftable** | Checkbox | false | Not a DB field; gates tabs 2, 3, 6, 7 at save time |
| **Has Prototype** | Checkbox | false | Not a DB field; gates tab 3 at save time; only relevant when Craftable = true |

---

### 6.2 Tab 2 — Calibration Template *(Craftable only)*

Same field set as Tab 1, with these differences:

| Difference | Detail |
|---|---|
| `definitionname` | Auto-suggested as `{main_definitionname}_cprg`; same prefix deduplication |
| `descriptiontoken` | Auto-suggested accordingly; same postfix deduplication |
| `purchasable` | Defaults to **false** |
| `hidden` | Defaults to **false** |
| `health` | Defaults to **100** |
| No Craftable / Has Prototype flags | These sub-entities are not independently craftable via the wizard |

---

### 6.3 Tab 3 — Prototype *(Craftable + Has Prototype only)*

Same field set as Tab 1, with these differences:

| Difference | Detail |
|---|---|
| `definitionname` | Auto-suggested as `{main_definitionname}_pr`; same prefix deduplication |
| `descriptiontoken` | Auto-suggested accordingly; same postfix deduplication |
| `purchasable` | Defaults to **true** |
| `hidden` | Defaults to **false** |
| `health` | Defaults to **100** |
| No Craftable / Has Prototype flags | |

On save, in addition to the `_pr` entity INSERT, a `prototypes` table row is written automatically linking the main item's new `definition` to the `_pr` entity's new `definition`. No separate UI for the `prototypes` table.

---

### 6.4 Tab 4 — Stats

DataGrid:

| Column | Behaviour |
|---|---|
| Field | Dropdown from `aggregatefields`; shows translated text; DB values compared against `AggregateField` enum — missing entries flagged per row: *"Code entry missing — enum update required"* |
| Original value | Read-only; from clone source `aggregatevalues`; blank if no clone |
| New value | Editable float |

- Add / Remove rows
- Duplicate field inline validation error
- Clone source pre-populates all its `aggregatevalues` rows

---

### 6.5 Tab 5 — Property Modifiers

Two sub-sections (A and B). Both always active.

**Sub-section A — Module Property Modifiers** (`modulepropertymodifiers`)

DataGrid:

| Column | Behaviour |
|---|---|
| Category flags | Read-only; mirrors Tab 1 `categoryflags` value |
| Base field | Dropdown from `aggregatefields`; translated text; DB-vs-enum check with missing entry warning |
| Modifier field | Same as Base field |
| Original (clone) | Read-only; clone source's base→modifier pair for this category |

- Add / Remove rows
- Existing rules for the selected category shown read-only at top: *"Existing rules for this category — not modified by this wizard"*
- Only newly added rows are written on save

**Sub-section B — Aggregate Modifiers** (`aggregatemodifiers`)

Identical layout and behaviour to Sub-section A, targeting `aggregatemodifiers`.

---

### 6.6 Tab 6 — Production *(Craftable only)*

**Sub-section A — Recipe** (`components`)

DataGrid:

| Column | Behaviour |
|---|---|
| Ingredient | Item picker; enabled items only; translated name + tier tag via `PackageItemPickItem.GetTierLabel`; sorted alphabetically; no category root restriction |
| Amount | Integer input; min 1 |
| Original (clone) | Read-only; clone source's amount for the same ingredient |

- Add / Remove rows
- Duplicate ingredient inline validation error

**Sub-section B — Production Duration** (`productionduration`, category-level, unique per category)

- If category already has a row: shown read-only — *"Existing rule for this category — not modified by this wizard"*
- If no row exists: editable float input (default `1.0`); written on save

---

### 6.7 Tab 7 — Research & Tech Tree *(Craftable only)*

**Sub-section A — Research Level** (`itemresearchlevels`, unique per definition)

Single-row form:

| Field | Control | Notes |
|---|---|---|
| `researchlevel` | Integer input | Default: 1 |
| `calibrationprogram` | Item picker | Enabled only; translated name + tier tag; nullable; auto-references Tab 2 `definitionname` when Craftable and a Calibration Template is defined — field becomes read-only in that case |
| `enabled` | Checkbox | Default: true |

**Sub-section B — Tech Tree Placement** (`techtree`)

DataGrid:

| Column | Behaviour |
|---|---|
| Parent definition | Item picker; enabled only; translated name + tier tag |
| Group | Dropdown from `techtreegroups` |
| x / y | Integer inputs; warning label: *"Verify coordinates don't overlap existing nodes"* |
| Enabler extension | Extension name picker from `extensions` table; nullable; resolved by name at save (never hardcoded ID) |
| Original (clone) | Read-only |

**Sub-section C — Research Costs** (`techtreenodeprices`)

DataGrid:

| Column | Behaviour |
|---|---|
| Point type | Dropdown from `techtreepointtypes` |
| Amount | Integer input |
| Original (clone) | Read-only |

Duplicate point type inline validation error.

**Sub-section D — Enabler Extensions** (`enablerextensions`)

DataGrid:

| Column | Behaviour |
|---|---|
| Extension | Dropdown from `extensions`; shown by name; resolved to ID at save |
| Required level | Integer input |
| Original (clone) | Read-only |

On save: full DELETE + INSERT for this definition (matching content guide replacement pattern).

---

### 6.8 Tab 8 — Options & Visual

**Sub-section A — Options** (`entitydefaults.options`)

Multi-line raw text input for the Genxy options string. Original value shown above as read-only when a clone source is selected.

**Sub-section B — Definition Config** (`definitionconfig`, unique per definition)

- **"Has definition config"** checkbox at top; unchecked by default
- When unchecked: no `definitionconfig` row is written even if values were entered
- If clone source has a `definitionconfig` row: checkbox pre-checked; non-null fields pre-populated

When checked, sparse key-value grid:

| Column | Behaviour |
|---|---|
| Field | Dropdown of all `definitionconfig` column names |
| Value | Typed input per column: hex text for `tint` (validated `#RRGGBB`), checkbox for `missionrelated`, integer for int columns, float for float columns |
| Original (clone) | Read-only |

- Add / Remove rows
- Duplicate field inline validation error
- On save: single `INSERT INTO definitionconfig` with `NULL` for omitted columns

---

## 7. Save Flow

1. Validate required fields across all active tabs (block save on error; highlight offending tab)
2. Apply gating rules: discard data from tabs 2, 3, 6, 7 if `Craftable = false`; discard tab 3 data if `Has Prototype = false`; discard `definitionconfig` if unchecked
3. Execute DB transaction in dependency order:
   - `entitydefaults` — main item (capture `SCOPE_IDENTITY` as `@mainDef`)
   - `entitydefaults` — `_cprg` entity (if Craftable) → capture `@cprgDef`
   - `entitydefaults` — `_pr` entity (if Craftable + Has Prototype) → capture `@prDef`
   - `aggregatevalues` — using `@mainDef`
   - `modulepropertymodifiers` — new rows only (keyed by `categoryflags`)
   - `aggregatemodifiers` — new rows only
   - `components` — using `@mainDef`
   - `productionduration` — if no existing row for category
   - `itemresearchlevels` — using `@mainDef`
   - `techtree` — using `@mainDef`
   - `techtreenodeprices` — using `@mainDef`
   - `enablerextensions` — DELETE + INSERT for `@mainDef`
   - `prototypes` — `(@mainDef, @prDef)` if Craftable + Has Prototype
   - `definitionconfig` — if "Has definition config" checked
4. On DB success: call `TranslationStore.TryAddKey()` for each translation key (display name + description for each created entity); call `TranslationStore.Save()`
5. Refresh `LookupCache`; reload `EntitiesViewModel`; select new item in list
6. Show post-save summary listing all seeded translation keys
7. Close dialog

---

## 8. Translation Key Seeding

No dedicated Translations tab in the wizard. On save, the following keys are added to the shared `TranslationStore` (empty values):

| Entity | Keys added |
|---|---|
| Main item | `{definitionname}`, `{descriptiontoken}` |
| Calibration Template (if Craftable) | `{cprg_definitionname}`, `{cprg_descriptiontoken}` |
| Prototype (if Craftable + Has Prototype) | `{pr_definitionname}`, `{pr_descriptiontoken}` |

New keys appear at the top of the existing Translations tab (`Rows.Insert(0, row)`). The operator fills translated strings there and saves via the Translations tab's Save button.

If `TranslationStore.DirectoryExists` is false (GameRoot not configured): key seeding is skipped silently; a warning is shown in the post-save summary.

---

## 9. Validation Rules

| Rule | Scope |
|---|---|
| `definitionname` required and unique | Tab 1, 2, 3 |
| `categoryflags` required (non-zero) | Tab 1 |
| `def_` prefix enforced (no doubling) | Tab 1, 2, 3 |
| `_desc` postfix enforced (no doubling) | Tab 1, 2, 3 |
| `_cprg` / `_pr` suffix enforced on respective tab names | Tab 2, 3 |
| Duplicate stat field | Tab 4 |
| Duplicate ingredient | Tab 6A |
| Duplicate tech tree point type | Tab 7C |
| Tech tree x/y overlap warning (non-blocking) | Tab 7B |
| Prototype definition must exist if specified | Tab 3 (resolved via entity list) |
| `tint` must match `#RRGGBB` | Tab 8B |
| Duplicate `definitionconfig` field | Tab 8B |

Save is blocked on any error except the x/y overlap warning (advisory only).

---

## 10. Constraints & Notes

- The wizard creates **one main item** per invocation. Creating a prototype for a T1 item that is itself a prototype is a separate invocation.
- The `prototypes` table row is written automatically — no separate UI.
- All extension and field ID resolution uses name lookups, never hardcoded IDs (per content guide).
- `enablerextensions` uses full DELETE + INSERT per content guide.
- `productionduration` and property modifier tables are category-level; existing rows are shown read-only and not overwritten.
- The wizard does **not** use the ChangeQueue — it commits directly on Save.
- The `options` field uses the existing Genxy string format; no structured editor.

---

## 11. Manual Validation Steps

After creating an item via the wizard:

1. Verify the new `entitydefaults` row appears in the Entities tab list
2. Verify `aggregatevalues` rows are correct via the Entities tab stats editor
3. If Craftable: verify `itemresearchlevels` entry exists and `calibrationprogram` is linked correctly
4. If Craftable: verify `techtree` placement does not overlap existing nodes
5. If Craftable: verify `components` recipe is complete and ingredients exist
6. If Has Prototype: verify `prototypes` row exists linking the main item to the `_pr` entity
7. Open the Translations tab and confirm new keys appear at the top; fill in English values and save
8. If `definitionconfig` was written: verify row exists and `tint` value is valid

---

## 12. Potential Regressions

- `LookupCache` refresh after wizard save — verify other tabs that bind to entity pickers (Seasons, Packages, etc.) still function correctly after the refresh
- `TranslationStore.Save()` called from wizard — verify it does not interfere with an in-progress Translations tab edit session
- `EntityRepository.CreateAsync` transaction scope — verify it does not conflict with any open reader connections from the Entities tab list
