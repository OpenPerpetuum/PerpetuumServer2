# Seasons Admin Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Seasons tab to Perpetuum.AdminTool for creating, managing, and monitoring game seasons and reward packages.

**Architecture:** Data layer (row models + repositories + change objects) built first; ViewModels built second; WPF Views built last. Each layer builds on the previous. LookupCache gets a small additive change for the `hidden` column.

**Tech Stack:** WPF .NET 8, CommunityToolkit.Mvvm, Microsoft.Data.SqlClient (direct SQL), existing ChangeQueue/IPendingChange pattern.

**Verification command (every task):**
```
dotnet build E:\MyStuff\Projects\PerpetuumServer2\PerpetuumServer2.sln -c Release -p:Platform=x64
```

---

## Task 1: Add `hidden` column to LookupCache + EntityPickItem

**Goal:** Surface the `entitydefaults.hidden` column on `EntityPickItem` so the package item picker can exclude hidden definitions.

- [ ] Edit `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Common\EntityPickItem.cs` — replace contents with:

```csharp
namespace Perpetuum.AdminTool.Common
{
    public class EntityPickItem
    {
        public int Definition { get; init; }
        public string Name { get; init; } = "";

        // Exposed so consumers (structured editors, NPC-loot/relations dropdowns,
        // potential category filters) can match on category without an extra DB hit.
        public long CategoryFlags { get; init; }

        // Mirrors entitydefaults.enabled. Consumers (structured editors, future
        // selectors) hide disabled rows. Newly inserted rows default to enabled = 1
        // so they show up automatically once the cache refreshes post-commit.
        public bool Enabled { get; init; }

        // Mirrors entitydefaults.hidden. The package-item picker uses this to
        // exclude hidden rows from selection. NULL/missing in DB → false.
        public bool Hidden { get; init; }

        public string Display => $"{Definition} — {Name}";
    }
}
```

- [ ] Edit `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Common\LookupCache.cs` — change the SQL on line 50 and the reader block. The new file content is:

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Common
{
    /// <summary>
    /// Process-wide cache of small lookup tables that drive dropdowns in multiple tabs:
    /// entitydefaults (definition, definitionname) and robottemplates (id, name).
    /// Refresh on app start, after every successful Direct-DB commit, and from the
    /// per-tab Reload buttons.
    /// </summary>
    public class LookupCache
    {
        public ObservableCollection<EntityPickItem> Entities { get; } = new();
        public ObservableCollection<TemplatePickItem> Templates { get; } = new();

        public Dictionary<int, string> EntityNamesByDefinition { get; private set; } = new();
        public Dictionary<int, string> TemplateNamesById { get; private set; } = new();

        public async Task RefreshEntitiesAsync(ConnectionSettings connection)
        {
            await using var cn = new SqlConnection(connection.BuildConnectionString());
            await cn.OpenAsync();
            await RefreshEntitiesAsync(cn);
        }

        public async Task RefreshTemplatesAsync(ConnectionSettings connection)
        {
            await using var cn = new SqlConnection(connection.BuildConnectionString());
            await cn.OpenAsync();
            await RefreshTemplatesAsync(cn);
        }

        public async Task RefreshAllAsync(ConnectionSettings connection)
        {
            await using var cn = new SqlConnection(connection.BuildConnectionString());
            await cn.OpenAsync();
            await RefreshEntitiesAsync(cn);
            await RefreshTemplatesAsync(cn);
        }

        private async Task RefreshEntitiesAsync(SqlConnection cn)
        {
            var fresh = new List<EntityPickItem>();
            var names = new Dictionary<int, string>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select definition, definitionname, categoryflags, enabled, hidden from entitydefaults order by definitionname";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var def = reader.GetInt32(0);
                var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var categoryFlags = reader.IsDBNull(2) ? 0L : reader.GetInt64(2);
                var enabled = !reader.IsDBNull(3) && reader.GetBoolean(3);
                var hidden = !reader.IsDBNull(4) && reader.GetBoolean(4);
                fresh.Add(new EntityPickItem
                {
                    Definition = def,
                    Name = name,
                    CategoryFlags = categoryFlags,
                    Enabled = enabled,
                    Hidden = hidden
                });
                names[def] = name;
            }
            Entities.Clear();
            foreach (var p in fresh) Entities.Add(p);
            EntityNamesByDefinition = names;
        }

        private async Task RefreshTemplatesAsync(SqlConnection cn)
        {
            var fresh = new List<TemplatePickItem>();
            var names = new Dictionary<int, string>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select id, name from robottemplates order by name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                fresh.Add(new TemplatePickItem { Id = id, Name = name });
                names[id] = name;
            }
            Templates.Clear();
            foreach (var p in fresh) Templates.Add(p);
            TemplateNamesById = names;
        }
    }
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 2: PackageItemPickItem with category filter

**Goal:** Provide a filtered pick list for the Package Items entity picker. Filters by enabled, not hidden, and one of 11 root category flags (or any descendant).

- [ ] Create new directory `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Packages\` (no action needed — Write tool creates it).

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Packages\PackageItemPickItem.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Entities;
using Perpetuum.ExportedTypes;

namespace Perpetuum.AdminTool.Packages
{
    /// <summary>
    /// Display row for the package-item entity picker. Wraps the chosen definition
    /// and resolved display name. The picker pre-filters the LookupCache once per
    /// load (and once per cache refresh) using <see cref="BuildFilteredList"/>.
    /// </summary>
    public record PackageItemPickItem(int Definition, string DisplayName)
    {
        public string Display => $"{Definition} — {DisplayName}";

        // The 11 allowed root category flag names. An entity passes the filter if its
        // categoryflags falls under one of these roots (root match OR descendant match).
        // See spec §"Entity Picker for Package Items" → Filtering.
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

        /// <summary>
        /// Builds the allowed list from a LookupCache snapshot. Filters out disabled
        /// and hidden rows, then accepts only entities whose CategoryFlags fall under
        /// one of the 11 allowed root categories (descendant match via byte-mask).
        ///
        /// `hierarchy` is currently unused but is accepted for forward compatibility
        /// — a future schema may change category math; passing the precomputed
        /// hierarchy avoids re-walking the catalog if needed.
        /// </summary>
        public static List<PackageItemPickItem> BuildFilteredList(
            IEnumerable<EntityPickItem> all,
            CategoryFlagsHierarchy? hierarchy = null)
        {
            var result = new List<PackageItemPickItem>();
            foreach (var e in all)
            {
                if (!e.Enabled) continue;
                if (e.Hidden) continue;
                if (e.CategoryFlags == 0) continue;
                if (!MatchesAnyRoot(e.CategoryFlags)) continue;
                result.Add(new PackageItemPickItem(e.Definition, e.Name));
            }
            return result.OrderBy(p => p.DisplayName, System.StringComparer.OrdinalIgnoreCase).ToList();
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

        // Mirror of Perpetuum.CategoryFlagsExtensions.GetCategoryFlagsMask, adapted
        // to operate on long. Same math used in RobotTemplateSlotViewModel.RebuildAmmoPicks.
        private static long CategoryFlagsMask(long target)
        {
            var mask = unchecked((long)0xFFFFFFFFFFFFFFFFUL);
            while (((ulong)target & (ulong)mask) > 0)
            {
                mask <<= 8;
            }
            return ~mask;
        }
    }
}
```

> **Reference for hierarchy parameter:** The `CategoryFlagsHierarchy` argument is reserved for callers that already hold a built hierarchy and want to pass it through. The current implementation uses pure bit-math (matching `RobotTemplateSlotViewModel.RebuildAmmoPicks`) and does not need the tree.

- [ ] Run verification command. Build must succeed.

---

## Task 3: Row models (8 files)

**Goal:** Define the row model classes that view-models will operate on.

### Task 3a: SeasonRow

- [ ] Create new directory `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Seasons\`.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Seasons\SeasonRow.cs`:

```csharp
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonRow : ObservableObject
    {
        // PK is the identity column `id`. 0 means a new (unsaved) row.
        public int Id { get; }

        public bool IsNew { get; set; }
        public SeasonSnapshot Original { get; private set; }

        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _description = "";
        [ObservableProperty] private DateTime _startTime;
        [ObservableProperty] private DateTime _endTime;
        [ObservableProperty] private bool _isActive;

        public SeasonRow(SeasonSnapshot snapshot)
        {
            Id = snapshot.Id;
            Original = snapshot;
            ApplySnapshot(snapshot);
        }

        public void ApplySnapshot(SeasonSnapshot s)
        {
            Original = s;
            Name = s.Name;
            Description = s.Description;
            StartTime = s.StartTime;
            EndTime = s.EndTime;
            IsActive = s.IsActive;
        }

        public void RefreshOriginalFromCurrent()
        {
            Original = new SeasonSnapshot
            {
                Id = Id,
                Name = Name,
                Description = Description,
                StartTime = StartTime,
                EndTime = EndTime,
                IsActive = IsActive
            };
        }

        public static SeasonRow CreateNew(SeasonSnapshot seed)
        {
            return new SeasonRow(seed) { IsNew = true };
        }

        // Card visual state per spec §Tab Structure → Seasons View → Season Cards.
        public SeasonCardState CardState
        {
            get
            {
                if (IsActive) return SeasonCardState.Active;
                return EndTime > DateTime.UtcNow ? SeasonCardState.Draft : SeasonCardState.Ended;
            }
        }
    }

    public class SeasonSnapshot
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public DateTime StartTime { get; init; }
        public DateTime EndTime { get; init; }
        public bool IsActive { get; init; }
    }

    public enum SeasonCardState
    {
        Active,
        Draft,
        Ended
    }
}
```

### Task 3b: SeasonActivityRateRow

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Seasons\SeasonActivityRateRow.cs`:

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonActivityRateRow : ObservableObject
    {
        // PK is identity `id`. 0 = no DB row exists yet for this (season, activity) pair;
        // the upsert MERGE will INSERT in that case.
        public int Id { get; set; }
        public int SeasonId { get; set; }

        [ObservableProperty] private SeasonActivityType _activityType;
        [ObservableProperty] private double _pointsPerUnit;
        [ObservableProperty] private int _unitScale = 1;

        public string ActivityTypeLabel => ActivityType switch
        {
            SeasonActivityType.NpcKill         => "NPC Kill",
            SeasonActivityType.PvpKill         => "PvP Kill",
            SeasonActivityType.MissionComplete => "Mission Complete",
            SeasonActivityType.MineralMined    => "Mineral Mined",
            SeasonActivityType.EpSpent         => "EP Spent",
            SeasonActivityType.NicEarned       => "NIC Earned",
            SeasonActivityType.NicSpent        => "NIC Spent",
            SeasonActivityType.IntrusionPoint  => "Intrusion Point",
            _ => ActivityType.ToString()
        };

        public string EffectiveRate => GetEffectiveRateLabel(ActivityType, PointsPerUnit, UnitScale);

        partial void OnPointsPerUnitChanged(double value) => OnPropertyChanged(nameof(EffectiveRate));
        partial void OnUnitScaleChanged(int value) => OnPropertyChanged(nameof(EffectiveRate));
        partial void OnActivityTypeChanged(SeasonActivityType value) => OnPropertyChanged(nameof(EffectiveRate));

        public static string GetEffectiveRateLabel(SeasonActivityType type, double pointsPerUnit, int unitScale)
        {
            if (pointsPerUnit == 0) return "Disabled";

            var pts = pointsPerUnit.ToString("0.##", CultureInfo.InvariantCulture);
            var scale = unitScale.ToString("N0", CultureInfo.InvariantCulture);

            return type switch
            {
                SeasonActivityType.NpcKill         => $"{pts} pts per kill",
                SeasonActivityType.PvpKill         => $"{pts} pts per kill",
                SeasonActivityType.MissionComplete => $"{pts} pts per completion",
                SeasonActivityType.IntrusionPoint  => $"{pts} pts per intrusion point",
                SeasonActivityType.MineralMined    => unitScale > 1
                    ? $"{pts} pts per {scale} units mined"
                    : $"{pts} pts per unit mined",
                SeasonActivityType.EpSpent         => unitScale > 1
                    ? $"{pts} pts per {scale} EP spent"
                    : $"{pts} pts per EP spent",
                SeasonActivityType.NicEarned       => unitScale > 1
                    ? $"{pts} pts per {scale} NIC earned"
                    : $"{pts} pts per NIC earned",
                SeasonActivityType.NicSpent        => unitScale > 1
                    ? $"{pts} pts per {scale} NIC spent"
                    : $"{pts} pts per NIC spent",
                _ => $"{pts} pts"
            };
        }
    }
}
```

### Task 3c: SeasonObjectiveRow

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Seasons\SeasonObjectiveRow.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonObjectiveRow : ObservableObject
    {
        public int Id { get; set; }       // 0 = new
        public int SeasonId { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _description = "";
        [ObservableProperty] private SeasonActivityType _activityType = SeasonActivityType.NpcKill;
        [ObservableProperty] private long _targetValue;
        [ObservableProperty] private int _bonusPoints;
        [ObservableProperty] private int _displayOrder;
    }
}
```

### Task 3d: SeasonTierRow

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Seasons\SeasonTierRow.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonTierRow : ObservableObject
    {
        public int Id { get; set; }       // 0 = new
        public int SeasonId { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private int _tierNumber;
        [ObservableProperty] private string _tierName = "";
        [ObservableProperty] private int _pointsRequired;
        [ObservableProperty] private int _packageId;
    }
}
```

### Task 3e: SeasonLeaderboardRewardRow

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Seasons\SeasonLeaderboardRewardRow.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonLeaderboardRewardRow : ObservableObject
    {
        public int Id { get; set; }       // 0 = new
        public int SeasonId { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private int _rankMin = 1;
        [ObservableProperty] private int _rankMax = 1;
        [ObservableProperty] private int _packageId;
    }
}
```

### Task 3f: PackageRow

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Packages\PackageRow.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Packages
{
    public partial class PackageRow : ObservableObject
    {
        public int Id { get; set; }       // 0 = new (unsaved)
        public bool IsNew { get; set; }

        [ObservableProperty] private string _name = "";
        [ObservableProperty] private int _itemCount;
        [ObservableProperty] private int _seasonCount;

        public bool IsUnused => SeasonCount == 0;
        public string Display => $"{Name}";

        public string SubtitleText => SeasonCount == 0
            ? $"{ItemCount} item(s) — Not used"
            : $"{ItemCount} item(s) — Used by {SeasonCount} season(s)";

        partial void OnSeasonCountChanged(int value)
        {
            OnPropertyChanged(nameof(IsUnused));
            OnPropertyChanged(nameof(SubtitleText));
        }

        partial void OnItemCountChanged(int value)
        {
            OnPropertyChanged(nameof(SubtitleText));
        }

        partial void OnNameChanged(string value)
        {
            OnPropertyChanged(nameof(Display));
        }
    }
}
```

