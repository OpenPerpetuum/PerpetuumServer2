# Seasons Admin Tool — Fixes Round 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 8 smoke-test issues and add 5 targeted improvements to the Seasons Admin Tool and server-side SeasonService.

**Architecture:** Server-side fixes (SeasonService) are isolated from AdminTool changes. AdminTool fixes flow through row models → view models → XAML in that order, enabling incremental builds after each task.

**Tech Stack:** WPF .NET 8, CommunityToolkit.Mvvm, Microsoft.Data.SqlClient, SQL Server.

**Verification command (after every task):**
```
dotnet build E:\MyStuff\Projects\PerpetuumServer2\PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: Build succeeded. 0 Error(s).

---

## Task 1: SeasonService — Fix server startup intro email + forced season end

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

Fixes Issues 7 and 8.

- [ ] Open `src/Perpetuum/Services/Seasons/SeasonService.cs`. Add a `ConcurrentQueue<Character>` field after `_lastNotifiedSeasonId`:

```csharp
private volatile int _lastNotifiedSeasonId;
private readonly System.Collections.Concurrent.ConcurrentQueue<Character> _pendingIntroChars
    = new System.Collections.Concurrent.ConcurrentQueue<Character>();
```

- [ ] Replace the entire `OnCharacterLogin` method:

```csharp
public void OnCharacterLogin(Character character)
{
    var season = _activeSeason;
    if (season == null)
    {
        // Process loop hasn't warmed the cache yet — defer until RefreshCache runs
        _pendingIntroChars.Enqueue(character);
        return;
    }
    if (DateTime.UtcNow > season.EndTime)
        return;
    if (_repository.TryMarkIntroMailSent(character.Id, season.Id))
        SendIntroMail(character, season);
}
```

- [ ] Replace the entire `RefreshCache` method:

```csharp
internal void RefreshCache()
{
    var previous = _activeSeason;
    var season = _repository.GetActiveSeason();

    if (season == null)
    {
        // If admin deactivated before natural end, trigger end processing now
        if (previous != null && DateTime.UtcNow < previous.EndTime)
        {
            ProcessSeasonEnd(previous);
        }
        else
        {
            _activeSeason      = null;
            _activeRates       = ImmutableList<SeasonActivityRate>.Empty;
            _activeObjectives  = ImmutableList<SeasonObjective>.Empty;
            _activeTiers       = ImmutableList<SeasonTier>.Empty;
            _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;
        }
        // No active season — discard any pending login chars (they'll get email when season activates)
        while (_pendingIntroChars.TryDequeue(out _)) { }
        return;
    }

    _activeRates       = _repository.GetActivityRates(season.Id).ToImmutableList();
    _activeObjectives  = _repository.GetObjectives(season.Id).ToImmutableList();
    _activeTiers       = _repository.GetTiers(season.Id).ToImmutableList();
    _activeLeaderboard = _repository.GetLeaderboardRewards(season.Id).ToImmutableList();
    _activeSeason      = season; // assign last so readers see consistent snapshot

    if (_lastNotifiedSeasonId != season.Id)
    {
        _lastNotifiedSeasonId = season.Id;
        NotifyOnlinePlayersSeasonStarted(season);
    }

    // Send intro mail to characters that connected while cache was null
    while (_pendingIntroChars.TryDequeue(out var character))
    {
        if (DateTime.UtcNow <= season.EndTime &&
            _repository.TryMarkIntroMailSent(character.Id, season.Id))
            SendIntroMail(character, season);
    }
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 2: PackageChanges — Fix SQL variable duplication

**Files:**
- Modify: `src/Perpetuum.AdminTool/Packages/PackageChanges.cs`

Fixes Issue 1. When multiple new packages are queued before a single commit, `SqlScriptBuilder` concatenates their SQL into one batch. If both use `@pkgId`, SQL Server errors on the duplicate DECLARE.

- [ ] In `PackageChanges.cs`, find `BuildInsertPackageWithItems` and replace the variable name with a GUID-unique name:

```csharp
public static IPendingChange BuildInsertPackageWithItems(string name, System.Collections.Generic.IReadOnlyList<PackageItemRow> items)
{
    var varName = "@pkgId_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"DECLARE {varName} INT;");
    sb.AppendLine($"INSERT INTO packages (name) VALUES ({SqlLiteral.Of(name)});");
    sb.AppendLine($"SET {varName} = SCOPE_IDENTITY();");
    foreach (var it in items)
        sb.AppendLine($"INSERT INTO packageitems (packageid, definition, quantity) VALUES ({varName}, {it.Definition}, {it.Quantity});");

    var desc = items.Count > 0
        ? $"packages: insert '{name}' with {items.Count} item(s)"
        : $"packages: insert '{name}'";
    return new RawSqlChange(desc, sb.ToString());
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 3: SeasonActivityRateRow — CanQueueSave property

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonActivityRateRow.cs`

Fixes Issue 5. Prevents admins from queuing an upsert for a rate that has never been saved (Id=0) and has PointsPerUnit=0.

- [ ] In `SeasonActivityRateRow.cs`, add a `CanQueueSave` property and notify it when `PointsPerUnit` changes. Replace the existing `partial void OnPointsPerUnitChanged` line with:

```csharp
public bool CanQueueSave => Id > 0 || PointsPerUnit > 0;

partial void OnPointsPerUnitChanged(double value)
{
    OnPropertyChanged(nameof(EffectiveRate));
    OnPropertyChanged(nameof(CanQueueSave));
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 4: SeasonTierRow + SeasonLeaderboardRewardRow — SelectedPackage property

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonTierRow.cs`
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonLeaderboardRewardRow.cs`

Fixes Issues 3 & 4. When the user selects a package from the ComboBox and then clicks elsewhere, the CellTemplate currently shows the numeric PackageId. Adding `SelectedPackage PackageRow?` lets the CellTemplate show the name.

- [ ] Replace the entire contents of `src/Perpetuum.AdminTool/Seasons/SeasonTierRow.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Packages;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonTierRow : ObservableObject
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private int _tierNumber;
        [ObservableProperty] private string _tierName = "";
        [ObservableProperty] private int _pointsRequired;
        [ObservableProperty] private int _packageId;
        [ObservableProperty] private PackageRow? _selectedPackage;

        partial void OnSelectedPackageChanged(PackageRow? value)
        {
            if (value != null) PackageId = value.Id;
        }
    }
}
```

- [ ] Replace the entire contents of `src/Perpetuum.AdminTool/Seasons/SeasonLeaderboardRewardRow.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Packages;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonLeaderboardRewardRow : ObservableObject
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private int _rankMin = 1;
        [ObservableProperty] private int _rankMax = 1;
        [ObservableProperty] private int _packageId;
        [ObservableProperty] private PackageRow? _selectedPackage;

        partial void OnSelectedPackageChanged(PackageRow? value)
        {
            if (value != null) PackageId = value.Id;
        }
    }
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 5: LookupCache + EntityPickItem + PackageItemPickItem — Tier labels

