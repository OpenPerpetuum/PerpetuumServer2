# IMPROVEMENT-017: Script Filename Prefixes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give each SQL script save path a fixed type prefix in the filename so scripts are immediately identifiable in the output directory without opening them.

**Architecture:** Add a single `BuildFileName(string prefix, string? name)` static method to `SqlScriptBuilder`. It owns normalization (lowercase, sanitize, collapse underscores) and the timestamp format. Three existing call sites each call it with their fixed prefix and an optional entity/season name. No new files, no DB changes, no protocol changes.

**Tech Stack:** C# 12 / .NET 8, `System.Text.RegularExpressions.Regex`, CommunityToolkit.Mvvm (already in use — no new dependencies).

**Spec:** `docs/superpowers/specs/2026-05-18-improvement-017-script-filename-prefixes-design.md`

---

## File Map

| File | Change |
|---|---|
| `src/Perpetuum.AdminTool/Editing/SqlScriptBuilder.cs` | Add `BuildFileName` static method; add `using System.Text.RegularExpressions;` |
| `src/Perpetuum.AdminTool/ViewModels/NewItemDialogViewModel.cs` | Replace filename string at ~line 185 |
| `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs` | Replace filename string at ~line 316 |
| `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs` | Replace filename string at ~line 190 |

---

## Task 1: Add `BuildFileName` to `SqlScriptBuilder`

**Files:**
- Modify: `src/Perpetuum.AdminTool/Editing/SqlScriptBuilder.cs`

This project has no automated test suite. Verification is by build + manual smoke test at the end (Task 2).

- [ ] **Step 1: Open `SqlScriptBuilder.cs` and add the using directive**

Add `using System.Text.RegularExpressions;` as the third using line. Full file after change:

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Perpetuum.AdminTool.Editing
{
    public static class SqlScriptBuilder
    {
        public static string Build(IEnumerable<IPendingChange> changes, string? authorEmail = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("-- Perpetuum.AdminTool generated script");
            sb.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            if (!string.IsNullOrWhiteSpace(authorEmail))
            {
                sb.AppendLine($"-- Author: {authorEmail}");
            }
            sb.AppendLine();
            sb.AppendLine("SET XACT_ABORT ON;");
            sb.AppendLine("BEGIN TRANSACTION;");
            sb.AppendLine();

            int i = 1;
            foreach (var change in changes)
            {
                sb.AppendLine($"-- [{i}] {change.Description}");
                var body = change.ToSql().TrimEnd();
                sb.AppendLine(body);
                if (!body.EndsWith(";", StringComparison.Ordinal))
                {
                    sb.AppendLine(";");
                }
                sb.AppendLine();
                i++;
            }

            sb.AppendLine("COMMIT TRANSACTION;");
            return sb.ToString();
        }

        public static string BuildFileName(string prefix, string? name = null)
        {
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            if (string.IsNullOrWhiteSpace(name))
                return $"{prefix}_{ts}.sql";
            var safe = Regex.Replace(
                Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9_]", "_"),
                @"_+", "_").Trim('_');
            return $"{prefix}_{safe}_{ts}.sql";
        }
    }
}
```

- [ ] **Step 2: Build to verify no compile errors**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/Editing/SqlScriptBuilder.cs
git commit -m "feat(admin-tool): add SqlScriptBuilder.BuildFileName helper"
```

---

## Task 2: Update the three call sites

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/NewItemDialogViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Update `NewItemDialogViewModel.SaveAsync`**

In `NewItemDialogViewModel.cs`, inside `SaveAsync`, find the `SqlScript` branch. Replace the filename line:

```csharp
// Remove this line:
var fileName = $"admintool_{DateTime.Now:yyyyMMdd_HHmmss}.sql";

// Replace with:
var fileName = SqlScriptBuilder.BuildFileName("entity", BasicPanel.DefinitionName);
```

The surrounding context for orientation (do not change these lines):