### Task 3g: PackageItemRow

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Packages\PackageItemRow.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Packages
{
    public partial class PackageItemRow : ObservableObject
    {
        public int Id { get; set; }       // 0 = new (unsaved)
        public int PackageId { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private int _definition;
        [ObservableProperty] private int _quantity = 1;
        [ObservableProperty] private string _displayName = "";
    }
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 4: SeasonRepository

**Goal:** Provide all SQL reads for seasons and statistics.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Seasons\SeasonRepository.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;
using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.Seasons
{
    public class SeasonRepository
    {
        private readonly ConnectionSettings _connection;

        public SeasonRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<List<SeasonRow>> LoadAllSeasonsAsync()
        {
            var result = new List<SeasonRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, name, description, start_time, end_time, is_active " +
                "FROM seasons ORDER BY start_time DESC";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var snap = new SeasonSnapshot
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    StartTime = reader.GetDateTime(3),
                    EndTime = reader.GetDateTime(4),
                    IsActive = !reader.IsDBNull(5) && reader.GetBoolean(5)
                };
                result.Add(new SeasonRow(snap));
            }
            return result;
        }

        public async Task<List<SeasonActivityRateRow>> LoadActivityRatesAsync(int seasonId)
        {
            var result = new List<SeasonActivityRateRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, season_id, activity_type, points_per_unit, unit_scale " +
                "FROM season_activity_rates WHERE season_id = @seasonId";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new SeasonActivityRateRow
                {
                    Id = reader.GetInt32(0),
                    SeasonId = reader.GetInt32(1),
                    ActivityType = (SeasonActivityType)reader.GetInt32(2),
                    PointsPerUnit = reader.GetDouble(3),
                    UnitScale = reader.GetInt32(4)
                });
            }
            return result;
        }

        public async Task<List<SeasonObjectiveRow>> LoadObjectivesAsync(int seasonId)
        {
            var result = new List<SeasonObjectiveRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, season_id, name, description, activity_type, " +
                "target_value, bonus_points, display_order " +
                "FROM season_objectives WHERE season_id = @seasonId ORDER BY display_order";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new SeasonObjectiveRow
                {
                    Id = reader.GetInt32(0),
                    SeasonId = reader.GetInt32(1),
                    Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    ActivityType = (SeasonActivityType)reader.GetInt32(4),
                    TargetValue = reader.GetInt64(5),
                    BonusPoints = reader.GetInt32(6),
                    DisplayOrder = reader.GetInt32(7)
                });
            }
            return result;
        }

        public async Task<List<SeasonTierRow>> LoadTiersAsync(int seasonId)
        {
            var result = new List<SeasonTierRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, season_id, tier_number, tier_name, points_required, package_id " +
                "FROM season_tiers WHERE season_id = @seasonId ORDER BY tier_number";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new SeasonTierRow
                {
                    Id = reader.GetInt32(0),
                    SeasonId = reader.GetInt32(1),
                    TierNumber = reader.GetInt32(2),
                    TierName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    PointsRequired = reader.GetInt32(4),
                    PackageId = reader.GetInt32(5)
                });
            }
            return result;
        }

        public async Task<List<SeasonLeaderboardRewardRow>> LoadLeaderboardRewardsAsync(int seasonId)
        {
            var result = new List<SeasonLeaderboardRewardRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, season_id, rank_min, rank_max, package_id " +
                "FROM season_leaderboard_rewards WHERE season_id = @seasonId ORDER BY rank_min";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new SeasonLeaderboardRewardRow
                {
                    Id = reader.GetInt32(0),
                    SeasonId = reader.GetInt32(1),
                    RankMin = reader.GetInt32(2),
                    RankMax = reader.GetInt32(3),
                    PackageId = reader.GetInt32(4)
                });
            }
            return result;
        }

        public async Task<int> LoadParticipantCountAsync(int seasonId)
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM season_character_points WHERE season_id = @seasonId";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            var v = await cmd.ExecuteScalarAsync();
            return v == null ? 0 : System.Convert.ToInt32(v);
        }

        public async Task<int> LoadActiveLast7DaysAsync(int seasonId)
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM season_character_points " +
                "WHERE season_id = @seasonId AND last_updated >= DATEADD(day, -7, GETUTCDATE())";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            var v = await cmd.ExecuteScalarAsync();
            return v == null ? 0 : System.Convert.ToInt32(v);
        }

        public async Task<List<TierDistributionRow>> LoadTierDistributionAsync(int seasonId)
        {
            var result = new List<TierDistributionRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT t.tier_number, t.tier_name, COUNT(c.character_id) AS claim_count " +
                "FROM season_tiers t " +
                "LEFT JOIN season_tier_claims c ON c.tier_id = t.id AND c.season_id = @seasonId " +
                "WHERE t.season_id = @seasonId " +
                "GROUP BY t.id, t.tier_number, t.tier_name " +
                "ORDER BY t.tier_number";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new TierDistributionRow(
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.GetInt32(2)));
            }
            return result;
        }

        public async Task<List<LeaderboardEntryRow>> LoadTop10LeaderboardAsync(int seasonId)
        {
            var result = new List<LeaderboardEntryRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT TOP 10 scp.character_id, ch.nick AS character_name, scp.total_points " +
                "FROM season_character_points scp " +
                "JOIN characters ch ON ch.characterID = scp.character_id " +
                "WHERE scp.season_id = @seasonId " +
                "ORDER BY scp.total_points DESC";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            int rank = 1;
            while (await reader.ReadAsync())
            {
                var nick = reader.IsDBNull(1) ? $"(char {reader.GetInt32(0)})" : reader.GetString(1);
                result.Add(new LeaderboardEntryRow(rank++, nick, reader.GetInt64(2)));
            }
            return result;
        }

        public async Task<List<ObjectiveCompletionRow>> LoadObjectiveCompletionAsync(int seasonId)
        {
            var result = new List<ObjectiveCompletionRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT o.id, o.name, COUNT(p.character_id) AS completed_count " +
                "FROM season_objectives o " +
                "LEFT JOIN season_objective_progress p ON p.objective_id = o.id " +
                "    AND p.season_id = @seasonId AND p.completed = 1 " +
                "WHERE o.season_id = @seasonId " +
                "GROUP BY o.id, o.name, o.display_order " +
                "ORDER BY o.display_order";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new ObjectiveCompletionRow(
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.GetInt32(2)));
            }
            return result;
        }

        public async Task<double> LoadAvgPointsPerDayAsync(int seasonId)
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT " +
                "    CAST(SUM(total_points) AS float) / " +
                "    NULLIF(COUNT(*), 0) / " +
                "    NULLIF(DATEDIFF(day, s.start_time, GETUTCDATE()), 0) AS avg_points_per_day " +
                "FROM season_character_points scp " +
                "JOIN seasons s ON s.id = scp.season_id " +
                "WHERE scp.season_id = @seasonId " +
                "GROUP BY s.start_time";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            var v = await cmd.ExecuteScalarAsync();
            if (v == null || v == System.DBNull.Value) return 0.0;
            return System.Convert.ToDouble(v);
        }
    }

    public record TierDistributionRow(int TierNumber, string TierName, int ClaimCount);
    public record LeaderboardEntryRow(int Rank, string CharacterName, long TotalPoints);
    public record ObjectiveCompletionRow(string Name, int CompletedCount);
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 5: PackageRepository

**Goal:** Provide SQL reads for packages and package items, including season usage.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Packages\PackageRepository.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Packages
{
    public class PackageRepository
    {
        private readonly ConnectionSettings _connection;

        public PackageRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<List<PackageRow>> LoadAllPackagesAsync()
        {
            var result = new List<PackageRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT " +
                "    p.id, " +
                "    p.name, " +
                "    (SELECT COUNT(*) FROM packageitems pi WHERE pi.packageid = p.id) AS item_count, " +
                "    (SELECT COUNT(DISTINCT season_id) " +
                "     FROM ( " +
                "         SELECT season_id FROM season_tiers WHERE package_id = p.id " +
                "         UNION ALL " +
                "         SELECT season_id FROM season_leaderboard_rewards WHERE package_id = p.id " +
                "     ) refs " +
                "    ) AS season_count " +
                "FROM packages p " +
                "ORDER BY p.name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new PackageRow
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ItemCount = reader.GetInt32(2),
                    SeasonCount = reader.GetInt32(3)
                });
            }
            return result;
        }

        public async Task<List<PackageItemRow>> LoadPackageItemsAsync(int packageId)
        {
            var result = new List<PackageItemRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, packageid, definition, quantity " +
                "FROM packageitems WHERE packageid = @packageId";
            cmd.Parameters.AddWithValue("@packageId", packageId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new PackageItemRow
                {
                    Id = reader.GetInt32(0),
                    PackageId = reader.GetInt32(1),
                    Definition = reader.GetInt32(2),
                    Quantity = reader.GetInt32(3)
                });
            }
            return result;
        }

        public async Task<List<PackageUsageRow>> LoadSeasonUsageAsync(int packageId)
        {
            var result = new List<PackageUsageRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT s.id AS season_id, s.name AS season_name, s.is_active, " +
                "       'Tier' AS context, t.tier_name AS detail " +
                "FROM season_tiers t " +
                "JOIN seasons s ON s.id = t.season_id " +
                "WHERE t.package_id = @packageId " +
                "UNION ALL " +
                "SELECT s.id, s.name, s.is_active, " +
                "       'Leaderboard' AS context, " +
                "       'Rank ' + CAST(lr.rank_min AS varchar) + '-' + CAST(lr.rank_max AS varchar) AS detail " +
                "FROM season_leaderboard_rewards lr " +
                "JOIN seasons s ON s.id = lr.season_id " +
                "WHERE lr.package_id = @packageId " +
                "ORDER BY season_name, context";
            cmd.Parameters.AddWithValue("@packageId", packageId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new PackageUsageRow(
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    !reader.IsDBNull(2) && reader.GetBoolean(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.IsDBNull(4) ? "" : reader.GetString(4)));
            }
            return result;
        }
    }

    public record PackageUsageRow(int SeasonId, string SeasonName, bool IsActive, string Context, string Detail);
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 6: SeasonChanges