**Files:**
- Modify: `src/Perpetuum.AdminTool/Common/EntityPickItem.cs`
- Modify: `src/Perpetuum.AdminTool/Common/LookupCache.cs`
- Modify: `src/Perpetuum.AdminTool/Packages/PackageItemPickItem.cs`

Implements Improvement 4. Reads `tiertype` and `tierlevel` from `entitydefaults` and appends a tier label (e.g. `T4P`, `Mk2`) to package item display names.

- [ ] Replace the entire contents of `src/Perpetuum.AdminTool/Common/EntityPickItem.cs`:

```csharp
namespace Perpetuum.AdminTool.Common
{
    public class EntityPickItem
    {
        public int Definition { get; init; }
        public string Name { get; init; } = "";
        public long CategoryFlags { get; init; }
        public bool Enabled { get; init; }
        public bool Hidden { get; init; }
        public int TierType { get; init; }
        public int TierLevel { get; init; }

        public string Display => $"{Definition} — {Name}";
    }
}
```

- [ ] In `src/Perpetuum.AdminTool/Common/LookupCache.cs`, update the SQL query in `RefreshEntitiesAsync` and add two more column reads. Replace the `cmd.CommandText` line and the reader block:

```csharp
cmd.CommandText = "select definition, definitionname, categoryflags, enabled, hidden, " +
                  "ISNULL(tiertype,0), ISNULL(tierlevel,0) " +
                  "from entitydefaults order by definitionname";
await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    var def          = reader.GetInt32(0);
    var name         = reader.IsDBNull(1) ? "" : reader.GetString(1);
    var categoryFlags = reader.IsDBNull(2) ? 0L : reader.GetInt64(2);
    var enabled      = !reader.IsDBNull(3) && reader.GetBoolean(3);
    var hidden       = !reader.IsDBNull(4) && reader.GetBoolean(4);
    var tierType     = reader.GetInt32(5);
    var tierLevel    = reader.GetInt32(6);
    fresh.Add(new EntityPickItem
    {
        Definition    = def,
        Name          = name,
        CategoryFlags = categoryFlags,
        Enabled       = enabled,
        Hidden        = hidden,
        TierType      = tierType,
        TierLevel     = tierLevel,
    });
    names[def] = name;
}
```

- [ ] In `src/Perpetuum.AdminTool/Packages/PackageItemPickItem.cs`, add a `using Perpetuum.ExportedTypes;` if not already present, add a `GetTierLabel` static helper, and update `BuildFilteredList` to embed the label in `displayName`. Replace the entire file:

```csharp
using System.Collections.Generic;
using System.Linq;
using Perpetuum.AdminTool.Common;
using Perpetuum.ExportedTypes;

namespace Perpetuum.AdminTool.Packages
{
    public record PackageItemPickItem(int Definition, string DisplayName)
    {
        public string Display => $"{Definition} — {DisplayName}";

        private static readonly long[] AllowedRoots =
        {
            (long)CategoryFlags.cf_robots,
            (long)CategoryFlags.cf_ammo,
            (long)CategoryFlags.cf_robot_equipment,
            (long)CategoryFlags.cf_material,
            (long)CategoryFlags.cf_production_items,
            (long)CategoryFlags.cf_gift_packages,
            (long)CategoryFlags.cf_consumable_items,
            (long)CategoryFlags.cf_consumable_boosters,
            (long)CategoryFlags.cf_field_accessories,
            (long)CategoryFlags.cf_pbs_capsules,
            (long)CategoryFlags.cf_redeemables,
        };

        public static List<PackageItemPickItem> BuildFilteredList(
            IEnumerable<EntityPickItem> all,
            Dictionary<string, string>? englishNames = null)
        {
            var result = new List<PackageItemPickItem>();
            foreach (var e in all)
            {
                if (!e.Enabled) continue;
                if (e.Hidden) continue;
                if (e.CategoryFlags == 0) continue;
                if (!MatchesAnyRoot(e.CategoryFlags)) continue;
                var baseName = (englishNames != null && englishNames.TryGetValue(e.Name, out var eng) && !string.IsNullOrEmpty(eng))
                    ? eng
                    : e.Name;
                var tierLabel = GetTierLabel(e.CategoryFlags, e.TierType, e.TierLevel);
                var displayName = tierLabel.Length > 0 ? $"{baseName} ({tierLabel})" : baseName;
                result.Add(new PackageItemPickItem(e.Definition, displayName));
            }
            return result.OrderBy(p => p.DisplayName, System.StringComparer.OrdinalIgnoreCase).ToList();
        }

        internal static string GetTierLabel(long categoryFlags, int tierType, int tierLevel)
        {
            var tt = (TierType)tierType;
            bool isRobot = (categoryFlags & CategoryFlagsMask((long)CategoryFlags.cf_robots)) == (long)CategoryFlags.cf_robots;

            if (isRobot)
            {
                return tt switch
                {
                    TierType.Prototype => "P",
                    TierType.Normal when tierLevel >= 2 => $"Mk{tierLevel}",
                    _ => ""
                };
            }
            return (tt, tierLevel) switch
            {
                (TierType.Undefined, _) => "",
                (_, 0) => "",
                (TierType.Normal, 1) => "",
                (TierType.Normal, int l) => $"T{l}",
                (TierType.Prototype, int l) => $"T{l}P",
                (TierType.Special, int l) => $"T{l}+",
                _ => ""
            };
        }

        private static bool MatchesAnyRoot(long entityFlags)
        {
            foreach (var root in AllowedRoots)
            {
                var mask = CategoryFlagsMask(root);
                if ((entityFlags & mask) == root) return true;
            }
            return false;
        }

        private static long CategoryFlagsMask(long target)
        {
            var mask = unchecked((long)0xFFFFFFFFFFFFFFFFUL);
            while (((ulong)target & (ulong)mask) > 0)
                mask <<= 8;
            return ~mask;
        }
    }
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 6: SeasonWizardViewModel — Fix 2, Fix 3/4, Improvement 1, Improvements 8/9

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs`

- [ ] Add the following using at the top (if not already present):

```csharp
using System.Linq;
using System.Text;
using Perpetuum.AdminTool.Editing;
```

- [ ] Add these computed properties after `HasPackages`:

```csharp
// Fix 2: only show enabled activity types as objective options
public IReadOnlyList<ActivityTypeOption> ActiveObjectiveActivityTypeOptions =>
    ActivityRates
        .Where(r => r.PointsPerUnit > 0)
        .Select(r => new ActivityTypeOption(r.ActivityType, r.ActivityTypeLabel))
        .ToList();

// Improvements 8 & 9
public int TotalObjectiveBonusPoints => Objectives.Sum(o => o.BonusPoints);
public int MaxTierPoints => Tiers.Count > 0 ? Tiers.Max(t => t.PointsRequired) : 0;
public bool HasObjectives => Objectives.Count > 0;
public bool HasTiers => Tiers.Count > 0;

// Improvement 2: review section computed lines
public IReadOnlyList<string> ReviewActiveRates =>
    ActivityRates.Where(r => r.PointsPerUnit > 0)
                 .Select(r => $"  • {r.ActivityTypeLabel}: {r.EffectiveRate}")
                 .ToList();
public bool HasActiveRates => ReviewActiveRates.Count > 0;
public string ReviewObjectivesHeader => Objectives.Count == 0 ? "Objectives: none"
    : $"Objectives ({Objectives.Count}, {TotalObjectiveBonusPoints} bonus pts total):";
public IReadOnlyList<string> ReviewObjectiveLines =>
    Objectives.Select(o =>
    {
        var typeName = ObjectiveActivityTypeOptions.FirstOrDefault(x => x.Value == o.ActivityType)?.Label ?? o.ActivityType.ToString();
        return $"  • {o.Name} — {typeName}: {o.TargetValue:N0} → +{o.BonusPoints} pts";
    }).ToList();
public string ReviewTiersHeader => Tiers.Count == 0 ? "Tiers: none" : $"Tiers ({Tiers.Count}):";
public IReadOnlyList<string> ReviewTierLines =>
    Tiers.Select(t => $"  • {t.TierName}: {t.PointsRequired:N0} pts → {t.SelectedPackage?.Name ?? $"pkg {t.PackageId}"}").ToList();
public string ReviewLeaderboardHeader => LeaderboardRewards.Count == 0 ? "Leaderboard Rewards: none"
    : $"Leaderboard Rewards ({LeaderboardRewards.Count}):";
public IReadOnlyList<string> ReviewLeaderboardLines =>
    LeaderboardRewards.Select(l => $"  • Rank {l.RankMin}–{l.RankMax}: {l.SelectedPackage?.Name ?? $"pkg {l.PackageId}"}").ToList();
```