```csharp
var script = SqlScriptBuilder.Build([change], _session.Email);
Directory.CreateDirectory(dir);
var fileName = SqlScriptBuilder.BuildFileName("entity", BasicPanel.DefinitionName);  // ← changed line
var path = Path.Combine(dir, fileName);
await File.WriteAllTextAsync(path, script);
```

- [ ] **Step 2: Update `NewRobotDialogViewModel.SaveAsync`**

In `NewRobotDialogViewModel.cs`, inside `SaveAsync`, find the `SqlScript` branch. Replace the filename line:

```csharp
// Remove this line:
var fileName = $"{BasicPanel.DefinitionName}_{DateTime.Now:yyyyMMdd_HHmmss}.sql";

// Replace with:
var fileName = SqlScriptBuilder.BuildFileName("robot", BasicPanel.DefinitionName);
```

The surrounding context for orientation (do not change these lines):

```csharp
var script = SqlScriptBuilder.Build([change], _session.Email);
Directory.CreateDirectory(dir);
var fileName = SqlScriptBuilder.BuildFileName("robot", BasicPanel.DefinitionName);  // ← changed line
var path = Path.Combine(dir, fileName);
await File.WriteAllTextAsync(path, script);
```

- [ ] **Step 3: Update `MainViewModel.CommitAsync`**

In `MainViewModel.cs`, inside `CommitAsync`, find the `SqlScript` branch. Replace the filename line:

```csharp
// Remove this line:
var fileName = $"admintool_{DateTime.Now:yyyyMMdd_HHmmss}.sql";

// Replace with:
var fileName = SqlScriptBuilder.BuildFileName("season", Seasons.DetailViewModel?.Season.Name);
```

The surrounding context for orientation (do not change these lines):

```csharp
Directory.CreateDirectory(dir);
var fileName = SqlScriptBuilder.BuildFileName("season", Seasons.DetailViewModel?.Season.Name);  // ← changed line
var path = Path.Combine(dir, fileName);
await File.WriteAllTextAsync(path, script);
```

- [ ] **Step 4: Build to verify no compile errors**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 5: Manual smoke test**

Run the Admin Tool in SqlScript mode and verify each save path:

1. **NewItemDialog** — open New Item, fill in a definition name (e.g. `def_test_item`), save in SqlScript mode. Confirm a file named `entity_def_test_item_<date>.sql` appears in the output directory.

2. **NewRobotDialog** — open New Robot, fill in a definition name (e.g. `def_test_robot`), save in SqlScript mode. Confirm a file named `robot_def_test_robot_<date>.sql` appears.

3. **MainViewModel with season open** — open a season (e.g. `"Summer 2026"`), queue any change, commit in SqlScript mode. Confirm a file named `season_summer_2026_<date>.sql` appears.

4. **MainViewModel with no season open** — do not navigate into any season, queue any change (use the stub change button if needed), commit in SqlScript mode. Confirm a file named `season_<date>.sql` appears.

5. **Season name with special chars** — open a season named `"Season 1 - Test"`, queue and commit. Confirm the filename is `season_season_1_test_<date>.sql` (spaces and dashes collapsed to single underscores).

- [ ] **Step 6: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/NewItemDialogViewModel.cs
git add src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs
git add src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs
git commit -m "feat(admin-tool): prefix SQL script filenames by entity type (IMPROVEMENT-017)"
```

---

## Task 3: Update backlog

**Files:**
- Modify: `docs/backlog/improvements.md`

- [ ] **Step 1: Mark IMPROVEMENT-017 as DONE**

In `docs/backlog/improvements.md`, find the `## IMPROVEMENT-017` section and update its status line:

```markdown
Status: DONE
```

Add a `Spec` line below `Priority` if not already present:

```markdown
Spec: `docs/superpowers/specs/2026-05-18-improvement-017-script-filename-prefixes-design.md`
```

- [ ] **Step 2: Commit**

```
git add docs/backlog/improvements.md
git commit -m "docs(backlog): mark IMPROVEMENT-017 done"
```