**Goal:** Build `IPendingChange` instances for all season-related mutations. Follows the `FlockChanges`/`PresenceChanges` static-class pattern.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Seasons\SeasonChanges.cs`:

```csharp
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.Seasons
{
    /// <summary>
    /// Static factory of IPendingChange instances for every season mutation.
    /// The view-models call these directly and add the result to the ChangeQueue —
    /// there is no bulk "compute diff" helper because the seasons UI uses
    /// per-row queue-on-save (cleaner per-detail-tab semantics than a bulk diff).
    /// </summary>
    public static class SeasonChanges
    {
        // ------------------------- seasons table -------------------------

        public static IPendingChange BuildInsert(SeasonRow row)
        {
            return new RawSqlChange(
                $"seasons: insert '{row.Name}' (start {row.StartTime:yyyy-MM-dd})",
                "INSERT INTO seasons (name, description, start_time, end_time, is_active) " +
                "VALUES (" +
                $"{SqlLiteral.Of(row.Name)}, " +
                $"{SqlLiteral.Of(row.Description ?? "")}, " +
                $"{SqlLiteral.Of(row.StartTime.ToString("yyyy-MM-dd HH:mm:ss"))}, " +
                $"{SqlLiteral.Of(row.EndTime.ToString("yyyy-MM-dd HH:mm:ss"))}, " +
                "0)");
        }

        public static IPendingChange BuildUpdate(SeasonRow row)
        {
            return new RawSqlChange(
                $"seasons: update id {row.Id} ('{row.Name}')",
                "UPDATE seasons SET " +
                $"name = {SqlLiteral.Of(row.Name)}, " +
                $"description = {SqlLiteral.Of(row.Description ?? "")}, " +
                $"start_time = {SqlLiteral.Of(row.StartTime.ToString("yyyy-MM-dd HH:mm:ss"))}, " +
                $"end_time = {SqlLiteral.Of(row.EndTime.ToString("yyyy-MM-dd HH:mm:ss"))} " +
                $"WHERE id = {row.Id}");
        }

        public static IPendingChange BuildActivate(int seasonId)
        {
            return new RawSqlChange(
                $"seasons: activate id {seasonId}",
                $"UPDATE seasons SET is_active = 1 WHERE id = {seasonId}");
        }

        public static IPendingChange BuildDeactivate(int seasonId)
        {
            return new RawSqlChange(
                $"seasons: deactivate id {seasonId}",
                $"UPDATE seasons SET is_active = 0 WHERE id = {seasonId}");
        }

        // ----------------------- activity rates -----------------------

        public static IPendingChange BuildUpsertActivityRate(SeasonActivityRateRow row)
        {
            var sql =
                "MERGE season_activity_rates AS target " +
                $"USING (SELECT {row.SeasonId} AS season_id, {(int)row.ActivityType} AS activity_type) AS src " +
                "ON target.season_id = src.season_id AND target.activity_type = src.activity_type " +
                "WHEN MATCHED THEN " +
                $"    UPDATE SET points_per_unit = {SqlLiteral.Of(row.PointsPerUnit)}, unit_scale = {row.UnitScale} " +
                "WHEN NOT MATCHED THEN " +
                "    INSERT (season_id, activity_type, points_per_unit, unit_scale) " +
                $"    VALUES ({row.SeasonId}, {(int)row.ActivityType}, {SqlLiteral.Of(row.PointsPerUnit)}, {row.UnitScale});";

            return new RawSqlChange(
                $"season_activity_rates: upsert season {row.SeasonId} type {row.ActivityType} ({row.PointsPerUnit}/unit, scale {row.UnitScale})",
                sql);
        }

        public static IPendingChange BuildDeleteActivityRate(int id)
        {
            return new RawSqlChange(
                $"season_activity_rates: delete id {id}",
                $"DELETE FROM season_activity_rates WHERE id = {id}",
                isDestructive: true);
        }

        // ----------------------- objectives -----------------------

        public static IPendingChange BuildInsertObjective(SeasonObjectiveRow row)
        {
            return new RawSqlChange(
                $"season_objectives: insert season {row.SeasonId} '{row.Name}'",
                "INSERT INTO season_objectives " +
                "(season_id, name, description, activity_type, target_value, bonus_points, display_order) " +
                "VALUES (" +
                $"{row.SeasonId}, " +
                $"{SqlLiteral.Of(row.Name)}, " +
                $"{SqlLiteral.Of(row.Description ?? "")}, " +
                $"{(int)row.ActivityType}, " +
                $"{row.TargetValue}, " +
                $"{row.BonusPoints}, " +
                $"{row.DisplayOrder})");
        }

        public static IPendingChange BuildUpdateObjective(SeasonObjectiveRow row)
        {
            return new RawSqlChange(
                $"season_objectives: update id {row.Id} ('{row.Name}')",
                "UPDATE season_objectives SET " +
                $"name = {SqlLiteral.Of(row.Name)}, " +
                $"description = {SqlLiteral.Of(row.Description ?? "")}, " +
                $"activity_type = {(int)row.ActivityType}, " +
                $"target_value = {row.TargetValue}, " +
                $"bonus_points = {row.BonusPoints}, " +
                $"display_order = {row.DisplayOrder} " +
                $"WHERE id = {row.Id}");
        }

        public static IPendingChange BuildDeleteObjective(int id)
        {
            return new RawSqlChange(
                $"season_objectives: delete id {id}",
                $"DELETE FROM season_objectives WHERE id = {id}",
                isDestructive: true);
        }

        // ----------------------- tiers -----------------------

        public static IPendingChange BuildInsertTier(SeasonTierRow row)
        {
            return new RawSqlChange(
                $"season_tiers: insert season {row.SeasonId} tier {row.TierNumber} ('{row.TierName}')",
                "INSERT INTO season_tiers (season_id, tier_number, tier_name, points_required, package_id) " +
                "VALUES (" +
                $"{row.SeasonId}, " +
                $"{row.TierNumber}, " +
                $"{SqlLiteral.Of(row.TierName)}, " +
                $"{row.PointsRequired}, " +
                $"{row.PackageId})");
        }

        public static IPendingChange BuildUpdateTier(SeasonTierRow row)
        {
            return new RawSqlChange(
                $"season_tiers: update id {row.Id} (tier {row.TierNumber}, '{row.TierName}')",
                "UPDATE season_tiers SET " +
                $"tier_number = {row.TierNumber}, " +
                $"tier_name = {SqlLiteral.Of(row.TierName)}, " +
                $"points_required = {row.PointsRequired}, " +
                $"package_id = {row.PackageId} " +
                $"WHERE id = {row.Id}");
        }

        public static IPendingChange BuildDeleteTier(int id)
        {
            return new RawSqlChange(
                $"season_tiers: delete id {id}",
                $"DELETE FROM season_tiers WHERE id = {id}",
                isDestructive: true);
        }

        // ------------------- leaderboard rewards -------------------

        public static IPendingChange BuildInsertLeaderboardReward(SeasonLeaderboardRewardRow row)
        {
            return new RawSqlChange(
                $"season_leaderboard_rewards: insert season {row.SeasonId} rank {row.RankMin}-{row.RankMax}",
                "INSERT INTO season_leaderboard_rewards (season_id, rank_min, rank_max, package_id) " +
                "VALUES (" +
                $"{row.SeasonId}, " +
                $"{row.RankMin}, " +
                $"{row.RankMax}, " +
                $"{row.PackageId})");
        }

        public static IPendingChange BuildUpdateLeaderboardReward(SeasonLeaderboardRewardRow row)
        {
            return new RawSqlChange(
                $"season_leaderboard_rewards: update id {row.Id} (rank {row.RankMin}-{row.RankMax})",
                "UPDATE season_leaderboard_rewards SET " +
                $"rank_min = {row.RankMin}, " +
                $"rank_max = {row.RankMax}, " +
                $"package_id = {row.PackageId} " +
                $"WHERE id = {row.Id}");
        }

        public static IPendingChange BuildDeleteLeaderboardReward(int id)
        {
            return new RawSqlChange(
                $"season_leaderboard_rewards: delete id {id}",
                $"DELETE FROM season_leaderboard_rewards WHERE id = {id}",
                isDestructive: true);
        }
    }
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 7: PackageChanges

**Goal:** Build `IPendingChange` instances for all package and package-item mutations.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Packages\PackageChanges.cs`:

```csharp
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.Packages
{
    public static class PackageChanges
    {
        public static IPendingChange BuildInsertPackage(string name)
        {
            return new RawSqlChange(
                $"packages: insert '{name}'",
                $"INSERT INTO packages (name) VALUES ({SqlLiteral.Of(name)})");
        }

        public static IPendingChange BuildUpdatePackage(int id, string name)
        {
            return new RawSqlChange(
                $"packages: update id {id} (name '{name}')",
                $"UPDATE packages SET name = {SqlLiteral.Of(name)} WHERE id = {id}");
        }

        public static IPendingChange BuildDeletePackage(int id)
        {
            return new RawSqlChange(
                $"packages: delete id {id}",
                $"DELETE FROM packages WHERE id = {id}",
                isDestructive: true);
        }

        public static IPendingChange BuildInsertPackageItem(int packageId, int definition, int quantity)
        {
            return new RawSqlChange(
                $"packageitems: insert package {packageId} def {definition} qty {quantity}",
                "INSERT INTO packageitems (packageid, definition, quantity) " +
                $"VALUES ({packageId}, {definition}, {quantity})");
        }

        public static IPendingChange BuildDeletePackageItem(int id)
        {
            return new RawSqlChange(
                $"packageitems: delete id {id}",
                $"DELETE FROM packageitems WHERE id = {id}",
                isDestructive: true);
        }
    }
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 8: PackagesViewModel

**Goal:** Master-detail VM for the Packages view. Holds package list, currently selected package, its items, and pre-filtered picker list. Queues all mutations to `ChangeQueue`.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\ViewModels\PackagesViewModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Seasons;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class PackagesViewModel : ObservableObject
    {
        private readonly PackageRepository _repo;
        private readonly SeasonRepository _seasonRepo;
        private readonly ChangeQueue _queue;
        private readonly LookupCache _lookups;
        private readonly ConnectionSettings _connection;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;

        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private PackageRow? _selectedPackage;

        public ObservableCollection<PackageRow> Packages { get; } = new();
        public ObservableCollection<PackageRow> FilteredPackages { get; } = new();
        public ObservableCollection<PackageItemRow> SelectedPackageItems { get; } = new();
        public ObservableCollection<PackageUsageRow> SelectedPackageUsage { get; } = new();

        // Pre-filtered picker list rebuilt from LookupCache on load.
        public ObservableCollection<PackageItemPickItem> PickItems { get; } = new();
        private Dictionary<int, string> _pickNamesByDefinition = new();

        public bool HasSelection => SelectedPackage != null;
        public bool IsActiveSeason => SelectedPackageUsage.Any(u => u.IsActive);
        public bool CanDeleteSelected => SelectedPackage != null && SelectedPackage.SeasonCount == 0;

        public string UsageDescription
        {
            get
            {
                if (SelectedPackage == null) return "";
                if (SelectedPackageUsage.Count == 0) return "Not used by any season.";
                var lines = SelectedPackageUsage
                    .Select(u => $"• {u.SeasonName} — {u.Context}: {u.Detail}")
                    .ToList();
                return $"Used by {SelectedPackageUsage.Count} reference(s):\n" + string.Join("\n", lines);
            }
        }

        public PackagesViewModel(
            PackageRepository repo,
            SeasonRepository seasonRepo,
            ChangeQueue queue,
            LookupCache lookups,
            ConnectionSettings connection)
        {
            _repo = repo;
            _seasonRepo = seasonRepo;
            _queue = queue;
            _lookups = lookups;
            _connection = connection;
        }

        partial void OnFilterTextChanged(string value) => RefreshFilter();

        partial void OnSelectedPackageChanged(PackageRow? value)
        {
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanDeleteSelected));
            _ = LoadSelectedDetailAsync();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading packages...";
            StatusIsError = false;
            try
            {
                var pkgs = await _repo.LoadAllPackagesAsync();
                Packages.Clear();
                foreach (var p in pkgs) Packages.Add(p);

                RebuildPickItems();
                RefreshFilter();

                if (SelectedPackage != null)
                {
                    var match = Packages.FirstOrDefault(p => p.Id == SelectedPackage.Id);
                    SelectedPackage = match;
                }
                else
                {
                    SelectedPackageItems.Clear();
                    SelectedPackageUsage.Clear();
                    OnPropertyChanged(nameof(UsageDescription));
                    OnPropertyChanged(nameof(IsActiveSeason));
                }

                StatusMessage = $"Loaded {Packages.Count} package(s).";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void RebuildPickItems()
        {
            var fresh = PackageItemPickItem.BuildFilteredList(_lookups.Entities);
            PickItems.Clear();
            foreach (var p in fresh) PickItems.Add(p);
            _pickNamesByDefinition = fresh.ToDictionary(p => p.Definition, p => p.DisplayName);
        }

        private void RefreshFilter()
        {
            FilteredPackages.Clear();
            var f = (FilterText ?? "").Trim();
            foreach (var p in Packages)
            {
                if (f.Length == 0 || p.Name.Contains(f, StringComparison.OrdinalIgnoreCase))
                    FilteredPackages.Add(p);
            }
        }

        private async Task LoadSelectedDetailAsync()
        {
            SelectedPackageItems.Clear();
            SelectedPackageUsage.Clear();

            if (SelectedPackage == null || SelectedPackage.Id <= 0)
            {
                OnPropertyChanged(nameof(UsageDescription));
                OnPropertyChanged(nameof(IsActiveSeason));
                return;
            }

            try
            {
                var items = await _repo.LoadPackageItemsAsync(SelectedPackage.Id);
                foreach (var it in items)
                {
                    if (_pickNamesByDefinition.TryGetValue(it.Definition, out var name))
                        it.DisplayName = name;
                    else if (_lookups.EntityNamesByDefinition.TryGetValue(it.Definition, out var fallback))
                        it.DisplayName = fallback;
                    else
                        it.DisplayName = $"(def {it.Definition})";
                    SelectedPackageItems.Add(it);
                }

                var usage = await _repo.LoadSeasonUsageAsync(SelectedPackage.Id);
                foreach (var u in usage) SelectedPackageUsage.Add(u);
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Detail load failed: {ex.Message}";
            }

            OnPropertyChanged(nameof(UsageDescription));
            OnPropertyChanged(nameof(IsActiveSeason));
        }

        [RelayCommand]
        private void NewPackage()
        {
            var name = Microsoft.VisualBasic.Interaction.InputBox(
                "Name for the new package:", "New Package", "New Package");
            if (string.IsNullOrWhiteSpace(name)) return;

            _queue.Add(PackageChanges.BuildInsertPackage(name));

            // Optimistic local add: id 0 until the next reload after commit.
            var row = new PackageRow { Id = 0, Name = name, IsNew = true, ItemCount = 0, SeasonCount = 0 };
            Packages.Add(row);
            RefreshFilter();
            SelectedPackage = row;
            StatusIsError = false;
            StatusMessage = $"Queued INSERT for package '{name}'. Items can be added after commit + reload.";
        }

        [RelayCommand]
        private void DeletePackage()
        {
            if (SelectedPackage == null) return;
            if (!CanDeleteSelected)
            {
                MessageBox.Show(
                    "Cannot delete a package that is referenced by any season. Remove its tier/leaderboard references first.",
                    "Package in use",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var ok = MessageBox.Show(
                $"Delete package '{SelectedPackage.Name}' (id {SelectedPackage.Id})? This is destructive.",
                "Delete package",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            if (SelectedPackage.Id > 0)
                _queue.Add(PackageChanges.BuildDeletePackage(SelectedPackage.Id));

            var row = SelectedPackage;
            SelectedPackage = null;
            Packages.Remove(row);
            RefreshFilter();
            StatusIsError = false;
            StatusMessage = $"Queued DELETE for package '{row.Name}'.";
        }

        [RelayCommand]
        private void AddItem()
        {
            if (SelectedPackage == null)
            {
                MessageBox.Show("Select a package first.", "No package",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (SelectedPackage.Id <= 0)
            {
                MessageBox.Show(
                    "This package is unsaved. Commit the queue, then reload Packages, then add items.",
                    "Package not yet saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (PickItems.Count == 0)
            {
                MessageBox.Show("Picker list is empty. Reload entities and try again.",
                    "No picks available",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Default: pick the first allowed item with quantity 1.
            var pick = PickItems[0];
            _queue.Add(PackageChanges.BuildInsertPackageItem(SelectedPackage.Id, pick.Definition, 1));

            var row = new PackageItemRow
            {
                Id = 0,
                PackageId = SelectedPackage.Id,
                Definition = pick.Definition,
                Quantity = 1,
                DisplayName = pick.DisplayName,
                IsNew = true
            };
            SelectedPackageItems.Add(row);
            SelectedPackage.ItemCount = SelectedPackage.ItemCount + 1;
            StatusIsError = false;
            StatusMessage = $"Queued INSERT for package item '{pick.DisplayName}' (x1). Adjust definition/quantity inline if needed.";
        }

        [RelayCommand]
        private void RemoveItem(PackageItemRow? row)
        {
            if (row == null || SelectedPackage == null) return;
            var ok = MessageBox.Show(
                $"Remove item '{row.DisplayName}' x{row.Quantity}?",
                "Remove item",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            if (row.Id > 0)
                _queue.Add(PackageChanges.BuildDeletePackageItem(row.Id));

            SelectedPackageItems.Remove(row);
            SelectedPackage.ItemCount = System.Math.Max(0, SelectedPackage.ItemCount - 1);
            StatusIsError = false;
            StatusMessage = row.Id > 0
                ? $"Queued DELETE for package item id {row.Id}."
                : "Removed unsaved item.";
        }
    }
}
```

> **Note on Microsoft.VisualBasic.Interaction.InputBox:** This is a built-in WPF-compatible simple input dialog. It is part of `Microsoft.VisualBasic.dll`, included with the .NET 8 Windows Desktop SDK; no extra package reference is needed.

- [ ] Run verification command. Build must succeed.

---

## Task 9: SeasonStatisticsViewModel

**Goal:** Read-only metrics view-model. Statistics load on tab activation; `RefreshCommand` reruns the queries.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\ViewModels\SeasonStatisticsViewModel.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Seasons;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class SeasonStatisticsViewModel : ObservableObject
    {
        private readonly SeasonRepository _repo;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;
        [ObservableProperty] private int _currentSeasonId;

        [ObservableProperty] private int _totalParticipants;
        [ObservableProperty] private int _activeLast7Days;
        [ObservableProperty] private string _retentionRate = "—";
        [ObservableProperty] private double _avgPointsPerDay;

        public ObservableCollection<TierDistributionRow> TierDistribution { get; } = new();
        public ObservableCollection<LeaderboardEntryRow> Top10 { get; } = new();
        public ObservableCollection<ObjectiveCompletionRow> ObjectiveCompletion { get; } = new();

        public SeasonStatisticsViewModel(SeasonRepository repo)
        {
            _repo = repo;
        }

        public async Task LoadAsync(int seasonId)
        {
            CurrentSeasonId = seasonId;
            if (seasonId <= 0)
            {
                ClearAll();
                StatusMessage = "(unsaved season — no statistics)";
                return;
            }

            IsLoading = true;
            StatusMessage = "Loading statistics...";
            StatusIsError = false;
            try
            {
                TotalParticipants = await _repo.LoadParticipantCountAsync(seasonId);
                ActiveLast7Days = await _repo.LoadActiveLast7DaysAsync(seasonId);
                RetentionRate = TotalParticipants > 0
                    ? $"{(ActiveLast7Days * 100.0 / TotalParticipants):F1}%"
                    : "—";
                AvgPointsPerDay = await _repo.LoadAvgPointsPerDayAsync(seasonId);

                TierDistribution.Clear();
                foreach (var t in await _repo.LoadTierDistributionAsync(seasonId))
                    TierDistribution.Add(t);

                Top10.Clear();
                foreach (var t in await _repo.LoadTop10LeaderboardAsync(seasonId))
                    Top10.Add(t);

                ObjectiveCompletion.Clear();
                foreach (var o in await _repo.LoadObjectiveCompletionAsync(seasonId))
                    ObjectiveCompletion.Add(o);

                StatusMessage = $"Statistics loaded at {DateTime.Now:HH:mm:ss}.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ClearAll()
        {
            TotalParticipants = 0;
            ActiveLast7Days = 0;
            RetentionRate = "—";
            AvgPointsPerDay = 0;
            TierDistribution.Clear();
            Top10.Clear();
            ObjectiveCompletion.Clear();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (CurrentSeasonId > 0) await LoadAsync(CurrentSeasonId);
        }
    }
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 10: SeasonDetailViewModel

**Goal:** ViewModel for the per-season detail view (7 tabs). Loads activity rates, objectives, tiers, leaderboard. Holds nested `PackagesVm` and `StatisticsVm`.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\ViewModels\SeasonDetailViewModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Seasons;
using Perpetuum.AdminTool.Settings;
using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class SeasonDetailViewModel : ObservableObject
    {
        private readonly SeasonRepository _repo;
        private readonly PackageRepository _pkgRepo;
        private readonly ChangeQueue _queue;
        private readonly LookupCache _cache;
        private readonly ConnectionSettings _connection;

        [ObservableProperty] private SeasonRow _season;
        [ObservableProperty] private int _selectedTabIndex;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;

        public ObservableCollection<SeasonActivityRateRow> ActivityRates { get; } = new();
        public ObservableCollection<SeasonObjectiveRow> Objectives { get; } = new();
        public ObservableCollection<SeasonTierRow> Tiers { get; } = new();
        public ObservableCollection<SeasonLeaderboardRewardRow> LeaderboardRewards { get; } = new();

        // Packages reachable across the whole tab — shared with the Packages view at the
        // SeasonsViewModel level. We just receive the same ObservableCollection here so
        // tier/leaderboard ComboBox bindings always see the live list.
        public ObservableCollection<PackageRow> Packages { get; }

        public PackagesViewModel PackagesVm { get; }
        public SeasonStatisticsViewModel StatisticsVm { get; }

        // Static dropdown sources for activity-type ComboBoxes inside DataGrids.
        public IReadOnlyList<ActivityTypeOption> ActivityTypeOptions { get; } =
            new[]
            {
                new ActivityTypeOption(SeasonActivityType.NpcKill,         "NPC Kill"),
                new ActivityTypeOption(SeasonActivityType.PvpKill,         "PvP Kill"),
                new ActivityTypeOption(SeasonActivityType.MissionComplete, "Mission Complete"),
                new ActivityTypeOption(SeasonActivityType.MineralMined,    "Mineral Mined"),
                new ActivityTypeOption(SeasonActivityType.EpSpent,         "EP Spent"),
                new ActivityTypeOption(SeasonActivityType.NicEarned,       "NIC Earned"),
                new ActivityTypeOption(SeasonActivityType.NicSpent,        "NIC Spent"),
                new ActivityTypeOption(SeasonActivityType.IntrusionPoint,  "Intrusion Point"),
            };

        public bool CanActivate => !Season.IsActive;
        public bool CanDeactivate => Season.IsActive;
        public string StatusBadge => Season.CardState switch
        {
            SeasonCardState.Active => "ACTIVE",
            SeasonCardState.Draft  => "DRAFT",
            SeasonCardState.Ended  => "ENDED",
            _ => ""
        };

        public SeasonDetailViewModel(
            SeasonRow season,
            SeasonRepository repo,
            PackageRepository pkgRepo,
            ChangeQueue queue,
            PackagesViewModel packagesVm,
            SeasonStatisticsViewModel statsVm,
            LookupCache cache,
            ConnectionSettings connection,
            ObservableCollection<PackageRow> packages)
        {
            _season = season;
            _repo = repo;
            _pkgRepo = pkgRepo;
            _queue = queue;
            _cache = cache;
            _connection = connection;
            PackagesVm = packagesVm;
            StatisticsVm = statsVm;
            Packages = packages;
        }

        public async Task LoadAsync()
        {
            try
            {
                StatusIsError = false;
                StatusMessage = "Loading season detail...";

                // Activity rates: always show all 8 types. Hydrate from DB rows where present.
                var dbRates = Season.Id > 0
                    ? await _repo.LoadActivityRatesAsync(Season.Id)
                    : new List<SeasonActivityRateRow>();
                var dbByType = dbRates.ToDictionary(r => r.ActivityType, r => r);

                ActivityRates.Clear();
                foreach (SeasonActivityType type in Enum.GetValues(typeof(SeasonActivityType)))
                {
                    if (dbByType.TryGetValue(type, out var existing))
                    {
                        ActivityRates.Add(existing);
                    }
                    else
                    {
                        ActivityRates.Add(new SeasonActivityRateRow
                        {
                            Id = 0,
                            SeasonId = Season.Id,
                            ActivityType = type,
                            PointsPerUnit = 0,
                            UnitScale = 1
                        });
                    }
                }

                Objectives.Clear();
                if (Season.Id > 0)
                    foreach (var o in await _repo.LoadObjectivesAsync(Season.Id))
                        Objectives.Add(o);

                Tiers.Clear();
                if (Season.Id > 0)
                    foreach (var t in await _repo.LoadTiersAsync(Season.Id))
                        Tiers.Add(t);

                LeaderboardRewards.Clear();
                if (Season.Id > 0)
                    foreach (var l in await _repo.LoadLeaderboardRewardsAsync(Season.Id))
                        LeaderboardRewards.Add(l);

                OnPropertyChanged(nameof(CanActivate));
                OnPropertyChanged(nameof(CanDeactivate));
                OnPropertyChanged(nameof(StatusBadge));
                StatusMessage = $"Loaded season '{Season.Name}'.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
        }

        partial void OnSelectedTabIndexChanged(int value)
        {
            // Statistics tab is index 6 (General=0, Activity=1, Objectives=2, Tiers=3,
            // Leaderboard=4, Packages=5, Statistics=6). Load on activation.
            if (value == 6 && Season.Id > 0)
                _ = StatisticsVm.LoadAsync(Season.Id);
        }

        [RelayCommand]
        private void Activate()
        {
            if (Season.Id <= 0)
            {
                MessageBox.Show("Season is unsaved. Commit the queue first.",
                    "Cannot activate", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var ok = MessageBox.Show(
                $"Activate season '{Season.Name}'? This queues an UPDATE seasons SET is_active = 1.",
                "Activate season", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ok != MessageBoxResult.Yes) return;

            _queue.Add(SeasonChanges.BuildActivate(Season.Id));
            Season.IsActive = true;
            OnPropertyChanged(nameof(CanActivate));
            OnPropertyChanged(nameof(CanDeactivate));
            OnPropertyChanged(nameof(StatusBadge));
            StatusIsError = false;
            StatusMessage = "Queued ACTIVATE. Use the main Commit button to apply.";
        }

        [RelayCommand]
        private void Deactivate()
        {
            if (Season.Id <= 0) return;
            var ok = MessageBox.Show(
                $"Deactivate season '{Season.Name}'?",
                "Deactivate season", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ok != MessageBoxResult.Yes) return;

            _queue.Add(SeasonChanges.BuildDeactivate(Season.Id));
            Season.IsActive = false;
            OnPropertyChanged(nameof(CanActivate));
            OnPropertyChanged(nameof(CanDeactivate));
            OnPropertyChanged(nameof(StatusBadge));
            StatusIsError = false;
            StatusMessage = "Queued DEACTIVATE.";
        }

        [RelayCommand]
        private void SaveGeneral()
        {
            if (Season.Id <= 0)
            {
                _queue.Add(SeasonChanges.BuildInsert(Season));
                StatusMessage = "Queued INSERT for new season. After commit + reload, edit detail tabs.";
            }
            else
            {
                _queue.Add(SeasonChanges.BuildUpdate(Season));
                StatusMessage = "Queued UPDATE for season general fields.";
            }
            StatusIsError = false;
        }

        [RelayCommand]
        private void QueueActivityRateSave(SeasonActivityRateRow? row)
        {
            if (row == null) return;
            if (Season.Id <= 0)
            {
                MessageBox.Show("Save the season (General tab) first.", "Season unsaved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            row.SeasonId = Season.Id;
            _queue.Add(SeasonChanges.BuildUpsertActivityRate(row));
            StatusIsError = false;
            StatusMessage = $"Queued upsert for activity '{row.ActivityType}'.";
        }

        [RelayCommand]
        private void AddObjective()
        {
            if (Season.Id <= 0)
            {
                MessageBox.Show("Save the season (General tab) first.", "Season unsaved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var row = new SeasonObjectiveRow
            {
                SeasonId = Season.Id,
                Name = "New Objective",
                Description = "",
                ActivityType = SeasonActivityType.NpcKill,
                TargetValue = 1,
                BonusPoints = 0,
                DisplayOrder = Objectives.Count,
                IsNew = true
            };
            Objectives.Add(row);
            _queue.Add(SeasonChanges.BuildInsertObjective(row));
            StatusIsError = false;
            StatusMessage = "Queued INSERT for objective.";
        }

        [RelayCommand]
        private void RemoveObjective(SeasonObjectiveRow? row)
        {
            if (row == null) return;
            var ok = MessageBox.Show($"Remove objective '{row.Name}'?",
                "Remove objective", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            if (row.Id > 0) _queue.Add(SeasonChanges.BuildDeleteObjective(row.Id));
            Objectives.Remove(row);
            StatusIsError = false;
            StatusMessage = row.Id > 0
                ? $"Queued DELETE for objective id {row.Id}."
                : "Removed unsaved objective.";
        }

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
                SeasonId = Season.Id,
                TierNumber = (Tiers.Count == 0 ? 1 : Tiers.Max(t => t.TierNumber) + 1),
                TierName = "New Tier",
                PointsRequired = 0,
                PackageId = Packages[0].Id,
                IsNew = true
            };
            Tiers.Add(row);
            _queue.Add(SeasonChanges.BuildInsertTier(row));
            StatusIsError = false;
            StatusMessage = "Queued INSERT for tier.";
        }

        [RelayCommand]
        private void RemoveTier(SeasonTierRow? row)
        {
            if (row == null) return;
            var ok = MessageBox.Show($"Remove tier '{row.TierName}'?",
                "Remove tier", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            if (row.Id > 0) _queue.Add(SeasonChanges.BuildDeleteTier(row.Id));
            Tiers.Remove(row);
            StatusIsError = false;
            StatusMessage = row.Id > 0
                ? $"Queued DELETE for tier id {row.Id}."
                : "Removed unsaved tier.";
        }

        [RelayCommand]
        private void AddLeaderboardReward()
        {
            if (Season.Id <= 0)
            {
                MessageBox.Show("Save the season (General tab) first.", "Season unsaved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (Packages.Count == 0)
            {
                MessageBox.Show("No packages exist. Create a package first.",
                    "No packages", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var nextMin = LeaderboardRewards.Count == 0 ? 1 : LeaderboardRewards.Max(r => r.RankMax) + 1;
            var row = new SeasonLeaderboardRewardRow
            {
                SeasonId = Season.Id,
                RankMin = nextMin,
                RankMax = nextMin,
                PackageId = Packages[0].Id,
                IsNew = true
            };
            LeaderboardRewards.Add(row);
            _queue.Add(SeasonChanges.BuildInsertLeaderboardReward(row));
            StatusIsError = false;
            StatusMessage = "Queued INSERT for leaderboard bracket.";
        }

        [RelayCommand]
        private void RemoveLeaderboardReward(SeasonLeaderboardRewardRow? row)
        {
            if (row == null) return;
            var ok = MessageBox.Show($"Remove rank bracket {row.RankMin}-{row.RankMax}?",
                "Remove bracket", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            if (row.Id > 0) _queue.Add(SeasonChanges.BuildDeleteLeaderboardReward(row.Id));
            LeaderboardRewards.Remove(row);
            StatusIsError = false;
            StatusMessage = row.Id > 0
                ? $"Queued DELETE for leaderboard bracket id {row.Id}."
                : "Removed unsaved bracket.";
        }
    }

    public record ActivityTypeOption(SeasonActivityType Value, string Label);
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 11: SeasonsViewModel

**Goal:** Top-level ViewModel for the tab. Holds card list, packages list (shared), detail-view drill-down state, and the switcher between Seasons / Packages views.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\ViewModels\SeasonsViewModel.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Seasons;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Views;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class SeasonsViewModel : ObservableObject
    {
        private readonly SeasonRepository _seasonRepo;
        private readonly PackageRepository _pkgRepo;
        private readonly ChangeQueue _queue;
        private readonly LookupCache _lookups;
        private readonly ConnectionSettings _connection;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;

        [ObservableProperty] private bool _showPackages;
        [ObservableProperty] private bool _isInDetail;
        [ObservableProperty] private SeasonDetailViewModel? _detailViewModel;

        public ObservableCollection<SeasonRow> Seasons { get; } = new();
        public PackagesViewModel PackagesVm { get; }

        public bool ShowSeasonsList => !ShowPackages && !IsInDetail;
        public bool ShowPackagesView => ShowPackages && !IsInDetail;

        public SeasonsViewModel(
            SeasonRepository seasonRepo,
            PackageRepository pkgRepo,
            ChangeQueue queue,
            LookupCache lookups,
            ConnectionSettings connection)
        {
            _seasonRepo = seasonRepo;
            _pkgRepo = pkgRepo;
            _queue = queue;
            _lookups = lookups;
            _connection = connection;
            PackagesVm = new PackagesViewModel(_pkgRepo, _seasonRepo, _queue, _lookups, _connection);
        }

        partial void OnShowPackagesChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowSeasonsList));
            OnPropertyChanged(nameof(ShowPackagesView));
        }

        partial void OnIsInDetailChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowSeasonsList));
            OnPropertyChanged(nameof(ShowPackagesView));
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading seasons...";
            StatusIsError = false;
            try
            {
                var rows = await _seasonRepo.LoadAllSeasonsAsync();
                Seasons.Clear();
                foreach (var r in rows) Seasons.Add(r);

                await PackagesVm.LoadAsync();

                StatusMessage = $"Loaded {Seasons.Count} season(s).";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ShowSeasons()
        {
            IsInDetail = false;
            ShowPackages = false;
        }

        [RelayCommand]
        private void ShowPackagesPanel()
        {
            IsInDetail = false;
            ShowPackages = true;
        }

        [RelayCommand]
        private void BackToList()
        {
            IsInDetail = false;
            DetailViewModel = null;
        }

        [RelayCommand]
        private void NavigateToSeason(SeasonRow? row)
        {
            if (row == null) return;
            var statsVm = new SeasonStatisticsViewModel(_seasonRepo);
            var detail = new SeasonDetailViewModel(
                row, _seasonRepo, _pkgRepo, _queue,
                PackagesVm, statsVm,
                _lookups, _connection, PackagesVm.Packages);
            DetailViewModel = detail;
            IsInDetail = true;
            _ = detail.LoadAsync();
        }

        [RelayCommand]
        private void NewSeason()
        {
            var wizardVm = new SeasonWizardViewModel(_queue, PackagesVm.Packages, () =>
            {
                StatusIsError = false;
                StatusMessage = "Wizard queued INSERT statements for new season.";
            });
            var win = new SeasonWizardWindow(wizardVm)
            {
                Owner = Application.Current?.MainWindow
            };
            win.ShowDialog();
        }
    }
}
```

- [ ] Run verification command. Build must succeed. (Note: this references `SeasonWizardViewModel` and `SeasonWizardWindow` from Tasks 12 and 16. The build will fail here if those files don't yet exist — defer running the build until Tasks 12 and 16 are also written. To unblock incremental verification, you may temporarily stub `NewSeason` to do nothing, then restore it once the wizard exists.)

> **Recommended order to keep builds green:** Skip the `NewSeason` command body initially (replace its body with `{ /* wired in Task 16 */ }`), and after Tasks 12 and 16 are done, restore the body shown above.

---

## Task 12: SeasonWizardViewModel

**Goal:** 6-step wizard ViewModel. Collects all season config, then queues a SeasonChanges.BuildInsert + objective/tier/leaderboard/rate inserts. Note: child inserts assume the season identity hasn't been resolved yet — the spec accepts this limitation by queuing all inserts; users commit the queue then reload and revisit details.

Actually, the wizard cannot queue child INSERTs that reference the new season's id (it doesn't exist until commit). To handle this cleanly, the wizard queues only the season INSERT; the user is instructed to commit, reload, and finish configuring via the detail tabs. The wizard's review step lists what it will queue and what must be done after commit.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\ViewModels\SeasonWizardViewModel.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Seasons;
using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class SeasonWizardViewModel : ObservableObject
    {
        private readonly ChangeQueue _queue;
        private readonly ObservableCollection<PackageRow> _packages;
        private readonly Action _onComplete;

        // Step 1 of 6: Season Info
        // Step 2 of 6: Activity Rates
        // Step 3 of 6: Objectives
        // Step 4 of 6: Tiers
        // Step 5 of 6: Leaderboard Rewards
        // Step 6 of 6: Review
        [ObservableProperty] private int _currentStep = 1;

        public bool IsStep1 => CurrentStep == 1;
        public bool IsStep2 => CurrentStep == 2;
        public bool IsStep3 => CurrentStep == 3;
        public bool IsStep4 => CurrentStep == 4;
        public bool IsStep5 => CurrentStep == 5;
        public bool IsReviewStep => CurrentStep == 6;

        public bool CanGoBack => CurrentStep > 1;
        public bool CanGoNext => CurrentStep < 6;

        public string StepTitle => CurrentStep switch
        {
            1 => "Step 1 of 6 — Season Info",
            2 => "Step 2 of 6 — Activity Rates",
            3 => "Step 3 of 6 — Objectives (optional)",
            4 => "Step 4 of 6 — Tiers (optional)",
            5 => "Step 5 of 6 — Leaderboard Rewards (optional)",
            6 => "Step 6 of 6 — Review",
            _ => ""
        };

        // Step 1 fields
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _description = "";
        [ObservableProperty] private DateTime _startTime = DateTime.UtcNow.Date;
        [ObservableProperty] private DateTime _endTime = DateTime.UtcNow.Date.AddDays(30);

        // Step 2 — 8 pre-populated rows
        public ObservableCollection<SeasonActivityRateRow> ActivityRates { get; } = new();
        // Step 3
        public ObservableCollection<SeasonObjectiveRow> Objectives { get; } = new();
        // Step 4
        public ObservableCollection<SeasonTierRow> Tiers { get; } = new();
        // Step 5
        public ObservableCollection<SeasonLeaderboardRewardRow> LeaderboardRewards { get; } = new();

        public ObservableCollection<PackageRow> Packages => _packages;
        public bool HasPackages => _packages.Count > 0;

        public string Step1Validation { get; private set; } = "";
        public string FinishHint => Tiers.Count > 0 || LeaderboardRewards.Count > 0 || Objectives.Count > 0
            ? "After committing the season INSERT, reopen the season detail to add objectives, tiers, and leaderboard rewards (they need the assigned season id)."
            : "Click 'Add to Change Queue' to queue the INSERT for the new season.";

        public SeasonWizardViewModel(
            ChangeQueue queue,
            ObservableCollection<PackageRow> packages,
            Action onComplete)
        {
            _queue = queue;
            _packages = packages;
            _onComplete = onComplete;

            // Pre-populate all 8 activity types with disabled defaults.
            foreach (SeasonActivityType type in Enum.GetValues(typeof(SeasonActivityType)))
            {
                ActivityRates.Add(new SeasonActivityRateRow
                {
                    Id = 0,
                    SeasonId = 0,
                    ActivityType = type,
                    PointsPerUnit = 0,
                    UnitScale = 1
                });
            }
        }

        partial void OnCurrentStepChanged(int value)
        {
            OnPropertyChanged(nameof(IsStep1));
            OnPropertyChanged(nameof(IsStep2));
            OnPropertyChanged(nameof(IsStep3));
            OnPropertyChanged(nameof(IsStep4));
            OnPropertyChanged(nameof(IsStep5));
            OnPropertyChanged(nameof(IsReviewStep));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(StepTitle));
            OnPropertyChanged(nameof(FinishHint));
        }

        private bool ValidateCurrentStep()
        {
            Step1Validation = "";
            OnPropertyChanged(nameof(Step1Validation));

            if (CurrentStep == 1)
            {
                if (string.IsNullOrWhiteSpace(Name))
                {
                    Step1Validation = "Name is required.";
                    OnPropertyChanged(nameof(Step1Validation));
                    return false;
                }
                if (EndTime <= StartTime)
                {
                    Step1Validation = "End time must be after start time.";
                    OnPropertyChanged(nameof(Step1Validation));
                    return false;
                }
            }
            return true;
        }

        [RelayCommand]
        private void Back()
        {
            if (CurrentStep > 1) CurrentStep--;
        }

        [RelayCommand]
        private void Next()
        {
            if (!ValidateCurrentStep()) return;
            if (CurrentStep < 6) CurrentStep++;
        }

        [RelayCommand]
        private void AddObjectiveRow()
        {
            Objectives.Add(new SeasonObjectiveRow
            {
                SeasonId = 0,
                Name = "New Objective",
                ActivityType = SeasonActivityType.NpcKill,
                TargetValue = 1,
                BonusPoints = 0,
                DisplayOrder = Objectives.Count,
                IsNew = true
            });
        }

        [RelayCommand]
        private void RemoveObjectiveRow(SeasonObjectiveRow? row)
        {
            if (row != null) Objectives.Remove(row);
        }

        [RelayCommand]
        private void AddTierRow()
        {
            if (Packages.Count == 0) return;
            Tiers.Add(new SeasonTierRow
            {
                SeasonId = 0,
                TierNumber = Tiers.Count == 0 ? 1 : Tiers.Max(t => t.TierNumber) + 1,
                TierName = "New Tier",
                PointsRequired = 0,
                PackageId = Packages[0].Id,
                IsNew = true
            });
        }

        [RelayCommand]
        private void RemoveTierRow(SeasonTierRow? row)
        {
            if (row != null) Tiers.Remove(row);
        }

        [RelayCommand]
        private void AddLeaderboardRow()
        {
            if (Packages.Count == 0) return;
            var nextMin = LeaderboardRewards.Count == 0 ? 1 : LeaderboardRewards.Max(r => r.RankMax) + 1;
            LeaderboardRewards.Add(new SeasonLeaderboardRewardRow
            {
                SeasonId = 0,
                RankMin = nextMin,
                RankMax = nextMin,
                PackageId = Packages[0].Id,
                IsNew = true
            });
        }

        [RelayCommand]
        private void RemoveLeaderboardRow(SeasonLeaderboardRewardRow? row)
        {
            if (row != null) LeaderboardRewards.Remove(row);
        }

        [RelayCommand]
        private void Finish()
        {
            // Queue ONLY the season INSERT. Children require the new season id which
            // is not known until commit; we surface this clearly via FinishHint and ask
            // the admin to commit + reload + finish configuration via the detail tabs.
            var seed = new SeasonSnapshot
            {
                Id = 0,
                Name = Name,
                Description = Description ?? "",
                StartTime = StartTime,
                EndTime = EndTime,
                IsActive = false
            };
            var row = SeasonRow.CreateNew(seed);
            _queue.Add(SeasonChanges.BuildInsert(row));

            _onComplete?.Invoke();
        }
    }
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 13: PackagesView (XAML)

**Goal:** Master-detail UI for packages.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Views\PackagesView.xaml`:

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.PackagesView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:common="clr-namespace:Perpetuum.AdminTool.Common"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:PackagesViewModel}">
    <UserControl.Resources>
        <common:BindingProxy x:Key="VmProxy" Data="{Binding}"/>
    </UserControl.Resources>

    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="280"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- Left: Package list -->
        <DockPanel Grid.Column="0" Background="#FAFAFA">
            <Border DockPanel.Dock="Top" Background="#F2F2F2" Padding="8" BorderBrush="#DDD" BorderThickness="0,0,0,1">
                <StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                        <Button Content="+ New Package" Padding="8,2" Command="{Binding NewPackageCommand}"/>
                        <Button Content="Reload" Margin="6,0,0,0" Padding="8,2" Click="OnReloadClick"/>
                    </StackPanel>
                    <TextBox Margin="0,4,0,0"
                             Text="{Binding FilterText, UpdateSourceTrigger=PropertyChanged, Delay=150}"
                             ToolTip="Filter by name"/>
                </StackPanel>
            </Border>

            <ListBox ItemsSource="{Binding FilteredPackages}"
                     SelectedItem="{Binding SelectedPackage}"
                     HorizontalContentAlignment="Stretch"
                     BorderThickness="0">
                <ListBox.ItemTemplate>
                    <DataTemplate>
                        <Border Padding="6,4">
                            <Border.Style>
                                <Style TargetType="Border">
                                    <Setter Property="Opacity" Value="1"/>
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding IsUnused}" Value="True">
                                            <Setter Property="Opacity" Value="0.55"/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </Border.Style>
                            <StackPanel>
                                <TextBlock Text="{Binding Name}" FontWeight="Bold"/>
                                <TextBlock Text="{Binding SubtitleText}" Foreground="DimGray" FontSize="11"/>
                            </StackPanel>
                        </Border>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </DockPanel>

        <!-- Right: Package detail -->
        <DockPanel Grid.Column="1" Margin="8">
            <Border DockPanel.Dock="Top" BorderBrush="#DDD" BorderThickness="0,0,0,1" Padding="0,0,0,8">
                <StackPanel>
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="Package name:" VerticalAlignment="Center" Margin="0,0,8,0"/>
                        <TextBox Width="240" Text="{Binding SelectedPackage.Name, UpdateSourceTrigger=LostFocus}"
                                 IsEnabled="{Binding HasSelection}"/>
                        <Button Content="Delete Package"
                                Margin="16,0,0,0" Padding="10,2" Foreground="DarkRed"
                                Command="{Binding DeletePackageCommand}"
                                IsEnabled="{Binding CanDeleteSelected}"
                                ToolTip="Deletes the package row. Disabled while the package is referenced by any season."/>
                    </StackPanel>
                    <TextBlock Margin="0,8,0,0" TextWrapping="Wrap"
                               Text="{Binding UsageDescription}"
                               Foreground="DimGray"
                               Visibility="{Binding HasSelection, Converter={x:Static common:InverseBoolConverter.Instance}, ConverterParameter=invert}"/>
                </StackPanel>
            </Border>

            <Border DockPanel.Dock="Top" Background="#FFF8E1" BorderBrush="#FFB300"
                    BorderThickness="1" Padding="8" Margin="0,8,0,0"
                    Visibility="{Binding IsActiveSeason, Converter={StaticResource BoolToVisibilityHidden}, FallbackValue=Collapsed}">
                <TextBlock TextWrapping="Wrap"
                           Text="Warning: this package is referenced by an active season. Changes will affect players who have not yet claimed this reward."/>
            </Border>

            <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,8,0,0">
                <Button Content="+ Add Item" Padding="8,2"
                        Command="{Binding AddItemCommand}"
                        IsEnabled="{Binding HasSelection}"/>
                <TextBlock Margin="16,0,0,0" VerticalAlignment="Center" Foreground="DimGray"
                           Text="{Binding StatusMessage}">
                    <TextBlock.Style>
                        <Style TargetType="TextBlock">
                            <Setter Property="Foreground" Value="DimGray"/>
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding StatusIsError}" Value="True">
                                    <Setter Property="Foreground" Value="DarkRed"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </TextBlock.Style>
                </TextBlock>
            </StackPanel>

            <DataGrid x:Name="ItemsGrid" Margin="0,8,0,0"
                      ItemsSource="{Binding SelectedPackageItems}"
                      AutoGenerateColumns="False"
                      CanUserAddRows="False"
                      CanUserDeleteRows="False"
                      HeadersVisibility="Column"
                      GridLinesVisibility="All"
                      SelectionMode="Single" SelectionUnit="FullRow">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="Id" Binding="{Binding Id}" Width="60" IsReadOnly="True"/>
                    <DataGridTextColumn Header="Definition" Binding="{Binding Definition}" Width="100"/>
                    <DataGridTemplateColumn Header="Display name" Width="*">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <TextBlock Margin="4,0" VerticalAlignment="Center"
                                           Text="{Binding DisplayName}"/>
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                        <DataGridTemplateColumn.CellEditingTemplate>
                            <DataTemplate>
                                <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.PickItems}"
                                          DisplayMemberPath="Display"
                                          SelectedValuePath="Definition"
                                          IsEditable="True"
                                          IsTextSearchEnabled="True"
                                          TextSearch.TextPath="Display"
                                          SelectedValue="{Binding Definition, UpdateSourceTrigger=PropertyChanged}"/>
                            </DataTemplate>
                        </DataGridTemplateColumn.CellEditingTemplate>
                    </DataGridTemplateColumn>
                    <DataGridTextColumn Header="Quantity" Binding="{Binding Quantity, UpdateSourceTrigger=LostFocus}" Width="80"/>
                    <DataGridTemplateColumn Header="" Width="80">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <Button Content="Remove" Padding="6,1" Foreground="DarkRed"
                                        Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveItemCommand}"
                                        CommandParameter="{Binding}"/>
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>
                </DataGrid.Columns>
            </DataGrid>
        </DockPanel>
    </Grid>
</UserControl>
```

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Views\PackagesView.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class PackagesView : UserControl
    {
        public PackagesView()
        {
            InitializeComponent();
        }

        private PackagesViewModel? Vm => DataContext as PackagesViewModel;

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await Vm.LoadAsync();
        }
    }
}
```

> **App-level resources to add:** The `BoolToVisibilityHidden` static resource referenced in the XAML must exist application-wide. If it doesn't yet, define it in `App.xaml`:
>
> ```xml
> <Application.Resources>
>     <BooleanToVisibilityConverter x:Key="BoolToVisibilityHidden"/>
> </Application.Resources>
> ```
>
> Check `App.xaml` first; only add if missing.

- [ ] Run verification command. Build must succeed.

---

## Task 14: SeasonDetailView (XAML)

**Goal:** Tabbed detail UI for one season.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Views\SeasonDetailView.xaml`:

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.SeasonDetailView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:common="clr-namespace:Perpetuum.AdminTool.Common"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:views="clr-namespace:Perpetuum.AdminTool.Views"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:SeasonDetailViewModel}">
    <UserControl.Resources>
        <common:BindingProxy x:Key="VmProxy" Data="{Binding}"/>
    </UserControl.Resources>
    <DockPanel>
        <!-- Header with back arrow, name, status, activate/deactivate -->
        <Border DockPanel.Dock="Top" Background="#F2F2F2" Padding="8" BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <DockPanel>
                <Button DockPanel.Dock="Left" Content="&#x2190; All Seasons" Padding="8,2"
                        Click="OnBackClick" Margin="0,0,12,0"/>
                <Button DockPanel.Dock="Right" Content="Deactivate" Padding="10,2" Margin="6,0,0,0"
                        Foreground="DarkRed"
                        Command="{Binding DeactivateCommand}"
                        IsEnabled="{Binding CanDeactivate}"/>
                <Button DockPanel.Dock="Right" Content="Activate" Padding="10,2"
                        Command="{Binding ActivateCommand}"
                        IsEnabled="{Binding CanActivate}"/>
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                    <TextBlock Text="{Binding Season.Name}" FontWeight="Bold" FontSize="14" VerticalAlignment="Center"/>
                    <Border Margin="12,0,0,0" Padding="6,1" Background="#1E88E5" CornerRadius="3"
                            VerticalAlignment="Center">
                        <TextBlock Text="{Binding StatusBadge}" Foreground="White" FontSize="11" FontWeight="Bold"/>
                    </Border>
                </StackPanel>
            </DockPanel>
        </Border>

        <Border DockPanel.Dock="Bottom" Background="#FAFAFA" BorderBrush="#DDD" BorderThickness="0,1,0,0" Padding="8,4">
            <TextBlock Text="{Binding StatusMessage}">
                <TextBlock.Style>
                    <Style TargetType="TextBlock">
                        <Setter Property="Foreground" Value="DimGray"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding StatusIsError}" Value="True">
                                <Setter Property="Foreground" Value="DarkRed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBlock.Style>
            </TextBlock>
        </Border>

        <TabControl SelectedIndex="{Binding SelectedTabIndex}">
            <!-- 0: General -->
            <TabItem Header="General">
                <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="8">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="180"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>

                        <TextBlock Grid.Row="0" Grid.Column="0" Text="Season ID:" Margin="0,4"/>
                        <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding Season.Id}" Margin="0,4" Foreground="DimGray"/>

                        <TextBlock Grid.Row="1" Grid.Column="0" Text="Name:" Margin="0,4"/>
                        <TextBox Grid.Row="1" Grid.Column="1" Text="{Binding Season.Name, UpdateSourceTrigger=LostFocus}" Margin="0,4"/>

                        <TextBlock Grid.Row="2" Grid.Column="0" Text="Description:" Margin="0,4"/>
                        <TextBox Grid.Row="2" Grid.Column="1" Text="{Binding Season.Description, UpdateSourceTrigger=LostFocus}"
                                 Margin="0,4" TextWrapping="Wrap" AcceptsReturn="True" MinHeight="60"/>

                        <TextBlock Grid.Row="3" Grid.Column="0" Text="Start time (UTC):" Margin="0,4"/>
                        <DatePicker Grid.Row="3" Grid.Column="1" SelectedDate="{Binding Season.StartTime}" Margin="0,4"/>

                        <TextBlock Grid.Row="4" Grid.Column="0" Text="End time (UTC):" Margin="0,4"/>
                        <DatePicker Grid.Row="4" Grid.Column="1" SelectedDate="{Binding Season.EndTime}" Margin="0,4"/>

                        <StackPanel Grid.Row="5" Grid.Column="1" Orientation="Horizontal" Margin="0,12,0,0">
                            <Button Content="Save General" Padding="14,2" FontWeight="Bold"
                                    Command="{Binding SaveGeneralCommand}"/>
                        </StackPanel>
                    </Grid>
                </ScrollViewer>
            </TabItem>

            <!-- 1: Activity Rates -->
            <TabItem Header="Activity Rates">
                <DockPanel>
                    <TextBlock DockPanel.Dock="Top" Margin="8" Foreground="DimGray" TextWrapping="Wrap"
                               Text="The 8 activity types are fixed. Set Points per Unit to 0 to disable a type for this season. Click Queue Save on the row to upsert."/>
                    <DataGrid ItemsSource="{Binding ActivityRates}"
                              AutoGenerateColumns="False" CanUserAddRows="False" CanUserDeleteRows="False"
                              HeadersVisibility="Column" GridLinesVisibility="All"
                              SelectionMode="Single" SelectionUnit="FullRow" Margin="8">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Activity Type" Binding="{Binding ActivityTypeLabel}" Width="180" IsReadOnly="True"/>
                            <DataGridTextColumn Header="Points per Unit" Binding="{Binding PointsPerUnit, UpdateSourceTrigger=LostFocus}" Width="120"/>
                            <DataGridTextColumn Header="Scale" Binding="{Binding UnitScale, UpdateSourceTrigger=LostFocus}" Width="100"/>
                            <DataGridTextColumn Header="Effective Rate" Binding="{Binding EffectiveRate}" Width="*" IsReadOnly="True"/>
                            <DataGridTemplateColumn Header="" Width="120">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="Queue Save" Padding="6,1"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.QueueActivityRateSaveCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>
                </DockPanel>
            </TabItem>

            <!-- 2: Objectives -->
            <TabItem Header="Objectives">
                <DockPanel>
                    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="8">
                        <Button Content="+ Add Objective" Padding="8,2" Command="{Binding AddObjectiveCommand}"/>
                    </StackPanel>
                    <DataGrid ItemsSource="{Binding Objectives}"
                              AutoGenerateColumns="False" CanUserAddRows="False" CanUserDeleteRows="False"
                              HeadersVisibility="Column" GridLinesVisibility="All"
                              SelectionMode="Single" SelectionUnit="FullRow" Margin="8">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Id" Binding="{Binding Id}" Width="60" IsReadOnly="True"/>
                            <DataGridTextColumn Header="Name" Binding="{Binding Name, UpdateSourceTrigger=LostFocus}" Width="200"/>
                            <DataGridTextColumn Header="Description" Binding="{Binding Description, UpdateSourceTrigger=LostFocus}" Width="*"/>
                            <DataGridTemplateColumn Header="Activity Type" Width="180">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock Margin="4,0" VerticalAlignment="Center" Text="{Binding ActivityType}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                                <DataGridTemplateColumn.CellEditingTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.ActivityTypeOptions}"
                                                  DisplayMemberPath="Label"
                                                  SelectedValuePath="Value"
                                                  SelectedValue="{Binding ActivityType, UpdateSourceTrigger=PropertyChanged}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellEditingTemplate>
                            </DataGridTemplateColumn>
                            <DataGridTextColumn Header="Target" Binding="{Binding TargetValue, UpdateSourceTrigger=LostFocus}" Width="100"/>
                            <DataGridTextColumn Header="Bonus Pts" Binding="{Binding BonusPoints, UpdateSourceTrigger=LostFocus}" Width="100"/>
                            <DataGridTextColumn Header="Order" Binding="{Binding DisplayOrder, UpdateSourceTrigger=LostFocus}" Width="80"/>
                            <DataGridTemplateColumn Header="" Width="80">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="Remove" Padding="6,1" Foreground="DarkRed"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveObjectiveCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>
                </DockPanel>
            </TabItem>

            <!-- 3: Tiers -->
            <TabItem Header="Tiers">
                <DockPanel>
                    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="8">
                        <Button Content="+ Add Tier" Padding="8,2" Command="{Binding AddTierCommand}"/>
                    </StackPanel>
                    <DataGrid ItemsSource="{Binding Tiers}"
                              AutoGenerateColumns="False" CanUserAddRows="False" CanUserDeleteRows="False"
                              HeadersVisibility="Column" GridLinesVisibility="All"
                              SelectionMode="Single" SelectionUnit="FullRow" Margin="8">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Id" Binding="{Binding Id}" Width="60" IsReadOnly="True"/>
                            <DataGridTextColumn Header="Tier #" Binding="{Binding TierNumber, UpdateSourceTrigger=LostFocus}" Width="80"/>
                            <DataGridTextColumn Header="Name" Binding="{Binding TierName, UpdateSourceTrigger=LostFocus}" Width="200"/>
                            <DataGridTextColumn Header="Points Required" Binding="{Binding PointsRequired, UpdateSourceTrigger=LostFocus}" Width="140"/>
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
                            <DataGridTemplateColumn Header="" Width="80">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="Remove" Padding="6,1" Foreground="DarkRed"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveTierCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>
                </DockPanel>
            </TabItem>

            <!-- 4: Leaderboard -->
            <TabItem Header="Leaderboard">
                <DockPanel>
                    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="8">
                        <Button Content="+ Add Bracket" Padding="8,2" Command="{Binding AddLeaderboardRewardCommand}"/>
                    </StackPanel>
                    <DataGrid ItemsSource="{Binding LeaderboardRewards}"
                              AutoGenerateColumns="False" CanUserAddRows="False" CanUserDeleteRows="False"
                              HeadersVisibility="Column" GridLinesVisibility="All"
                              SelectionMode="Single" SelectionUnit="FullRow" Margin="8">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Id" Binding="{Binding Id}" Width="60" IsReadOnly="True"/>
                            <DataGridTextColumn Header="Rank Min" Binding="{Binding RankMin, UpdateSourceTrigger=LostFocus}" Width="100"/>
                            <DataGridTextColumn Header="Rank Max" Binding="{Binding RankMax, UpdateSourceTrigger=LostFocus}" Width="100"/>
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
                            <DataGridTemplateColumn Header="" Width="80">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="Remove" Padding="6,1" Foreground="DarkRed"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveLeaderboardRewardCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>
                </DockPanel>
            </TabItem>

            <!-- 5: Packages (host the master-detail PackagesView) -->
            <TabItem Header="Packages">
                <views:PackagesView DataContext="{Binding PackagesVm}"/>
            </TabItem>

            <!-- 6: Statistics -->
            <TabItem Header="Statistics">
                <DockPanel>
                    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="8">
                        <Button Content="Refresh" Padding="10,2"
                                Command="{Binding StatisticsVm.RefreshCommand}"/>
                        <TextBlock Margin="16,0,0,0" VerticalAlignment="Center" Foreground="DimGray"
                                   Text="{Binding StatisticsVm.StatusMessage}"/>
                    </StackPanel>
                    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="8" DataContext="{Binding StatisticsVm}">
                        <StackPanel>
                            <!-- Participation Health -->
                            <TextBlock Text="Participation Health" FontSize="14" FontWeight="Bold" Margin="0,0,0,8"/>
                            <Grid Margin="0,0,0,16">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="200"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                </Grid.RowDefinitions>
                                <TextBlock Grid.Row="0" Grid.Column="0" Text="Total Participants:" Margin="0,2"/>
                                <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding TotalParticipants}" Margin="0,2"/>
                                <TextBlock Grid.Row="1" Grid.Column="0" Text="Active Last 7 Days:" Margin="0,2"/>
                                <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding ActiveLast7Days}" Margin="0,2"/>
                                <TextBlock Grid.Row="2" Grid.Column="0" Text="Retention Rate:" Margin="0,2"/>
                                <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding RetentionRate}" Margin="0,2"/>
                                <TextBlock Grid.Row="3" Grid.Column="0" Text="Avg Points / Day:" Margin="0,2"/>
                                <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding AvgPointsPerDay, StringFormat=F1}" Margin="0,2"/>
                            </Grid>

                            <TextBlock Text="Tier Distribution" FontSize="13" FontWeight="Bold" Margin="0,0,0,4"/>
                            <DataGrid ItemsSource="{Binding TierDistribution}" AutoGenerateColumns="False"
                                      IsReadOnly="True" HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                                      Margin="0,0,0,16" MaxHeight="200">
                                <DataGrid.Columns>
                                    <DataGridTextColumn Header="Tier #" Binding="{Binding TierNumber}" Width="80"/>
                                    <DataGridTextColumn Header="Name" Binding="{Binding TierName}" Width="*"/>
                                    <DataGridTextColumn Header="Claims" Binding="{Binding ClaimCount}" Width="100"/>
                                </DataGrid.Columns>
                            </DataGrid>

                            <TextBlock Text="Top 10 Leaderboard" FontSize="13" FontWeight="Bold" Margin="0,0,0,4"/>
                            <DataGrid ItemsSource="{Binding Top10}" AutoGenerateColumns="False"
                                      IsReadOnly="True" HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                                      Margin="0,0,0,16" MaxHeight="240">
                                <DataGrid.Columns>
                                    <DataGridTextColumn Header="Rank" Binding="{Binding Rank}" Width="60"/>
                                    <DataGridTextColumn Header="Character" Binding="{Binding CharacterName}" Width="*"/>
                                    <DataGridTextColumn Header="Points" Binding="{Binding TotalPoints}" Width="120"/>
                                </DataGrid.Columns>
                            </DataGrid>

                            <!-- Balance Tuning -->
                            <TextBlock Text="Balance Tuning" FontSize="14" FontWeight="Bold" Margin="0,8,0,8"/>
                            <Border Background="#F2F2F2" BorderBrush="#DDD" BorderThickness="1" Padding="8" Margin="0,0,0,12">
                                <TextBlock TextWrapping="Wrap" Foreground="DimGray"
                                           Text="Points by Activity Type: not available — the current schema (season_character_points) stores only cumulative totals. Adding per-activity tracking requires a schema change (see spec §Out of Scope)."/>
                            </Border>

                            <TextBlock Text="Objective Completion Rates" FontSize="13" FontWeight="Bold" Margin="0,0,0,4"/>
                            <DataGrid ItemsSource="{Binding ObjectiveCompletion}" AutoGenerateColumns="False"
                                      IsReadOnly="True" HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                                      MaxHeight="240">
                                <DataGrid.Columns>
                                    <DataGridTextColumn Header="Objective" Binding="{Binding Name}" Width="*"/>
                                    <DataGridTextColumn Header="Completed" Binding="{Binding CompletedCount}" Width="120"/>
                                </DataGrid.Columns>
                            </DataGrid>
                        </StackPanel>
                    </ScrollViewer>
                </DockPanel>
            </TabItem>
        </TabControl>
    </DockPanel>
</UserControl>
```

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Views\SeasonDetailView.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class SeasonDetailView : UserControl
    {
        public SeasonDetailView()
        {
            InitializeComponent();
        }

        private SeasonDetailViewModel? Vm => DataContext as SeasonDetailViewModel;

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            // The parent SeasonsView listens via the bound ViewModel — but for the simpler
            // detail-view case the back-arrow lives in the host. We surface this via a
            // routed event consumed by the parent SeasonsView's BackCommand wiring.
            if (DataContext is SeasonDetailViewModel)
            {
                // Bubble a request: parent SeasonsView wires its own button outside this
                // control. Here we walk up to the SeasonsView and invoke its back command.
                var parent = FindAncestor<SeasonsView>(this);
                parent?.RequestBack();
            }
        }

        private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            var current = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (current != null && current is not T)
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            return current as T;
        }
    }
}
```

> **Note:** `SeasonsView.RequestBack()` is defined in Task 15.

- [ ] Run verification command. Build must succeed (Task 15 must be present for the `SeasonsView` reference to resolve; if running this task standalone, comment out the `RequestBack()` call temporarily).

---

## Task 15: SeasonsView (XAML)

**Goal:** Top-level tab view. Holds the Seasons/Packages segmented switcher, the season cards grid (when in seasons mode), and acts as a host that swaps in `PackagesView` or `SeasonDetailView` as needed.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Views\SeasonsView.xaml`:

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.SeasonsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:common="clr-namespace:Perpetuum.AdminTool.Common"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:views="clr-namespace:Perpetuum.AdminTool.Views"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:SeasonsViewModel}">
    <UserControl.Resources>
        <common:BindingProxy x:Key="VmProxy" Data="{Binding}"/>

        <!-- Card style triggers — drive border colors based on SeasonRow.CardState -->
        <Style x:Key="SeasonCardBorder" TargetType="Border">
            <Setter Property="BorderThickness" Value="2"/>
            <Setter Property="BorderBrush" Value="#999999"/>
            <Setter Property="CornerRadius" Value="4"/>
            <Setter Property="Padding" Value="12"/>
            <Setter Property="Margin" Value="6"/>
            <Setter Property="Width" Value="260"/>
            <Setter Property="Height" Value="160"/>
            <Setter Property="Background" Value="White"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding CardState}" Value="Active">
                    <Setter Property="BorderBrush" Value="#1E88E5"/>
                </DataTrigger>
                <DataTrigger Binding="{Binding CardState}" Value="Draft">
                    <Setter Property="BorderBrush" Value="#999999"/>
                    <Setter Property="BorderThickness" Value="1.5"/>
                </DataTrigger>
                <DataTrigger Binding="{Binding CardState}" Value="Ended">
                    <Setter Property="BorderBrush" Value="#BBBBBB"/>
                    <Setter Property="Background" Value="#F4F4F4"/>
                    <Setter Property="Opacity" Value="0.7"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </UserControl.Resources>

    <DockPanel>
        <!-- Header with segmented switcher + New Season -->
        <Border DockPanel.Dock="Top" Background="#F2F2F2" Padding="8" BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <DockPanel>
                <Button DockPanel.Dock="Right" Content="+ New Season" Padding="10,2" FontWeight="Bold"
                        Command="{Binding NewSeasonCommand}"/>
                <Button DockPanel.Dock="Right" Content="Reload" Padding="10,2" Margin="0,0,8,0"
                        Click="OnReloadClick"/>
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                    <RadioButton GroupName="SeasonsTabSwitch" Content="Seasons" Padding="14,4"
                                 IsChecked="{Binding ShowSeasonsList, Mode=OneWay}"
                                 Command="{Binding ShowSeasonsCommand}"/>
                    <RadioButton GroupName="SeasonsTabSwitch" Content="Packages" Padding="14,4" Margin="6,0,0,0"
                                 IsChecked="{Binding ShowPackagesView, Mode=OneWay}"
                                 Command="{Binding ShowPackagesPanelCommand}"/>
                    <TextBlock Margin="16,0,0,0" VerticalAlignment="Center" Text="{Binding StatusMessage}">
                        <TextBlock.Style>
                            <Style TargetType="TextBlock">
                                <Setter Property="Foreground" Value="DimGray"/>
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding StatusIsError}" Value="True">
                                        <Setter Property="Foreground" Value="DarkRed"/>
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </TextBlock.Style>
                    </TextBlock>
                </StackPanel>
            </DockPanel>
        </Border>

        <Grid>
            <!-- Seasons cards -->
            <ScrollViewer VerticalScrollBarVisibility="Auto"
                          Visibility="{Binding ShowSeasonsList, Converter={StaticResource BoolToVisibilityHidden}, FallbackValue=Collapsed}">
                <ItemsControl ItemsSource="{Binding Seasons}" Margin="8">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate>
                            <WrapPanel/>
                        </ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Style="{StaticResource SeasonCardBorder}">
                                <DockPanel>
                                    <Border DockPanel.Dock="Top" HorizontalAlignment="Left" Padding="4,1"
                                            CornerRadius="2" Margin="0,0,0,6">
                                        <Border.Style>
                                            <Style TargetType="Border">
                                                <Setter Property="Background" Value="#999999"/>
                                                <Style.Triggers>
                                                    <DataTrigger Binding="{Binding CardState}" Value="Active">
                                                        <Setter Property="Background" Value="#1E88E5"/>
                                                    </DataTrigger>
                                                    <DataTrigger Binding="{Binding CardState}" Value="Draft">
                                                        <Setter Property="Background" Value="#9E9E9E"/>
                                                    </DataTrigger>
                                                    <DataTrigger Binding="{Binding CardState}" Value="Ended">
                                                        <Setter Property="Background" Value="#757575"/>
                                                    </DataTrigger>
                                                </Style.Triggers>
                                            </Style>
                                        </Border.Style>
                                        <TextBlock Text="{Binding CardState}" Foreground="White"
                                                   FontSize="10" FontWeight="Bold"/>
                                    </Border>
                                    <Button DockPanel.Dock="Bottom" Content="Manage &#x2192;" Padding="8,2" Margin="0,8,0,0"
                                            HorizontalAlignment="Right"
                                            Command="{Binding Source={StaticResource VmProxy}, Path=Data.NavigateToSeasonCommand}"
                                            CommandParameter="{Binding}"/>
                                    <StackPanel>
                                        <TextBlock Text="{Binding Name}" FontSize="14" FontWeight="Bold" TextTrimming="CharacterEllipsis"/>
                                        <TextBlock Margin="0,4,0,0" Foreground="DimGray" FontSize="11">
                                            <Run Text="{Binding StartTime, StringFormat=yyyy-MM-dd}"/>
                                            <Run Text="-"/>
                                            <Run Text="{Binding EndTime, StringFormat=yyyy-MM-dd}"/>
                                        </TextBlock>
                                        <TextBlock Margin="0,2,0,0" Foreground="DimGray" FontSize="11"
                                                   Text="{Binding Description}" TextTrimming="CharacterEllipsis"/>
                                    </StackPanel>
                                </DockPanel>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </ScrollViewer>

            <!-- Packages master-detail -->
            <ContentControl Content="{Binding PackagesVm}"
                            Visibility="{Binding ShowPackagesView, Converter={StaticResource BoolToVisibilityHidden}, FallbackValue=Collapsed}">
                <ContentControl.ContentTemplate>
                    <DataTemplate DataType="{x:Type vm:PackagesViewModel}">
                        <views:PackagesView/>
                    </DataTemplate>
                </ContentControl.ContentTemplate>
            </ContentControl>

            <!-- Detail drill-down -->
            <ContentControl Content="{Binding DetailViewModel}"
                            Visibility="{Binding IsInDetail, Converter={StaticResource BoolToVisibilityHidden}, FallbackValue=Collapsed}">
                <ContentControl.ContentTemplate>
                    <DataTemplate DataType="{x:Type vm:SeasonDetailViewModel}">
                        <views:SeasonDetailView/>
                    </DataTemplate>
                </ContentControl.ContentTemplate>
            </ContentControl>
        </Grid>
    </DockPanel>
</UserControl>
```

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Views\SeasonsView.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class SeasonsView : UserControl
    {
        public SeasonsView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private SeasonsViewModel? Vm => DataContext as SeasonsViewModel;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Vm != null && Vm.Seasons.Count == 0)
                await Vm.LoadAsync();
        }

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await Vm.LoadAsync();
        }

        // Invoked by SeasonDetailView's back arrow via VisualTree walk-up.
        public void RequestBack()
        {
            Vm?.BackToListCommand.Execute(null);
        }
    }
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 16: SeasonWizardWindow (XAML)