- [ ] Subscribe to collection changes in the constructor, after the existing `foreach (SeasonActivityType type ...)` block:

```csharp
Objectives.CollectionChanged += (_, _) =>
{
    OnPropertyChanged(nameof(TotalObjectiveBonusPoints));
    OnPropertyChanged(nameof(HasObjectives));
    OnPropertyChanged(nameof(ReviewObjectivesHeader));
    OnPropertyChanged(nameof(ReviewObjectiveLines));
};
Tiers.CollectionChanged += (_, _) =>
{
    OnPropertyChanged(nameof(MaxTierPoints));
    OnPropertyChanged(nameof(HasTiers));
    OnPropertyChanged(nameof(ReviewTiersHeader));
    OnPropertyChanged(nameof(ReviewTierLines));
};
LeaderboardRewards.CollectionChanged += (_, _) =>
{
    OnPropertyChanged(nameof(ReviewLeaderboardHeader));
    OnPropertyChanged(nameof(ReviewLeaderboardLines));
};
```

- [ ] Update `OnCurrentStepChanged` to notify `ActiveObjectiveActivityTypeOptions` when entering Step 3:

```csharp
partial void OnCurrentStepChanged(int value)
{
    OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2));
    OnPropertyChanged(nameof(IsStep3)); OnPropertyChanged(nameof(IsStep4));
    OnPropertyChanged(nameof(IsStep5)); OnPropertyChanged(nameof(IsReviewStep));
    OnPropertyChanged(nameof(CanGoBack)); OnPropertyChanged(nameof(CanGoNext));
    OnPropertyChanged(nameof(StepTitle)); OnPropertyChanged(nameof(FinishHint));
    if (value == 3)
        OnPropertyChanged(nameof(ActiveObjectiveActivityTypeOptions));
    if (value == 6)
    {
        OnPropertyChanged(nameof(ReviewActiveRates));
        OnPropertyChanged(nameof(ReviewObjectivesHeader));
        OnPropertyChanged(nameof(ReviewObjectiveLines));
        OnPropertyChanged(nameof(ReviewTiersHeader));
        OnPropertyChanged(nameof(ReviewTierLines));
        OnPropertyChanged(nameof(ReviewLeaderboardHeader));
        OnPropertyChanged(nameof(ReviewLeaderboardLines));
    }
}
```

- [ ] Replace `AddTierRow` to set `SelectedPackage` (Fix 3/4):

```csharp
[RelayCommand]
private void AddTierRow()
{
    var pkg = _packages.Count > 0 ? _packages[0] : null;
    var row = new SeasonTierRow
    {
        SeasonId        = 0,
        TierNumber      = Tiers.Count + 1,
        TierName        = $"Tier {Tiers.Count + 1}",
        PointsRequired  = (Tiers.Count + 1) * 1000,
        PackageId       = pkg?.Id ?? 0,
        IsNew           = true
    };
    row.SelectedPackage = pkg;
    Tiers.Add(row);
}
```

- [ ] Replace `AddLeaderboardRow` to set `SelectedPackage` (Fix 3/4):

```csharp
[RelayCommand]
private void AddLeaderboardRow()
{
    var pkg = _packages.Count > 0 ? _packages[0] : null;
    var row = new SeasonLeaderboardRewardRow
    {
        SeasonId  = 0,
        RankMin   = 1,
        RankMax   = 1,
        PackageId = pkg?.Id ?? 0,
        IsNew     = true
    };
    row.SelectedPackage = pkg;
    LeaderboardRewards.Add(row);
}
```

- [ ] Update `FinishHint` property and replace the `Finish` command (Improvement 1):

Replace the `FinishHint` property:
```csharp
public string FinishHint => "Queues the season (Draft) with all configured rates, objectives, tiers, and rewards in one SQL batch. Activate via season detail after committing.";
```

Replace the `Finish` relay command:
```csharp
[RelayCommand]
private void Finish()
{
    ValidateStep1();
    if (!string.IsNullOrEmpty(Step1Validation)) return;
    _queue.Add(BuildSeasonScript());
    _onComplete();
}
```

- [ ] Add the `BuildSeasonScript` private method after `Finish`:

```csharp
private IPendingChange BuildSeasonScript()
{
    var sb = new StringBuilder();
    sb.AppendLine("DECLARE @seasonId INT;");
    sb.AppendLine($"INSERT INTO seasons (name, description, start_time, end_time, is_active)");
    sb.AppendLine($"VALUES ({SqlLiteral.Of(Name)}, {SqlLiteral.Of(Description)},");
    sb.AppendLine($"  '{StartTime:yyyy-MM-dd HH:mm:ss}', '{EndTime:yyyy-MM-dd HH:mm:ss}', 0);");
    sb.AppendLine("SET @seasonId = SCOPE_IDENTITY();");

    foreach (var rate in ActivityRates.Where(r => r.PointsPerUnit > 0))
    {
        sb.AppendLine($"INSERT INTO season_activity_rates (season_id, activity_type, points_per_unit, unit_scale)");
        sb.AppendLine($"VALUES (@seasonId, {(int)rate.ActivityType}, {SqlLiteral.Of(rate.PointsPerUnit)}, {rate.UnitScale});");
    }

    int dispOrder = 0;
    foreach (var obj in Objectives)
    {
        sb.AppendLine($"INSERT INTO season_objectives (season_id, name, description, activity_type, target_value, bonus_points, display_order)");
        sb.AppendLine($"VALUES (@seasonId, {SqlLiteral.Of(obj.Name)}, {SqlLiteral.Of(obj.Description)}, {(int)obj.ActivityType}, {obj.TargetValue}, {obj.BonusPoints}, {dispOrder++});");
    }

    foreach (var tier in Tiers)
    {
        sb.AppendLine($"INSERT INTO season_tiers (season_id, tier_number, tier_name, points_required, package_id)");
        sb.AppendLine($"VALUES (@seasonId, {tier.TierNumber}, {SqlLiteral.Of(tier.TierName)}, {tier.PointsRequired}, {tier.PackageId});");
    }

    foreach (var lb in LeaderboardRewards)
    {
        sb.AppendLine($"INSERT INTO season_leaderboard_rewards (season_id, rank_min, rank_max, package_id)");
        sb.AppendLine($"VALUES (@seasonId, {lb.RankMin}, {lb.RankMax}, {lb.PackageId});");
    }

    var parts = new List<string> { "season" };
    var activeRates = ActivityRates.Count(r => r.PointsPerUnit > 0);
    if (activeRates > 0) parts.Add($"{activeRates} rates");
    if (Objectives.Count > 0) parts.Add($"{Objectives.Count} objectives");
    if (Tiers.Count > 0) parts.Add($"{Tiers.Count} tiers");
    if (LeaderboardRewards.Count > 0) parts.Add($"{LeaderboardRewards.Count} leaderboard entries");

    return new RawSqlChange($"seasons: insert '{Name}' with {string.Join(", ", parts)}", sb.ToString());
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 7: SeasonDetailViewModel — Fix 3/4 (SelectedPackage wiring) + Fix 6 (QueueSaveObjective)

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs`

- [ ] In `LoadAsync`, after `Tiers.Clear()`, replace the tier loading loop:

```csharp
Tiers.Clear();
if (Season.Id > 0)
    foreach (var t in await _repo.LoadTiersAsync(Season.Id))
    {
        t.SelectedPackage = Packages.FirstOrDefault(p => p.Id == t.PackageId);
        Tiers.Add(t);
    }
```

- [ ] In `LoadAsync`, after `LeaderboardRewards.Clear()`, replace the leaderboard loading loop:

```csharp
LeaderboardRewards.Clear();
if (Season.Id > 0)
    foreach (var l in await _repo.LoadLeaderboardRewardsAsync(Season.Id))
    {
        l.SelectedPackage = Packages.FirstOrDefault(p => p.Id == l.PackageId);
        LeaderboardRewards.Add(l);
    }
```

- [ ] In `AddTier`, after creating the `row`, add `row.SelectedPackage = Packages[0];` before `Tiers.Add(row)`:

```csharp
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
```

- [ ] In `AddLeaderboardReward`, after creating the `row`, add `row.SelectedPackage = Packages[0];` before `LeaderboardRewards.Add(row)`:

```csharp
var row = new SeasonLeaderboardRewardRow
{
    SeasonId  = Season.Id,
    RankMin   = 1,
    RankMax   = 1,
    PackageId = Packages[0].Id,
    IsNew     = true
};
row.SelectedPackage = Packages[0];
LeaderboardRewards.Add(row);
```

- [ ] Fix 6: In `AddObjective`, remove the immediate queue line. Find:

```csharp
Objectives.Add(row);
_queue.Add(SeasonChanges.BuildInsertObjective(row));
StatusIsError = false;
StatusMessage = "Queued INSERT for objective.";
```

Replace with:

```csharp
Objectives.Add(row);
StatusIsError = false;
StatusMessage = "Added objective row. Edit fields, then click 'Queue Save' on the row.";
```

