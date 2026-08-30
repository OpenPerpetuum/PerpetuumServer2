# IMPROVEMENT-012 — Tiers Tab Queue Save Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Seasons Tiers tab use the same deferred "Queue Save" mechanic as the Objectives tab, so all three data tabs (Activity Rates, Objectives, Tiers) behave consistently.

**Architecture:** Remove the immediate INSERT auto-queue from `AddTier()`. Add a `QueueSaveTierCommand` that checks `row.Id == 0` to choose INSERT vs UPDATE (identical pattern to `QueueSaveObjective`). Add a "Queue Save" button column to the Tiers DataGrid in XAML (identical to the Objectives column).

**Tech Stack:** C# 12 / .NET 8, WPF, CommunityToolkit.Mvvm (`[RelayCommand]`)

---

## File Map

| File | Change |
|---|---|
| `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs` | Fix `AddTier()` (remove auto-queue); add `QueueSaveTier()` command |
| `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml` | Add "Queue Save" `DataGridTemplateColumn` to Tiers DataGrid |

No other files require changes. `SeasonChanges.BuildInsertTier` and `BuildUpdateTier` already exist and are correct.

---

### Task 1: Fix AddTier and add QueueSaveTierCommand in ViewModel

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs:337-365`

**Context:** `AddTier()` currently calls `_queue.Add(SeasonChanges.BuildInsertTier(row))` immediately on line 362 and sets `StatusMessage = "Queued INSERT for tier."` on lines 363-364. This must be removed so the INSERT is deferred until "Queue Save" is clicked — matching `AddObjective()` which ends with `StatusMessage = "Added objective row. Edit fields, then click 'Queue Save' on the row."`.

`QueueSaveTier` must mirror `QueueSaveObjective` exactly: null guard, unsaved-season guard, `row.SeasonId = Season.Id`, then `Id == 0` → `BuildInsertTier`, else `BuildUpdateTier`, then `StatusIsError = false`.

- [ ] **Step 1: Replace the AddTier body**

In `SeasonDetailViewModel.cs`, find the `AddTier()` method (lines 337–365) and replace its body with the version below. The guard blocks at the top are unchanged; only the lines after `Tiers.Add(row)` change.

Replace:
```csharp
[RelayCommand]
private void AddTier()
{
    if (Season.Id <= 0)
    {
        MessageBox.Show("Save the season (General tab) first.", "Season unsaved",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    if (Packages.Count == 0)
    {
        MessageBox.Show("No packages exist. Create a package on the Packages tab first.",
            "No packages", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    var row = new SeasonTierRow
    {
        SeasonId       = Season.Id,
        TierNumber     = Tiers.Count + 1,
        TierName       = $"Tier {Tiers.Count + 1}",
        PointsRequired = (Tiers.Count + 1) * 1000,
        PackageId      = Packages[0].Id,
        IsNew          = true
    };
    row.SelectedPackage = Packages[0];
    Tiers.Add(row);
    _queue.Add(SeasonChanges.BuildInsertTier(row));
    StatusIsError = false;
    StatusMessage = "Queued INSERT for tier.";
}
```

With:
```csharp
[RelayCommand]
private void AddTier()
{
    if (Season.Id <= 0)
    {
        MessageBox.Show("Save the season (General tab) first.", "Season unsaved",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    if (Packages.Count == 0)
    {
        MessageBox.Show("No packages exist. Create a package on the Packages tab first.",
            "No packages", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    var row = new SeasonTierRow
    {
        SeasonId       = Season.Id,
        TierNumber     = Tiers.Count + 1,
        TierName       = $"Tier {Tiers.Count + 1}",
        PointsRequired = (Tiers.Count + 1) * 1000,
        PackageId      = Packages[0].Id,
        IsNew          = true
    };
    row.SelectedPackage = Packages[0];
    Tiers.Add(row);
    StatusIsError = false;
    StatusMessage = "Added tier row. Edit fields, then click 'Queue Save' on the row.";
}
```

- [ ] **Step 2: Add QueueSaveTier command**

Immediately after the closing brace of `RemoveTier()` (line 381) and before `AddLeaderboardReward()` (line 383), insert:

```csharp
[RelayCommand]
private void QueueSaveTier(SeasonTierRow? row)
{
    if (row == null) return;
    if (Season.Id <= 0)
    {
        MessageBox.Show("Save the season (General tab) first.", "Season unsaved",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    row.SeasonId = Season.Id;
    if (row.Id == 0)
    {
        _queue.Add(SeasonChanges.BuildInsertTier(row));
        StatusMessage = $"Queued INSERT for tier '{row.TierName}'.";
    }
    else
    {
        _queue.Add(SeasonChanges.BuildUpdateTier(row));
        StatusMessage = $"Queued UPDATE for tier '{row.TierName}'.";
    }
    StatusIsError = false;
}
```

- [ ] **Step 3: Build to verify ViewModel compiles**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds with 0 errors. The source generator (CommunityToolkit.Mvvm) will produce `QueueSaveTierCommand` automatically from the `[RelayCommand]` attribute.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs
git commit -m "feat(admin-tool): defer tier INSERT to Queue Save; add QueueSaveTierCommand"
```

---

### Task 2: Add Queue Save column to Tiers DataGrid in XAML

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml:278-287`

**Context:** The Tiers DataGrid currently has one action column — "Remove" — at lines 278–287. The Objectives DataGrid has two action columns: "Remove" at lines 222–230, then "Queue Save" at lines 231–239. The Tiers tab needs the same second column appended after the existing Remove column.

The XAML binding pattern uses `VmProxy` (a `BindingProxy` resource defined at line 12) because DataGrid cell templates lose the DataContext chain to the ViewModel. The command name must be `QueueSaveTierCommand` — the exact name generated by CommunityToolkit from `QueueSaveTier`.

- [ ] **Step 1: Add the Queue Save column to the Tiers DataGrid**

In `SeasonDetailView.xaml`, find the closing tag of the Tiers Remove column (the `</DataGridTemplateColumn>` on line 286) and insert the new column after it, before `</DataGrid.Columns>`:

Find this block (lines 278–287):
```xml
                            <DataGridTemplateColumn Header="" Width="80">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="Remove" Padding="6,1" Foreground="DarkRed"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveTierCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
```

Replace with:
```xml
                            <DataGridTemplateColumn Header="" Width="80">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="Remove" Padding="6,1" Foreground="DarkRed"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveTierCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                            <DataGridTemplateColumn Header="" Width="110">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="Queue Save" Padding="6,1"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.QueueSaveTierCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
```

- [ ] **Step 2: Build to verify XAML compiles**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds with 0 errors and 0 warnings related to the new XAML.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml
git commit -m "feat(admin-tool): add Queue Save column to Tiers DataGrid"
```

---

### Task 3: Manual validation

No automated test suite exists. Validate the following scenarios manually by running the Admin Tool and navigating to a season's Tiers tab.

**Setup:** Ensure a season exists in the DB (or create one via General tab + Commit). Ensure at least one package exists.

- [ ] **Scenario A — Add new tier, Queue Save queues INSERT**
  1. Click "+ Add Tier". A new row appears. Nothing appears in the change queue yet.
  2. Edit TierName to "Bronze", PointsRequired to `500`, choose a Reward Package.
  3. Click "Queue Save" on the row.
  4. Expected: `StatusMessage` reads `"Queued INSERT for tier 'Bronze'."`. Change queue shows one entry: `season_tiers: insert tier 1 ('Bronze') in season <id>`.
  5. Click Commit. Expected: SQL script contains `INSERT INTO season_tiers` with the correct values. DB row is created.

- [ ] **Scenario B — Edit existing tier, Queue Save queues UPDATE**
  1. Reload the season (navigate away and back, or restart). A tier with `Id > 0` loads from DB.
  2. Edit TierName to "Silver" in the grid.
  3. Click "Queue Save" on that row.
  4. Expected: `StatusMessage` reads `"Queued UPDATE for tier 'Silver'."`. Change queue shows one entry: `season_tiers: update id <id>`.
  5. Click Commit. Expected: SQL script contains `UPDATE season_tiers SET ... WHERE id = <id>`. DB row is updated.

- [ ] **Scenario C — Remove existing tier still works**
  1. With a DB-backed tier row visible, click "Remove".
  2. Confirm the dialog.
  3. Expected: row disappears from grid, change queue shows `season_tiers: delete id <id>` (marked destructive).

- [ ] **Scenario D — Add Tier without clicking Queue Save produces no INSERT**
  1. Click "+ Add Tier". Row appears.
  2. Do NOT click "Queue Save".
  3. Check the change queue — it must not contain any tier INSERT.
  4. Navigate away. No tier is persisted.

- [ ] **Scenario E — Unsaved season guard**
  1. Open a new season that has not been committed yet (`Id = 0`).
  2. Click "+ Add Tier".
  3. Expected: dialog "Save the season (General tab) first." appears. No row is added.

- [ ] **Scenario F — Mixed session: tiers + objectives + rates all committed together**
  1. Queue Save one tier change, one objective change, one activity rate change.
  2. Click Commit.
  3. Expected: single SQL script contains all three changes wrapped in one transaction.