**Goal:** Modal 6-step wizard window.

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Views\SeasonWizardWindow.xaml`:

```xml
<Window x:Class="Perpetuum.AdminTool.Views.SeasonWizardWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:common="clr-namespace:Perpetuum.AdminTool.Common"
        xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        d:DataContext="{d:DesignInstance Type=vm:SeasonWizardViewModel}"
        Title="New Season" Width="780" Height="600"
        WindowStartupLocation="CenterOwner">
    <Window.Resources>
        <common:BindingProxy x:Key="VmProxy" Data="{Binding}"/>
    </Window.Resources>
    <DockPanel>
        <!-- Top: step indicator -->
        <Border DockPanel.Dock="Top" Background="#F2F2F2" Padding="12" BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <StackPanel>
                <TextBlock Text="{Binding StepTitle}" FontSize="14" FontWeight="Bold" Margin="0,0,0,8"/>
                <StackPanel Orientation="Horizontal">
                    <Border Width="28" Height="28" CornerRadius="14" Background="#1E88E5" Margin="0,0,4,0">
                        <TextBlock Text="1" Foreground="White" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <Border Width="28" Height="28" CornerRadius="14" Margin="0,0,4,0">
                        <Border.Style>
                            <Style TargetType="Border">
                                <Setter Property="Background" Value="#CCCCCC"/>
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="2"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="3"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="4"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="5"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="6"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </Border.Style>
                        <TextBlock Text="2" Foreground="White" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <Border Width="28" Height="28" CornerRadius="14" Margin="0,0,4,0">
                        <Border.Style>
                            <Style TargetType="Border">
                                <Setter Property="Background" Value="#CCCCCC"/>
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="3"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="4"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="5"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="6"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </Border.Style>
                        <TextBlock Text="3" Foreground="White" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <Border Width="28" Height="28" CornerRadius="14" Margin="0,0,4,0">
                        <Border.Style>
                            <Style TargetType="Border">
                                <Setter Property="Background" Value="#CCCCCC"/>
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="4"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="5"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="6"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </Border.Style>
                        <TextBlock Text="4" Foreground="White" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <Border Width="28" Height="28" CornerRadius="14" Margin="0,0,4,0">
                        <Border.Style>
                            <Style TargetType="Border">
                                <Setter Property="Background" Value="#CCCCCC"/>
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="5"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="6"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </Border.Style>
                        <TextBlock Text="5" Foreground="White" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <Border Width="28" Height="28" CornerRadius="14" Margin="0,0,4,0">
                        <Border.Style>
                            <Style TargetType="Border">
                                <Setter Property="Background" Value="#CCCCCC"/>
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding CurrentStep}" Value="6"><Setter Property="Background" Value="#1E88E5"/></DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </Border.Style>
                        <TextBlock Text="6" Foreground="White" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                </StackPanel>
            </StackPanel>
        </Border>

        <!-- Bottom: Back / Next / Finish -->
        <Border DockPanel.Dock="Bottom" Background="#FAFAFA" BorderBrush="#DDD" BorderThickness="0,1,0,0" Padding="12">
            <DockPanel>
                <Button DockPanel.Dock="Left" Content="Back" Padding="14,4" Width="80"
                        Command="{Binding BackCommand}"
                        IsEnabled="{Binding CanGoBack}"/>
                <Button DockPanel.Dock="Right" Padding="14,4" Width="160" FontWeight="Bold"
                        Click="OnFinishOrNextClick">
                    <Button.Style>
                        <Style TargetType="Button">
                            <Setter Property="Content" Value="Next"/>
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IsReviewStep}" Value="True">
                                    <Setter Property="Content" Value="Add to Change Queue"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </Button.Style>
                </Button>
                <TextBlock Margin="16,0" VerticalAlignment="Center" Foreground="DimGray"
                           Text="{Binding FinishHint}"/>
            </DockPanel>
        </Border>

        <!-- Step content -->
        <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="12">
            <Grid>
                <!-- Step 1: Info -->
                <StackPanel Visibility="{Binding IsStep1, Converter={StaticResource BoolToVisibilityHidden}, FallbackValue=Collapsed}">
                    <TextBlock Text="Provide the season's identity and active window. End time must be after start time."
                               TextWrapping="Wrap" Foreground="DimGray" Margin="0,0,0,12"/>
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="120"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>
                        <TextBlock Grid.Row="0" Grid.Column="0" Text="Name:" Margin="0,4"/>
                        <TextBox  Grid.Row="0" Grid.Column="1" Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" Margin="0,4"/>
                        <TextBlock Grid.Row="1" Grid.Column="0" Text="Description:" Margin="0,4"/>
                        <TextBox  Grid.Row="1" Grid.Column="1" Text="{Binding Description, UpdateSourceTrigger=PropertyChanged}"
                                  TextWrapping="Wrap" AcceptsReturn="True" MinHeight="60" Margin="0,4"/>
                        <TextBlock Grid.Row="2" Grid.Column="0" Text="Start time (UTC):" Margin="0,4"/>
                        <DatePicker Grid.Row="2" Grid.Column="1" SelectedDate="{Binding StartTime}" Margin="0,4"/>
                        <TextBlock Grid.Row="3" Grid.Column="0" Text="End time (UTC):" Margin="0,4"/>
                        <DatePicker Grid.Row="3" Grid.Column="1" SelectedDate="{Binding EndTime}" Margin="0,4"/>
                    </Grid>
                    <TextBlock Margin="0,8,0,0" Foreground="DarkRed" Text="{Binding Step1Validation}"/>
                </StackPanel>

                <!-- Step 2: Activity Rates -->
                <StackPanel Visibility="{Binding IsStep2, Converter={StaticResource BoolToVisibilityHidden}, FallbackValue=Collapsed}">
                    <TextBlock Text="Set how many points each activity awards. Points per Unit = 0 disables the type. Scale is the unit denominator for bulk activities (e.g. 1000 NIC earned = X pts)."
                               TextWrapping="Wrap" Foreground="DimGray" Margin="0,0,0,8"/>
                    <DataGrid ItemsSource="{Binding ActivityRates}" AutoGenerateColumns="False"
                              CanUserAddRows="False" CanUserDeleteRows="False"
                              HeadersVisibility="Column" GridLinesVisibility="All" MinHeight="280">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Activity Type" Binding="{Binding ActivityTypeLabel}" Width="180" IsReadOnly="True"/>
                            <DataGridTextColumn Header="Points per Unit" Binding="{Binding PointsPerUnit, UpdateSourceTrigger=LostFocus}" Width="140"/>
                            <DataGridTextColumn Header="Scale" Binding="{Binding UnitScale, UpdateSourceTrigger=LostFocus}" Width="100"/>
                            <DataGridTextColumn Header="Effective Rate" Binding="{Binding EffectiveRate}" Width="*" IsReadOnly="True"/>
                        </DataGrid.Columns>
                    </DataGrid>
                    <TextBlock Margin="0,8,0,0" Foreground="DimGray" TextWrapping="Wrap"
                               Text="Note: activity rates configured here will be queued AFTER the new season's INSERT is committed and the season detail is reopened. The wizard records your intent; you finish wiring rates from the detail tab."/>
                </StackPanel>

                <!-- Step 3: Objectives -->
                <StackPanel Visibility="{Binding IsStep3, Converter={StaticResource BoolToVisibilityHidden}, FallbackValue=Collapsed}">
                    <TextBlock Text="Define optional milestone objectives. These reward bonus points on completion."
                               TextWrapping="Wrap" Foreground="DimGray" Margin="0,0,0,8"/>
                    <Button Content="+ Add Objective" Padding="8,2" HorizontalAlignment="Left"
                            Command="{Binding AddObjectiveRowCommand}" Margin="0,0,0,6"/>
                    <DataGrid ItemsSource="{Binding Objectives}" AutoGenerateColumns="False"
                              CanUserAddRows="False" CanUserDeleteRows="False"
                              HeadersVisibility="Column" GridLinesVisibility="All" MinHeight="240">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Name" Binding="{Binding Name, UpdateSourceTrigger=LostFocus}" Width="180"/>
                            <DataGridTextColumn Header="Description" Binding="{Binding Description, UpdateSourceTrigger=LostFocus}" Width="*"/>
                            <DataGridTextColumn Header="Activity Type" Binding="{Binding ActivityType, UpdateSourceTrigger=LostFocus}" Width="160"/>
                            <DataGridTextColumn Header="Target" Binding="{Binding TargetValue, UpdateSourceTrigger=LostFocus}" Width="100"/>
                            <DataGridTextColumn Header="Bonus Pts" Binding="{Binding BonusPoints, UpdateSourceTrigger=LostFocus}" Width="100"/>
                            <DataGridTemplateColumn Header="" Width="80">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="Remove" Padding="6,1" Foreground="DarkRed"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveObjectiveRowCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>
                </StackPanel>

                <!-- Step 4: Tiers -->
                <StackPanel Visibility="{Binding IsStep4, Converter={StaticResource BoolToVisibilityHidden}, FallbackValue=Collapsed}">
                    <TextBlock Text="Define reward tiers reachable as players accumulate points."
                               TextWrapping="Wrap" Foreground="DimGray" Margin="0,0,0,8"/>
                    <TextBlock Foreground="DarkOrange" Margin="0,0,0,8" TextWrapping="Wrap"
                               Visibility="{Binding HasPackages, Converter={StaticResource BoolToVisibilityHidden}, ConverterParameter=invert, FallbackValue=Collapsed}"
                               Text="No packages exist yet. Switch to the Packages tab and create one first, then return here."/>
                    <Button Content="+ Add Tier" Padding="8,2" HorizontalAlignment="Left"
                            Command="{Binding AddTierRowCommand}" Margin="0,0,0,6"
                            IsEnabled="{Binding HasPackages}"/>
                    <DataGrid ItemsSource="{Binding Tiers}" AutoGenerateColumns="False"
                              CanUserAddRows="False" CanUserDeleteRows="False"
                              HeadersVisibility="Column" GridLinesVisibility="All" MinHeight="240">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Tier #" Binding="{Binding TierNumber, UpdateSourceTrigger=LostFocus}" Width="80"/>
                            <DataGridTextColumn Header="Name" Binding="{Binding TierName, UpdateSourceTrigger=LostFocus}" Width="200"/>
                            <DataGridTextColumn Header="Points Required" Binding="{Binding PointsRequired, UpdateSourceTrigger=LostFocus}" Width="140"/>
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
                            <DataGridTemplateColumn Header="" Width="80">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="Remove" Padding="6,1" Foreground="DarkRed"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveTierRowCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>
                </StackPanel>

                <!-- Step 5: Leaderboard Rewards -->
                <StackPanel Visibility="{Binding IsStep5, Converter={StaticResource BoolToVisibilityHidden}, FallbackValue=Collapsed}">
                    <TextBlock Text="Define rank-bracket rewards for top finishers."
                               TextWrapping="Wrap" Foreground="DimGray" Margin="0,0,0,8"/>
                    <Button Content="+ Add Bracket" Padding="8,2" HorizontalAlignment="Left"
                            Command="{Binding AddLeaderboardRowCommand}" Margin="0,0,0,6"
                            IsEnabled="{Binding HasPackages}"/>
                    <DataGrid ItemsSource="{Binding LeaderboardRewards}" AutoGenerateColumns="False"
                              CanUserAddRows="False" CanUserDeleteRows="False"
                              HeadersVisibility="Column" GridLinesVisibility="All" MinHeight="240">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Rank Min" Binding="{Binding RankMin, UpdateSourceTrigger=LostFocus}" Width="100"/>
                            <DataGridTextColumn Header="Rank Max" Binding="{Binding RankMax, UpdateSourceTrigger=LostFocus}" Width="100"/>
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
                            <DataGridTemplateColumn Header="" Width="80">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="Remove" Padding="6,1" Foreground="DarkRed"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveLeaderboardRowCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>
                </StackPanel>

                <!-- Step 6: Review -->
                <StackPanel Visibility="{Binding IsReviewStep, Converter={StaticResource BoolToVisibilityHidden}, FallbackValue=Collapsed}">
                    <TextBlock Text="Review" FontSize="14" FontWeight="Bold" Margin="0,0,0,8"/>
                    <Grid Margin="0,0,0,12">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="180"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>
                        <TextBlock Grid.Row="0" Grid.Column="0" Text="Name:" Margin="0,2"/>
                        <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding Name}" Margin="0,2"/>
                        <TextBlock Grid.Row="1" Grid.Column="0" Text="Description:" Margin="0,2"/>
                        <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding Description}" TextWrapping="Wrap" Margin="0,2"/>
                        <TextBlock Grid.Row="2" Grid.Column="0" Text="Start time:" Margin="0,2"/>
                        <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding StartTime, StringFormat=yyyy-MM-dd HH:mm}" Margin="0,2"/>
                        <TextBlock Grid.Row="3" Grid.Column="0" Text="End time:" Margin="0,2"/>
                        <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding EndTime, StringFormat=yyyy-MM-dd HH:mm}" Margin="0,2"/>
                        <TextBlock Grid.Row="4" Grid.Column="0" Text="Objectives:" Margin="0,2"/>
                        <TextBlock Grid.Row="4" Grid.Column="1" Text="{Binding Objectives.Count}" Margin="0,2"/>
                        <TextBlock Grid.Row="5" Grid.Column="0" Text="Tiers:" Margin="0,2"/>
                        <TextBlock Grid.Row="5" Grid.Column="1" Text="{Binding Tiers.Count}" Margin="0,2"/>
                        <TextBlock Grid.Row="6" Grid.Column="0" Text="Leaderboard brackets:" Margin="0,2"/>
                        <TextBlock Grid.Row="6" Grid.Column="1" Text="{Binding LeaderboardRewards.Count}" Margin="0,2"/>
                    </Grid>
                    <Border Background="#E3F2FD" BorderBrush="#1E88E5" BorderThickness="1" Padding="8">
                        <TextBlock TextWrapping="Wrap"
                                   Text="On click of 'Add to Change Queue', the season INSERT (is_active=0, Draft) will be added to the change queue. Child rows (activity rates, objectives, tiers, leaderboard) reference the new season id and must be configured AFTER you commit and reopen the season's detail."/>
                    </Border>
                </StackPanel>
            </Grid>
        </ScrollViewer>
    </DockPanel>