- [ ] Fix 6: Add the `QueueSaveObjective` relay command after `RemoveObjective`:

```csharp
[RelayCommand]
private void QueueSaveObjective(SeasonObjectiveRow? row)
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
        _queue.Add(SeasonChanges.BuildInsertObjective(row));
        StatusMessage = $"Queued INSERT for objective '{row.Name}'.";
    }
    else
    {
        _queue.Add(SeasonChanges.BuildUpdateObjective(row));
        StatusMessage = $"Queued UPDATE for objective '{row.Name}'.";
    }
    StatusIsError = false;
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 8: SeasonDetailView.xaml — Fix 5 (CanQueueSave), Fix 6 (Queue Save column), Fix 3/4 (package CellTemplates)

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml`

- [ ] Fix 5: In the Activity Rates DataGrid, find the "Queue Save" button and add `IsEnabled`:

Find:
```xml
                                <Button Content="Queue Save" Padding="6,1"
                                        Command="{Binding Source={StaticResource VmProxy}, Path=Data.QueueActivityRateSaveCommand}"
                                        CommandParameter="{Binding}"/>
```

Replace with:
```xml
                                <Button Content="Queue Save" Padding="6,1"
                                        Command="{Binding Source={StaticResource VmProxy}, Path=Data.QueueActivityRateSaveCommand}"
                                        CommandParameter="{Binding}"
                                        IsEnabled="{Binding CanQueueSave}"/>
```

- [ ] Fix 6: In the Objectives DataGrid, add a "Queue Save" template column after the "Remove" column. Find the closing `</DataGrid.Columns>` inside the Objectives DataGrid and insert before it:

```xml
                            <DataGridTemplateColumn Header="" Width="110">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="Queue Save" Padding="6,1"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.QueueSaveObjectiveCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
```

- [ ] Fix 3/4 (Tiers): Replace the Tiers DataGrid "Reward Package" `DataGridTemplateColumn`. Find:

```xml
                            <DataGridTemplateColumn Header="Reward Package" Width="*">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock Margin="4,0" VerticalAlignment="Center" Text="{Binding PackageId}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                                <DataGridTemplateColumn.CellEditingTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.Packages}"
                                                  DisplayMemberPath="Name"
                                                  SelectedValuePath="Id"
                                                  IsEditable="True"
                                                  IsTextSearchEnabled="True"
                                                  TextSearch.TextPath="Name"
                                                  SelectedValue="{Binding PackageId, UpdateSourceTrigger=PropertyChanged}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellEditingTemplate>
                            </DataGridTemplateColumn>
```

Replace with:
```xml
                            <DataGridTemplateColumn Header="Reward Package" Width="*">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock Margin="4,0" VerticalAlignment="Center"
                                                   Text="{Binding SelectedPackage.Name, FallbackValue='(none)'}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                                <DataGridTemplateColumn.CellEditingTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.Packages}"
                                                  DisplayMemberPath="Name"
                                                  IsEditable="True"
                                                  IsTextSearchEnabled="True"
                                                  TextSearch.TextPath="Name"
                                                  SelectedItem="{Binding SelectedPackage, UpdateSourceTrigger=PropertyChanged}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellEditingTemplate>
                            </DataGridTemplateColumn>
```

- [ ] Fix 3/4 (Leaderboard): Apply the same change to the Leaderboard DataGrid "Reward Package" column. Find:

```xml
                            <DataGridTemplateColumn Header="Reward Package" Width="*">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock Margin="4,0" VerticalAlignment="Center" Text="{Binding PackageId}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                                <DataGridTemplateColumn.CellEditingTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.Packages}"
                                                  DisplayMemberPath="Name"
                                                  SelectedValuePath="Id"
                                                  IsEditable="True"
                                                  IsTextSearchEnabled="True"
                                                  TextSearch.TextPath="Name"
                                                  SelectedValue="{Binding PackageId, UpdateSourceTrigger=PropertyChanged}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellEditingTemplate>
                            </DataGridTemplateColumn>
```

Replace with:
```xml
                            <DataGridTemplateColumn Header="Reward Package" Width="*">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock Margin="4,0" VerticalAlignment="Center"
                                                   Text="{Binding SelectedPackage.Name, FallbackValue='(none)'}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                                <DataGridTemplateColumn.CellEditingTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.Packages}"
                                                  DisplayMemberPath="Name"
                                                  IsEditable="True"
                                                  IsTextSearchEnabled="True"
                                                  TextSearch.TextPath="Name"
                                                  SelectedItem="{Binding SelectedPackage, UpdateSourceTrigger=PropertyChanged}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellEditingTemplate>
                            </DataGridTemplateColumn>
```

- [ ] Run verification command. Build must succeed.

---

## Task 9: SeasonWizardWindow.xaml — Fix 2, Fix 3/4, Improvement 2, Improvements 8/9

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/SeasonWizardWindow.xaml`

- [ ] Fix 2: In the Step 3 Objectives DataGrid, update the Activity Type `CellEditingTemplate` ComboBox to use `ActiveObjectiveActivityTypeOptions`. Find:

```xml
                                        <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.ObjectiveActivityTypeOptions}"
