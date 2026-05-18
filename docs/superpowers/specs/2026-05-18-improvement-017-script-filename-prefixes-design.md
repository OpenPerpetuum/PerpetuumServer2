# IMPROVEMENT-017 — SQL Script Filename Prefixes

**Date:** 2026-05-18
**Status:** Approved for implementation

---

## Problem

SQL scripts saved in SqlScript mode carry no information about what kind of content they contain:

| Source | Current filename |
|---|---|
| NewItemDialog | `admintool_20260517_084326.sql` |
| NewRobotDialog | `def_assault_mech_20260517_084326.sql` |
| MainViewModel CommitAsync | `admintool_20260517_084326.sql` |

NewRobotDialog already includes the definition name (from a prior fix), but lacks a type prefix. NewItemDialog and MainViewModel carry no useful identity at all.

---

## Goal

Each save path emits a filename that immediately identifies the type of content it contains, without opening the file.

---

## Filename Format

| Source | Format | Example |
|---|---|---|
| NewItemDialog | `entity_{defName}_{date}.sql` | `entity_def_plasma_launcher_20260517_084326.sql` |
| NewRobotDialog | `robot_{defName}_{date}.sql` | `robot_def_assault_mech_20260517_084326.sql` |
| MainViewModel (season open) | `season_{seasonName}_{date}.sql` | `season_summer_2026_20260517_084326.sql` |
| MainViewModel (no season open) | `season_{date}.sql` | `season_20260517_084326.sql` |

Prefixes are fixed per dialog/path — not derived from item category.

---

## Implementation

### New helper: `SqlScriptBuilder.BuildFileName`

```csharp
public static string BuildFileName(string prefix, string? name = null)
{
    var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    if (string.IsNullOrWhiteSpace(name))
        return $"{prefix}_{ts}.sql";
    var safe = Regex.Replace(
        Regex.Replace(name.ToLowerInvariant(), @"[^\w]", "_"),
        @"_+", "_").Trim('_');
    return $"{prefix}_{safe}_{ts}.sql";
}
```

Normalization applied to `name`:
1. Lowercase
2. Replace any non-word character with `_`
3. Collapse consecutive `_` into one; trim leading/trailing `_`

For definition names (already `def_*` lowercase with underscores), steps 1–3 are a defensive no-op. For season names (free-form strings like `"Season 1 - Spring 2026"`), they produce `season_1_spring_2026`.

### Call site changes

**`NewItemDialogViewModel.SaveAsync`** (`NewItemDialogViewModel.cs` ~line 185):
```csharp
// before
var fileName = $"admintool_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
// after
var fileName = SqlScriptBuilder.BuildFileName("entity", BasicPanel.DefinitionName);
```

**`NewRobotDialogViewModel.SaveAsync`** (`NewRobotDialogViewModel.cs` ~line 316):
```csharp
// before
var fileName = $"{BasicPanel.DefinitionName}_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
// after
var fileName = SqlScriptBuilder.BuildFileName("robot", BasicPanel.DefinitionName);
```

**`MainViewModel.CommitAsync`** (`MainViewModel.cs` ~line 190):
```csharp
// before
var fileName = $"admintool_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
// after
var fileName = SqlScriptBuilder.BuildFileName("season", Seasons.DetailViewModel?.Season.Name);
```

### Season name access path

`MainViewModel.Seasons` → `SeasonsViewModel.DetailViewModel` → `SeasonDetailViewModel.Season` → `SeasonRow.Name`

`DetailViewModel` is `null` when no season detail is open (e.g. after using the New Season wizard without navigating into a season). `BuildFileName` handles `null` gracefully by omitting the name segment.

---

## Files Changed

| File | Change |
|---|---|
| `src/Perpetuum.AdminTool/Editing/SqlScriptBuilder.cs` | Add `BuildFileName` static method |
| `src/Perpetuum.AdminTool/ViewModels/NewItemDialogViewModel.cs` | Use `BuildFileName("entity", ...)` |
| `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs` | Use `BuildFileName("robot", ...)` |
| `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs` | Use `BuildFileName("season", ...)` |

---

## Scope Notes

- The original IMPROVEMENT-017 excluded `MainViewModel.CommitAsync` because it covers multiple changes. This design explicitly brings it into scope with the `season_` prefix, since the change queue is used primarily for season content in practice.
- No changes to the DB, network protocol, or game server.
- No UI changes — filename is shown in the success message/summary, which already reads from the generated `path`.

---

## Validation

1. Save a new item in SqlScript mode → file is named `entity_def_<name>_<date>.sql`.
2. Save a new robot in SqlScript mode → file is named `robot_def_<name>_<date>.sql`.
3. Queue season changes and commit in SqlScript mode with a season open → file is named `season_<normalized-season-name>_<date>.sql`.
4. Commit in SqlScript mode with no season detail open → file is named `season_<date>.sql`.
5. Season name with spaces and dashes (e.g. `"Season 1 - Spring"`) → normalized to `season_1_spring` in filename.