</Window>
```

- [ ] Write file `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Views\SeasonWizardWindow.xaml.cs`:

```csharp
using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class SeasonWizardWindow : Window
    {
        private readonly SeasonWizardViewModel _vm;

        public SeasonWizardWindow(SeasonWizardViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
        }

        private void OnFinishOrNextClick(object sender, RoutedEventArgs e)
        {
            if (_vm.IsReviewStep)
            {
                _vm.FinishCommand.Execute(null);
                DialogResult = true;
                Close();
            }
            else
            {
                _vm.NextCommand.Execute(null);
            }
        }
    }
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 17: Wire SeasonsViewModel into MainViewModel and MainWindow

**Goal:** Construct and expose the `SeasonsViewModel` from `MainViewModel`, and add a `<TabItem>` for it in `MainWindow.xaml`. Also ensure `App.xaml` exposes the `BoolToVisibilityHidden` resource referenced in the views.

- [ ] Inspect `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\App.xaml`. If a `<BooleanToVisibilityConverter>` is not registered with key `BoolToVisibilityHidden`, add it under `<Application.Resources>`. The full file should look like (preserve any existing resources):

```xml
<Application x:Class="Perpetuum.AdminTool.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVisibilityHidden"/>
    </Application.Resources>
</Application>
```

> If the file already has `<Application.Resources>`, only insert the `<BooleanToVisibilityConverter x:Key="BoolToVisibilityHidden"/>` element inside it.