```

Replace with:

```xml
                                        <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.ActiveObjectiveActivityTypeOptions}"
```

- [ ] Improvements 8: After the Step 3 Objectives DataGrid closing `</DataGrid>`, add the total bonus points label:

```xml
                    <TextBlock Margin="0,6,0,0" FontStyle="Italic" Foreground="DimGray"
                               Visibility="{Binding HasObjectives, Converter={StaticResource BoolToVisibilityHidden}}"
                               Text="{Binding TotalObjectiveBonusPoints, StringFormat='Total bonus points available (all objectives completed): {0}'}"/>
```

- [ ] Fix 3/4 (Tiers): In the Step 4 Tiers DataGrid, replace the "Reward Package" `DataGridTemplateColumn`. Find the column with `Text="{Binding PackageId}"` inside the Step 4 StackPanel and replace its `CellTemplate` and `CellEditingTemplate`:

Find:
```xml
                            <DataGridTemplateColumn Header="Reward Package" Width="*">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock Margin="4,0" VerticalAlignment="Center" Text="{Binding PackageId}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                                <DataGridTemplateColumn.CellEditingTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.Packages}"
                                                  DisplayMemberPath="Name"
                                                  SelectedValuePath="Id"
                                                  IsEditable="True"
                                                  IsTextSearchEnabled="True"
                                                  TextSearch.TextPath="Name"
                                                  SelectedValue="{Binding PackageId, UpdateSourceTrigger=PropertyChanged}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellEditingTemplate>
                            </DataGridTemplateColumn>
```
(this is the one inside Step 4 — there's also one in Step 5, handle them separately)

Replace with:
```xml
                            <DataGridTemplateColumn Header="Reward Package" Width="*">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock Margin="4,0" VerticalAlignment="Center"
                                                   Text="{Binding SelectedPackage.Name, FallbackValue='(none)'}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                                <DataGridTemplateColumn.CellEditingTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.Packages}"
                                                  DisplayMemberPath="Name"
                                                  IsEditable="True"
                                                  IsTextSearchEnabled="True"
                                                  TextSearch.TextPath="Name"
                                                  SelectedItem="{Binding SelectedPackage, UpdateSourceTrigger=PropertyChanged}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellEditingTemplate>
                            </DataGridTemplateColumn>
```

- [ ] Improvement 9: After the Step 4 Tiers DataGrid closing `</DataGrid>`, add the tier totals label:

```xml
                    <StackPanel Margin="0,6,0,0" Orientation="Horizontal"
                                Visibility="{Binding HasTiers, Converter={StaticResource BoolToVisibilityHidden}}">
                        <TextBlock FontStyle="Italic" Foreground="DimGray"
                                   Text="{Binding MaxTierPoints, StringFormat='Top tier threshold: {0:N0} pts'}"/>
                        <TextBlock Margin="16,0,0,0" FontStyle="Italic" Foreground="DimGray"
                                   Text="{Binding TotalObjectiveBonusPoints, StringFormat='  |  Objective bonus available: {0} pts'}"/>
                    </StackPanel>
```

- [ ] Fix 3/4 (Leaderboard): Apply the same CellTemplate/CellEditingTemplate change to the Step 5 Leaderboard "Reward Package" column. Find the second occurrence:

```xml
                            <DataGridTemplateColumn Header="Reward Package" Width="*">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock Margin="4,0" VerticalAlignment="Center" Text="{Binding PackageId}"/>
```
(inside Step 5 StackPanel)

Replace both CellTemplate and CellEditingTemplate the same way as the Tiers column above.

- [ ] Improvement 2: Replace the entire Step 6 Review `StackPanel` content (keeping the outer Visibility-bound StackPanel wrapper). Find:

```xml
                <!-- Step 6: Review -->
                <StackPanel Visibility="{Binding IsReviewStep, Converter={StaticResource BoolToVisibilityHidden}, FallbackValue=Collapsed}">
                    <TextBlock Text="Review" FontSize="14" FontWeight="Bold" Margin="0,0,0,8"/>
                    <Grid Margin="0,0,0,12">
```

Replace the entire inner content of that StackPanel (from `<TextBlock Text="Review"...>` to the closing `</Border>`) with:

```xml
                    <TextBlock Text="Review" FontSize="14" FontWeight="Bold" Margin="0,0,0,8"/>

                    <!-- Season Info -->
                    <Grid Margin="0,0,0,8">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="180"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>
                        <TextBlock Grid.Row="0" Grid.Column="0" Text="Name:" Margin="0,2"/>
                        <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding Name}" Margin="0,2"/>
                        <TextBlock Grid.Row="1" Grid.Column="0" Text="Description:" Margin="0,2"/>
                        <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding Description}" TextWrapping="Wrap" Margin="0,2"/>
                        <TextBlock Grid.Row="2" Grid.Column="0" Text="Start:" Margin="0,2"/>
                        <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding StartTime, StringFormat=yyyy-MM-dd HH:mm}" Margin="0,2"/>
                        <TextBlock Grid.Row="3" Grid.Column="0" Text="End:" Margin="0,2"/>
                        <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding EndTime, StringFormat=yyyy-MM-dd HH:mm}" Margin="0,2"/>
                    </Grid>

                    <!-- Active Rates -->
                    <TextBlock Text="Active Rates:" FontWeight="SemiBold" Margin="0,4,0,2"
                               Visibility="{Binding HasActiveRates, Converter={StaticResource BoolToVisibilityHidden}}"/>
                    <ItemsControl ItemsSource="{Binding ReviewActiveRates}"
                                  Visibility="{Binding HasActiveRates, Converter={StaticResource BoolToVisibilityHidden}}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding}" FontFamily="Consolas" FontSize="12"/>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>

                    <!-- Objectives -->
                    <TextBlock Text="{Binding ReviewObjectivesHeader}" FontWeight="SemiBold" Margin="0,4,0,2"/>
                    <ItemsControl ItemsSource="{Binding ReviewObjectiveLines}"
                                  Visibility="{Binding HasObjectives, Converter={StaticResource BoolToVisibilityHidden}}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding}" FontFamily="Consolas" FontSize="12" TextWrapping="Wrap"/>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>

                    <!-- Tiers -->
                    <TextBlock Text="{Binding ReviewTiersHeader}" FontWeight="SemiBold" Margin="0,4,0,2"/>
                    <ItemsControl ItemsSource="{Binding ReviewTierLines}"
                                  Visibility="{Binding HasTiers, Converter={StaticResource BoolToVisibilityHidden}}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding}" FontFamily="Consolas" FontSize="12" TextWrapping="Wrap"/>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>

                    <!-- Leaderboard -->
                    <TextBlock Text="{Binding ReviewLeaderboardHeader}" FontWeight="SemiBold" Margin="0,4,0,2"/>
                    <ItemsControl ItemsSource="{Binding ReviewLeaderboardLines}"
                                  Visibility="{Binding HasActiveRates, Converter={StaticResource BoolToVisibilityHidden}}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding}" FontFamily="Consolas" FontSize="12" TextWrapping="Wrap"/>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>

                    <!-- Info banner -->
                    <Border Background="#E3F2FD" BorderBrush="#1E88E5" BorderThickness="1" Padding="8" Margin="0,8,0,0">
                        <TextBlock TextWrapping="Wrap" Text="{Binding FinishHint}"/>
                    </Border>
```

- [ ] Run verification command. Build must succeed with 0 errors.

---

## Self-Review Checklist

- [x] Fix 1 (SQL variable): `BuildInsertPackageWithItems` uses `@pkgId_{guid8}` — Task 2
- [x] Fix 2 (disabled activities in wizard Step 3): `ActiveObjectiveActivityTypeOptions` filtered from `ActivityRates` — Task 6 VM, Task 9 XAML
- [x] Fix 3/4 (package shows "0"): `SelectedPackage` on `SeasonTierRow` + `SeasonLeaderboardRewardRow`, set on load/add, CellTemplate/EditingTemplate updated — Tasks 4, 7, 8, 9
- [x] Fix 5 (save disabled rates): `CanQueueSave` property on `SeasonActivityRateRow`, `IsEnabled` on button — Task 3, Task 8
- [x] Fix 6 (objectives default values): `AddObjective` no longer queues immediately; `QueueSaveObjective` command added; Queue Save column added to XAML — Task 7, Task 8
- [x] Fix 7 (server startup intro email): `_pendingIntroChars` queue drains in `RefreshCache` — Task 1
- [x] Fix 8 (forced season end): `RefreshCache` detects admin deactivation and calls `ProcessSeasonEnd` — Task 1
- [x] Improvement 1 (deferred wizard): `BuildSeasonScript()` emits one combined SQL batch — Task 6
- [x] Improvement 2 (review step): full review summary with sections for rates/objectives/tiers/leaderboard — Tasks 6, 9
- [x] Improvement 4 (tier labels): `tiertype`/`tierlevel` added to `LookupCache` query, `EntityPickItem`, and `PackageItemPickItem.GetTierLabel` — Task 5
- [x] Improvements 8/9 (point totals): `TotalObjectiveBonusPoints` + `MaxTierPoints` properties + XAML labels — Tasks 6, 9
- [x] `SeasonLeaderboardRewardRow` leaderboard review section uses `ReviewLeaderboardLines` (not `HasActiveRates` — **fix the Visibility binding in Task 9 last step**: change `HasActiveRates` to a new `HasLeaderboardRewards` property or use `LeaderboardRewards.Count > 0`. Add `public bool HasLeaderboardRewards => LeaderboardRewards.Count > 0;` to the VM and notify it in the `LeaderboardRewards.CollectionChanged` subscription. Then fix the XAML `Visibility="{Binding HasLeaderboardRewards, ...}"`)