- [ ] Edit `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\ViewModels\MainViewModel.cs`. Add three changes:

  1. Add `using` directives at the top of the file (alongside existing usings):

```csharp
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Seasons;
```

  2. Add a new public property next to the existing `Flocks` property:

```csharp
public SeasonsViewModel Seasons { get; }
```

  3. Inside the constructor, after the line `Flocks = new FlocksViewModel(store, session.Changes, session.Lookups);`, add:

```csharp
            Seasons = new SeasonsViewModel(
                new SeasonRepository(store.Settings.Connection),
                new PackageRepository(store.Settings.Connection),
                session.Changes,
                session.Lookups,
                store.Settings.Connection);
```

- [ ] Edit `E:\MyStuff\Projects\PerpetuumServer2\src\Perpetuum.AdminTool\Views\MainWindow.xaml`. Add a `<TabItem Header="Seasons">` inside the existing `<TabControl x:Name="ModuleTabs">`, placed right after the `<TabItem Header="NPC flocks">` block. Insert:

```xml
            <TabItem Header="Seasons">
                <views:SeasonsView DataContext="{Binding Seasons}"/>
            </TabItem>
```

- [ ] Run final verification: `dotnet build E:\MyStuff\Projects\PerpetuumServer2\PerpetuumServer2.sln -c Release -p:Platform=x64`. Build must succeed with no errors. Warnings may remain (existing baseline) but must not be regressed.

---

## Self-review Checklist

Before declaring the plan complete, verify:

- [x] Every spec section from `2026-05-10-seasons-admin-tool-design.md` has a corresponding task:
  - LookupCache `hidden` column change → Task 1
  - Entity Picker filtering by 11 root flags → Task 2
  - Row models for `seasons`, `season_activity_rates`, `season_objectives`, `season_tiers`, `season_leaderboard_rewards`, `packages`, `packageitems` → Task 3
  - Repository SQL for all reads (seasons + statistics) → Task 4
  - Repository SQL for packages and usage → Task 5
  - Change objects for every season mutation (insert/update/activate/deactivate, upsert activity rate, CRUD for objectives/tiers/leaderboard) → Task 6
  - Change objects for packages and package items → Task 7
  - Master-detail packages VM → Task 8
  - Statistics VM (read-only, lazy-load) → Task 9
  - Per-season detail VM (7 tabs including Statistics activation hook) → Task 10
  - Top-level tab VM (segmented switcher + drill-down state) → Task 11
  - 6-step wizard VM → Task 12
  - PackagesView XAML + code-behind → Task 13
  - SeasonDetailView XAML + code-behind (all 7 tabs) → Task 14
  - SeasonsView XAML + code-behind (cards, switcher, host) → Task 15
  - SeasonWizardWindow XAML + code-behind → Task 16
  - Bootstrapper wiring in MainViewModel + MainWindow + App resources → Task 17
- [x] No `TBD`, `TODO`, or "similar to" placeholders in any task.
- [x] All ViewModel constructor signatures match what the consuming code expects:
  - `PackagesViewModel(PackageRepository, SeasonRepository, ChangeQueue, LookupCache, ConnectionSettings)` — consumed by SeasonsViewModel.
  - `SeasonDetailViewModel(SeasonRow, SeasonRepository, PackageRepository, ChangeQueue, PackagesViewModel, SeasonStatisticsViewModel, LookupCache, ConnectionSettings, ObservableCollection<PackageRow>)` — consumed by SeasonsViewModel.NavigateToSeason.
  - `SeasonWizardViewModel(ChangeQueue, ObservableCollection<PackageRow>, Action)` — consumed by SeasonsViewModel.NewSeason.
  - `SeasonStatisticsViewModel(SeasonRepository)` — consumed by SeasonsViewModel.NavigateToSeason.
- [x] All file paths are absolute and rooted in `E:\MyStuff\Projects\PerpetuumServer2\`.
- [x] The verification command is identical for every task: `dotnet build E:\MyStuff\Projects\PerpetuumServer2\PerpetuumServer2.sln -c Release -p:Platform=x64`.
- [x] All XAML files declare `xmlns` for any used clr-namespace (common, vm, views, d, mc).
- [x] Activity-type ComboBoxes (in detail and wizard DataGrids) bind to `ActivityTypeOptions` (a static read-only list of `{Value, Label}` pairs in `SeasonDetailViewModel`).
- [x] Package ComboBoxes (tiers and leaderboard) bind to the shared `Packages` collection.
- [x] The `RemoveItemCommand` (PackagesViewModel) takes a `PackageItemRow?` parameter; XAML passes the row as `CommandParameter`.

---

