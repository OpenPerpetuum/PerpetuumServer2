# IMPROVEMENT-033: Equipment Set Season Rewards — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow season tier, objective, and leaderboard rewards to grant a random item from a named equipment set instead of (or instead of) a fixed item package.

**Architecture:** `equipment_set_id INT NULL` is added directly to the three season reward tables; the existing `package_id` columns become nullable on tiers and leaderboard rewards. At delivery time, the server resolves the set to a random member definition and writes it to `accountredeemableitems` — the same pipeline the client already uses. No client protocol changes. The Admin Tool gains an Equipment Set ComboBox column alongside the existing Package column in each reward editor.

**Tech Stack:** .NET 8 / C# 12, SQL Server, WPF + CommunityToolkit.Mvvm

**Spec:** `docs/superpowers/specs/2026-05-30-improvement-033-equipment-set-season-rewards-design.md`

---

## File Map

**Modified — server:**
- `src/Perpetuum/Services/Seasons/SeasonModels.cs` — model property changes
- `src/Perpetuum/Services/Seasons/SeasonRepository.cs` — read path + two new methods + clone fix
- `src/Perpetuum/Services/Seasons/SeasonService.cs` — delivery branching + mail refactor

**Modified — Admin Tool:**
- `src/Perpetuum.AdminTool/Seasons/SeasonTierRow.cs`
- `src/Perpetuum.AdminTool/Seasons/SeasonLeaderboardRewardRow.cs`
- `src/Perpetuum.AdminTool/Seasons/SeasonObjectiveRow.cs`
- `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs` — six build methods
- `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs` — three load methods + one new method
- `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs` — EquipmentSets list, LoadAsync, new QueueSave command, AddTier/AddLeaderboardReward cleanup
- `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml` — Equipment Set columns, Leaderboard Queue Save button

---

## Task 1: Apply database schema changes

**Files:**
- No code files — operator-run SQL DDL only

These five statements must be applied to the live database **before** the updated server or Admin Tool is deployed.

- [ ] **Step 1: Run the following SQL against the game database**

```sql
-- Make package_id nullable on tables where it was NOT NULL
ALTER TABLE season_tiers               ALTER COLUMN package_id INT NULL;
ALTER TABLE season_leaderboard_rewards ALTER COLUMN package_id INT NULL;

-- Add equipment_set_id to all three reward tables
ALTER TABLE season_tiers               ADD equipment_set_id INT NULL REFERENCES equipment_sets(set_id);
ALTER TABLE season_objectives          ADD equipment_set_id INT NULL REFERENCES equipment_sets(set_id);
ALTER TABLE season_leaderboard_rewards ADD equipment_set_id INT NULL REFERENCES equipment_sets(set_id);
```

Expected: 5 rows affected, no errors. Existing rows retain their current `package_id` values; `equipment_set_id` defaults to NULL everywhere.

- [ ] **Step 2: Verify**

```sql
SELECT COLUMN_NAME, IS_NULLABLE, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('season_tiers','season_objectives','season_leaderboard_rewards')
  AND COLUMN_NAME IN ('package_id','equipment_set_id')
ORDER BY TABLE_NAME, COLUMN_NAME;
```

Expected: 6 rows — `equipment_set_id` is nullable on all three tables; `package_id` is nullable on `season_tiers` and `season_leaderboard_rewards`.

- [ ] **Step 3: Commit note**

No code to commit in this task. DDL is the deliverable.

---

## Task 2: Update server models

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonModels.cs`

- [ ] **Step 1: Apply changes to SeasonModels.cs**

Replace the entire file content with:

```csharp
namespace Perpetuum.Services.Seasons
{
    public class Season
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsActive { get; set; }
        public bool IsRecurring { get; set; }
        public int? RecurrenceGapDays { get; set; }
        public int RecurrenceIteration { get; set; } = 1;
        public string? RecurrenceBaseName { get; set; }
        public SeasonScoringMode ScoringMode { get; set; }
        public int? DailyObjectivesPerDay { get; set; }
    }

    public class SeasonActivityRate
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public SeasonActivityType ActivityType { get; set; }
        public double PointsPerUnit { get; set; }
        public int UnitScale { get; set; }
    }

    public class SeasonObjective
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public SeasonActivityType ActivityType { get; set; }
        public long TargetValue { get; set; }
        public int BonusPoints { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsDaily { get; set; }
        public int? PackageId { get; set; }
        public int? EquipmentSetId { get; set; }
        public int? TargetDefinitionId { get; set; }
    }

    public class SeasonTier
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public int TierNumber { get; set; }
        public string TierName { get; set; } = "";
        public int PointsRequired { get; set; }
        public int? PackageId { get; set; }
        public int? EquipmentSetId { get; set; }
    }

    public class SeasonLeaderboardReward
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public int RankMin { get; set; }
        public int RankMax { get; set; }
        public int? PackageId { get; set; }
        public int? EquipmentSetId { get; set; }
    }

    public class SeasonCharacterPoints
    {
        public int CharacterId { get; set; }
        public int SeasonId { get; set; }
        public double TotalPoints { get; set; }
        public bool IntroMailSent { get; set; }
        public bool LeaderboardRewardDelivered { get; set; }
    }

    public class SeasonPackageItem
    {
        public int Definition { get; set; }
        public int Quantity { get; set; }
    }
}
```

- [ ] **Step 2: Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonModels.cs
git commit -m "feat(seasons): add EquipmentSetId to reward models; PackageId nullable on tiers and leaderboard"
```

---

## Task 3: Update server repository — read path and clone

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonRepository.cs`

Four methods change: `GetObjectives`, `GetTiers`, `GetLeaderboardRewards`, and `CloneSeasonForNextIteration`.

- [ ] **Step 1: Update GetObjectives**

Find the `GetObjectives` method. Replace its SQL and mapping:

```csharp
public List<SeasonObjective> GetObjectives(int seasonId)
{
    return Db.Query("SELECT id, season_id, name, description, activity_type, " +
                    "target_value, bonus_points, display_order, is_daily, package_id, " +
                    "target_definition_id, equipment_set_id " +
                    "FROM season_objectives WHERE season_id = @seasonId ORDER BY display_order")
             .SetParameter("@seasonId", seasonId)
             .Execute()
             .Select(r => new SeasonObjective
             {
                 Id = r.GetValue<int>("id"),
                 SeasonId = r.GetValue<int>("season_id"),
                 Name = r.GetValue<string>("name"),
                 Description = r.GetValue<string>("description"),
                 ActivityType = (SeasonActivityType)r.GetValue<int>("activity_type"),
                 TargetValue = r.GetValue<long>("target_value"),
                 BonusPoints = r.GetValue<int>("bonus_points"),
                 DisplayOrder = r.GetValue<int>("display_order"),
                 IsDaily = r.GetValue<bool>("is_daily"),
                 PackageId = r.GetValue<int?>("package_id"),
                 TargetDefinitionId = r.GetValue<int?>("target_definition_id"),
                 EquipmentSetId = r.GetValue<int?>("equipment_set_id"),
             })
             .ToList();
}
```

- [ ] **Step 2: Update GetTiers**

Replace the `GetTiers` method:

```csharp
public List<SeasonTier> GetTiers(int seasonId)
{
    return Db.Query("SELECT id, season_id, tier_number, tier_name, points_required, " +
                    "package_id, equipment_set_id " +
                    "FROM season_tiers WHERE season_id = @seasonId ORDER BY tier_number")
             .SetParameter("@seasonId", seasonId)
             .Execute()
             .Select(r => new SeasonTier
             {
                 Id = r.GetValue<int>("id"),
                 SeasonId = r.GetValue<int>("season_id"),
                 TierNumber = r.GetValue<int>("tier_number"),
                 TierName = r.GetValue<string>("tier_name"),
                 PointsRequired = r.GetValue<int>("points_required"),
                 PackageId = r.GetValue<int?>("package_id"),
                 EquipmentSetId = r.GetValue<int?>("equipment_set_id"),
             })
             .ToList();
}
```

- [ ] **Step 3: Update GetLeaderboardRewards**

Replace the `GetLeaderboardRewards` method:

```csharp
public List<SeasonLeaderboardReward> GetLeaderboardRewards(int seasonId)
{
    return Db.Query("SELECT id, season_id, rank_min, rank_max, package_id, equipment_set_id " +
                    "FROM season_leaderboard_rewards WHERE season_id = @seasonId")
             .SetParameter("@seasonId", seasonId)
             .Execute()
             .Select(r => new SeasonLeaderboardReward
             {
                 Id = r.GetValue<int>("id"),
                 SeasonId = r.GetValue<int>("season_id"),
                 RankMin = r.GetValue<int>("rank_min"),
                 RankMax = r.GetValue<int>("rank_max"),
                 PackageId = r.GetValue<int?>("package_id"),
                 EquipmentSetId = r.GetValue<int?>("equipment_set_id"),
             })
             .ToList();
}
```

- [ ] **Step 4: Update CloneSeasonForNextIteration — three INSERT…SELECT queries**

In `CloneSeasonForNextIteration`, find the three `Db.Query(...)` calls that clone objectives, tiers, and leaderboard rewards. Replace each with the version below.

Objectives clone — add `equipment_set_id` to column list and SELECT:

```csharp
Db.Query(
    "INSERT INTO season_objectives " +
    "(season_id, name, description, activity_type, target_value, " +
    "bonus_points, display_order, is_daily, package_id, equipment_set_id) " +
    "SELECT @newId, name, description, activity_type, target_value, " +
    "bonus_points, display_order, is_daily, package_id, equipment_set_id " +
    "FROM season_objectives WHERE season_id = @prevId")
    .SetParameter("@newId", newId)
    .SetParameter("@prevId", previous.Id)
    .ExecuteNonQuery();
```

Tiers clone:

```csharp
Db.Query(
    "INSERT INTO season_tiers " +
    "(season_id, tier_number, tier_name, points_required, package_id, equipment_set_id) " +
    "SELECT @newId, tier_number, tier_name, points_required, package_id, equipment_set_id " +
    "FROM season_tiers WHERE season_id = @prevId")
    .SetParameter("@newId", newId)
    .SetParameter("@prevId", previous.Id)
    .ExecuteNonQuery();
```

Leaderboard rewards clone:

```csharp
Db.Query(
    "INSERT INTO season_leaderboard_rewards " +
    "(season_id, rank_min, rank_max, package_id, equipment_set_id) " +
    "SELECT @newId, rank_min, rank_max, package_id, equipment_set_id " +
    "FROM season_leaderboard_rewards WHERE season_id = @prevId")
    .SetParameter("@newId", newId)
    .SetParameter("@prevId", previous.Id)
    .ExecuteNonQuery();
```

- [ ] **Step 5: Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonRepository.cs
git commit -m "feat(seasons): read equipment_set_id in GetTiers/GetObjectives/GetLeaderboardRewards; clone fix"
```

---

## Task 4: Add new server repository methods

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonRepository.cs`

Add two new methods to `SeasonRepository`. A good location is immediately after `GetPackageItems`.

- [ ] **Step 1: Add GetSetMemberDefinitions**

```csharp
public List<int> GetSetMemberDefinitions(int setId)
{
    return Db.Query("SELECT definition FROM equipment_set_members WHERE set_id = @setId")
             .SetParameter("@setId", setId)
             .Execute()
             .Select(r => r.GetValue<int>("definition"))
             .ToList();
}
```

- [ ] **Step 2: Add InsertRedeemableItem**

Place this immediately after `InsertRedeemableItems`:

```csharp
public void InsertRedeemableItem(int accountId, int definition)
{
    Db.Query("INSERT INTO accountredeemableitems (accountid, definition, quantity) " +
             "VALUES (@accountId, @definition, 1)")
      .SetParameter("@accountId", accountId)
      .SetParameter("@definition", definition)
      .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.SQLInsertError);
}
```

- [ ] **Step 3: Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonRepository.cs
git commit -m "feat(seasons): add GetSetMemberDefinitions and InsertRedeemableItem to SeasonRepository"
```

---

## Task 5: Update SeasonService delivery logic

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

Four changes: refactor `SendTierUnlockMail`, update `DeliverTierReward`, update `DeliverObjectivePackage` (renamed), update `DeliverLeaderboardReward`, fix `RecordActivity` call site.

- [ ] **Step 1: Refactor SendTierUnlockMail to accept items**

The current `SendTierUnlockMail` re-fetches items internally from `tier.PackageId`. Change the signature to accept a pre-resolved list. Find the method and replace it:

```csharp
private void SendTierUnlockMail(int characterId, SeasonTier tier, double total,
    List<SeasonPackageItem> items)
{
    var character = Character.Get(characterId);
    var dict = _customDictionary.GetDictionary(0);
    string subject = $"Tier Unlocked: {tier.TierName}";
    string body = $"You reached {tier.PointsRequired} season points and unlocked the {tier.TierName} tier reward!\n" +
                     $"Total points: {total}\n" +
                     $"Redeem your reward at any terminal via the Redeemable Items menu.";
    MailHandler.SendMail(_announcer.Value, character, subject, body,
        MailType.character, out _, out _);

    var chatMessage = new StringBuilder();
    chatMessage.AppendLine();
    chatMessage.AppendLine($"{character.Nick} just unlocked a new tier reward!");
    chatMessage.AppendLine();
    chatMessage.AppendLine($"Tier: {tier.TierName}");
    chatMessage.AppendLine($"Points: {total:N2}");
    chatMessage.AppendLine();
    chatMessage.AppendLine("Rewards:");

    if (items.Count > 0)
    {
        foreach (var item in items)
        {
            var ed = EntityDefault.Reader.Get(item.Definition);
            string name = (ed != null && ed != EntityDefault.None)
                ? Translate(ed.Name, dict)
                : item.Definition.ToString();
            chatMessage.AppendLine($"- {name} x{item.Quantity}");
        }
    }
    else
    {
        chatMessage.AppendLine("- Equipment set reward (check your Redeemable Items)");
    }

    chatMessage.AppendLine();
    chatMessage.AppendLine("Congratulations!");

    _channelManager.Value.Announcement(SeasonChannelName, _announcer.Value, chatMessage.ToString());
}
```

- [ ] **Step 2: Replace DeliverTierReward**

```csharp
private void DeliverTierReward(int characterId, int seasonId, SeasonTier tier, double currentPoints)
{
    var character = Character.Get(characterId);

    if (tier.EquipmentSetId.HasValue)
    {
        var definitions = _repository.GetSetMemberDefinitions(tier.EquipmentSetId.Value);
        if (definitions.Count == 0)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"[SeasonService] Tier {tier.Id} equipment set {tier.EquipmentSetId} has no members; skipping reward.");
            return;
        }
        var definition = definitions[new Random().Next(definitions.Count)];
        _repository.InsertRedeemableItem(character.AccountId, definition);
        SendTierUnlockMail(characterId, tier, currentPoints, new List<SeasonPackageItem>());
    }
    else if (tier.PackageId.HasValue)
    {
        var items = _repository.GetPackageItems(tier.PackageId.Value);
        if (items.Count == 0)
            return;
        _repository.InsertRedeemableItems(character.AccountId, tier.PackageId.Value, items);
        SendTierUnlockMail(characterId, tier, currentPoints, items);
    }
    else
    {
        System.Diagnostics.Trace.TraceWarning(
            $"[SeasonService] Tier {tier.Id} has neither package_id nor equipment_set_id; skipping reward.");
    }
}
```

- [ ] **Step 3: Replace DeliverObjectivePackage**

Rename the method to `DeliverObjectiveReward` and update its signature and body:

```csharp
private void DeliverObjectiveReward(int characterId, int? packageId, int? equipmentSetId)
{
    var character = Character.Get(characterId);

    if (equipmentSetId.HasValue)
    {
        var definitions = _repository.GetSetMemberDefinitions(equipmentSetId.Value);
        if (definitions.Count == 0)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"[SeasonService] Objective equipment set {equipmentSetId} has no members; skipping reward.");
            return;
        }
        var definition = definitions[new Random().Next(definitions.Count)];
        _repository.InsertRedeemableItem(character.AccountId, definition);
    }
    else if (packageId.HasValue)
    {
        var items = _repository.GetPackageItems(packageId.Value);
        if (items.Count == 0)
            return;
        _repository.InsertRedeemableItems(character.AccountId, packageId.Value, items);
    }
    else
    {
        System.Diagnostics.Trace.TraceWarning(
            $"[SeasonService] Objective reward has neither package_id nor equipment_set_id; skipping.");
    }
}
```

- [ ] **Step 4: Update the RecordActivity call site for objective rewards**

In `RecordActivity`, find:

```csharp
if (obj.IsDaily && obj.PackageId.HasValue)
    DeliverObjectivePackage(characterId, obj.PackageId.Value);
```

Replace with:

```csharp
if (obj.IsDaily && (obj.PackageId.HasValue || obj.EquipmentSetId.HasValue))
    DeliverObjectiveReward(characterId, obj.PackageId, obj.EquipmentSetId);
```

- [ ] **Step 5: Replace DeliverLeaderboardReward**

```csharp
private void DeliverLeaderboardReward(int characterId, SeasonLeaderboardReward reward)
{
    var character = Character.Get(characterId);

    if (reward.EquipmentSetId.HasValue)
    {
        var definitions = _repository.GetSetMemberDefinitions(reward.EquipmentSetId.Value);
        if (definitions.Count == 0)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"[SeasonService] Leaderboard reward {reward.Id} equipment set {reward.EquipmentSetId} has no members; skipping.");
            return;
        }
        var definition = definitions[new Random().Next(definitions.Count)];
        _repository.InsertRedeemableItem(character.AccountId, definition);
    }
    else if (reward.PackageId.HasValue)
    {
        var items = _repository.GetPackageItems(reward.PackageId.Value);
        if (items.Count == 0)
            return;
        _repository.InsertRedeemableItems(character.AccountId, reward.PackageId.Value, items);
    }
    else
    {
        System.Diagnostics.Trace.TraceWarning(
            $"[SeasonService] Leaderboard reward {reward.Id} has neither package_id nor equipment_set_id; skipping.");
    }
}
```

- [ ] **Step 6: Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat(seasons): delivery branching for equipment set rewards in tiers, objectives, leaderboard"
```

---

## Task 6: Build and verify server compiles

**Files:**
- No changes — verification step only

- [ ] **Step 1: Build the solution**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors. Common issues to check if errors appear:
- Any call site that used `tier.PackageId` as non-nullable `int` (now `int?`) — add `.Value` or `.HasValue` check
- Any call site that used `reward.PackageId` as non-nullable `int` — same
- The old `DeliverObjectivePackage(characterId, packageId)` call — should now be `DeliverObjectiveReward`
- `SendTierUnlockMail` call sites — must now pass a `List<SeasonPackageItem>` as fourth argument

- [ ] **Step 2: Commit if any fixes were needed**

```
git add -p
git commit -m "fix(seasons): compilation fixes after PackageId nullability change"
```

---

## Task 7: Update Admin Tool row models

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonTierRow.cs`
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonLeaderboardRewardRow.cs`
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonObjectiveRow.cs`

- [ ] **Step 1: Replace SeasonTierRow.cs**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.EquipmentSets;
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
        [ObservableProperty] private int? _packageId;
        [ObservableProperty] private PackageRow? _selectedPackage;
        [ObservableProperty] private int? _equipmentSetId;
        [ObservableProperty] private EquipmentSetRow? _selectedEquipmentSet;

        partial void OnSelectedPackageChanged(PackageRow? value)
        {
            if (value != null)
            {
                PackageId = value.Id;
                EquipmentSetId = null;
                _selectedEquipmentSet = null;
                OnPropertyChanged(nameof(SelectedEquipmentSet));
            }
        }

        partial void OnSelectedEquipmentSetChanged(EquipmentSetRow? value)
        {
            if (value != null)
            {
                EquipmentSetId = value.SetId;
                PackageId = null;
                _selectedPackage = null;
                OnPropertyChanged(nameof(SelectedPackage));
            }
        }
    }
}
```

- [ ] **Step 2: Replace SeasonLeaderboardRewardRow.cs**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.EquipmentSets;
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
        [ObservableProperty] private int? _packageId;
        [ObservableProperty] private PackageRow? _selectedPackage;
        [ObservableProperty] private int? _equipmentSetId;
        [ObservableProperty] private EquipmentSetRow? _selectedEquipmentSet;

        partial void OnSelectedPackageChanged(PackageRow? value)
        {
            if (value != null)
            {
                PackageId = value.Id;
                EquipmentSetId = null;
                _selectedEquipmentSet = null;
                OnPropertyChanged(nameof(SelectedEquipmentSet));
            }
        }

        partial void OnSelectedEquipmentSetChanged(EquipmentSetRow? value)
        {
            if (value != null)
            {
                EquipmentSetId = value.SetId;
                PackageId = null;
                _selectedPackage = null;
                OnPropertyChanged(nameof(SelectedPackage));
            }
        }
    }
}
```

- [ ] **Step 3: Update SeasonObjectiveRow.cs — add EquipmentSet fields**

In `SeasonObjectiveRow.cs`, add the following two fields and their partial callbacks. Insert them after the `_selectedPackage` / `OnSelectedPackageChanged` block:

```csharp
[ObservableProperty] private int? _equipmentSetId;
[ObservableProperty] private EquipmentSetRow? _selectedEquipmentSet;

partial void OnSelectedEquipmentSetChanged(EquipmentSetRow? value)
{
    if (value != null)
    {
        EquipmentSetId = value.SetId;
        PackageId = null;
        _selectedPackage = null;
        OnPropertyChanged(nameof(SelectedPackage));
    }
}
```

Also update the existing `OnSelectedPackageChanged` partial to clear the set when a package is chosen:

```csharp
partial void OnSelectedPackageChanged(PackageRow? value)
{
    if (value != null)
    {
        PackageId = value.Id;
        EquipmentSetId = null;
        _selectedEquipmentSet = null;
        OnPropertyChanged(nameof(SelectedEquipmentSet));
    }
    else
    {
        PackageId = value?.Id;
    }
}
```

Add the using at the top:

```csharp
using Perpetuum.AdminTool.EquipmentSets;
```

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/Seasons/SeasonTierRow.cs
git add src/Perpetuum.AdminTool/Seasons/SeasonLeaderboardRewardRow.cs
git add src/Perpetuum.AdminTool/Seasons/SeasonObjectiveRow.cs
git commit -m "feat(admintool/seasons): add EquipmentSetId and SelectedEquipmentSet to reward row models"
```

---

## Task 8: Update SeasonChanges — all six build methods

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs`

Replace the six affected methods. Leave `BuildInsert(SeasonRow)`, `BuildUpdate(SeasonRow)`, `BuildActivate`, `BuildDeactivate`, `BuildUpsertActivityRate`, `BuildDeleteObjective`, `BuildDeleteTier`, `BuildDeleteLeaderboardReward` unchanged.

- [ ] **Step 1: Replace BuildInsertObjective**

```csharp
public static IPendingChange BuildInsertObjective(SeasonObjectiveRow row)
{
    return new RawSqlChange(
        $"season_objectives: insert '{row.Name}' in season {row.SeasonId}",
        $"INSERT INTO season_objectives (season_id, name, description, activity_type, " +
        $"target_value, bonus_points, display_order, is_daily, package_id, target_definition_id, equipment_set_id) VALUES (" +
        $"{row.SeasonId}, {SqlLiteral.Of(row.Name)}, {SqlLiteral.Of(row.Description)}, " +
        $"{(int)row.ActivityType}, {row.TargetValue}, {row.BonusPoints}, {row.DisplayOrder}, " +
        $"{(row.IsDaily ? 1 : 0)}, {SqlLiteral.OfNullableInt(row.PackageId)}, " +
        $"{SqlLiteral.OfNullableInt(row.TargetDefinitionId)}, {SqlLiteral.OfNullableInt(row.EquipmentSetId)})");
}
```

- [ ] **Step 2: Replace BuildUpdateObjective**

```csharp
public static IPendingChange BuildUpdateObjective(SeasonObjectiveRow row)
{
    return new RawSqlChange(
        $"season_objectives: update id {row.Id}",
        $"UPDATE season_objectives SET name = {SqlLiteral.Of(row.Name)}, " +
        $"description = {SqlLiteral.Of(row.Description)}, " +
        $"activity_type = {(int)row.ActivityType}, target_value = {row.TargetValue}, " +
        $"bonus_points = {row.BonusPoints}, display_order = {row.DisplayOrder}, " +
        $"is_daily = {(row.IsDaily ? 1 : 0)}, package_id = {SqlLiteral.OfNullableInt(row.PackageId)}, " +
        $"target_definition_id = {SqlLiteral.OfNullableInt(row.TargetDefinitionId)}, " +
        $"equipment_set_id = {SqlLiteral.OfNullableInt(row.EquipmentSetId)} " +
        $"WHERE id = {row.Id}");
}
```

- [ ] **Step 3: Replace BuildInsertTier**

```csharp
public static IPendingChange BuildInsertTier(SeasonTierRow row) =>
    new RawSqlChange(
        $"season_tiers: insert tier {row.TierNumber} ('{row.TierName}') in season {row.SeasonId}",
        $"INSERT INTO season_tiers (season_id, tier_number, tier_name, points_required, package_id, equipment_set_id) VALUES (" +
        $"{row.SeasonId}, {row.TierNumber}, {SqlLiteral.Of(row.TierName)}, {row.PointsRequired}, " +
        $"{SqlLiteral.OfNullableInt(row.PackageId)}, {SqlLiteral.OfNullableInt(row.EquipmentSetId)})");
```

- [ ] **Step 4: Replace BuildUpdateTier**

```csharp
public static IPendingChange BuildUpdateTier(SeasonTierRow row) =>
    new RawSqlChange(
        $"season_tiers: update id {row.Id}",
        $"UPDATE season_tiers SET tier_number = {row.TierNumber}, tier_name = {SqlLiteral.Of(row.TierName)}, " +
        $"points_required = {row.PointsRequired}, package_id = {SqlLiteral.OfNullableInt(row.PackageId)}, " +
        $"equipment_set_id = {SqlLiteral.OfNullableInt(row.EquipmentSetId)} WHERE id = {row.Id}");
```

- [ ] **Step 5: Replace BuildInsertLeaderboardReward**

```csharp
public static IPendingChange BuildInsertLeaderboardReward(SeasonLeaderboardRewardRow row) =>
    new RawSqlChange(
        $"season_leaderboard_rewards: insert ranks {row.RankMin}-{row.RankMax} in season {row.SeasonId}",
        $"INSERT INTO season_leaderboard_rewards (season_id, rank_min, rank_max, package_id, equipment_set_id) VALUES (" +
        $"{row.SeasonId}, {row.RankMin}, {row.RankMax}, " +
        $"{SqlLiteral.OfNullableInt(row.PackageId)}, {SqlLiteral.OfNullableInt(row.EquipmentSetId)})");
```

- [ ] **Step 6: Replace BuildUpdateLeaderboardReward**

```csharp
public static IPendingChange BuildUpdateLeaderboardReward(SeasonLeaderboardRewardRow row) =>
    new RawSqlChange(
        $"season_leaderboard_rewards: update id {row.Id}",
        $"UPDATE season_leaderboard_rewards SET rank_min = {row.RankMin}, rank_max = {row.RankMax}, " +
        $"package_id = {SqlLiteral.OfNullableInt(row.PackageId)}, " +
        $"equipment_set_id = {SqlLiteral.OfNullableInt(row.EquipmentSetId)} WHERE id = {row.Id}");
```

- [ ] **Step 7: Commit**

```
git add src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs
git commit -m "feat(admintool/seasons): add equipment_set_id to all six SeasonChanges build methods"
```

---

## Task 9: Update Admin Tool SeasonRepository

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs`

- [ ] **Step 1: Update LoadObjectivesAsync — add equipment_set_id**

In the SELECT command text, add `equipment_set_id` after `target_definition_id`. In the row construction, add:

```csharp
EquipmentSetId = reader.IsDBNull(11) ? (int?)null : reader.GetInt32(11),
```

The full updated SELECT:
```sql
SELECT id, season_id, name, description, activity_type,
       target_value, bonus_points, display_order, is_daily, package_id,
       target_definition_id, equipment_set_id
FROM season_objectives WHERE season_id = @seasonId ORDER BY display_order
```

And the updated object construction (indices 0–11):
```csharp
result.Add(new SeasonObjectiveRow
{
    Id           = reader.GetInt32(0),
    SeasonId     = reader.GetInt32(1),
    Name         = reader.IsDBNull(2) ? "" : reader.GetString(2),
    Description  = reader.IsDBNull(3) ? "" : reader.GetString(3),
    ActivityType = (SeasonActivityType)reader.GetInt32(4),
    TargetValue  = reader.GetInt64(5),
    BonusPoints  = reader.GetInt32(6),
    DisplayOrder = reader.GetInt32(7),
    IsDaily      = !reader.IsDBNull(8) && reader.GetBoolean(8),
    PackageId    = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
    TargetDefinitionId = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
    EquipmentSetId = reader.IsDBNull(11) ? (int?)null : reader.GetInt32(11),
});
```

- [ ] **Step 2: Update LoadTiersAsync — add equipment_set_id**

Updated SELECT:
```sql
SELECT id, season_id, tier_number, tier_name, points_required,
       package_id, equipment_set_id
FROM season_tiers WHERE season_id = @seasonId ORDER BY tier_number
```

Updated object construction:
```csharp
result.Add(new SeasonTierRow
{
    Id             = reader.GetInt32(0),
    SeasonId       = reader.GetInt32(1),
    TierNumber     = reader.GetInt32(2),
    TierName       = reader.IsDBNull(3) ? "" : reader.GetString(3),
    PointsRequired = reader.GetInt32(4),
    PackageId      = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
    EquipmentSetId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
});
```

- [ ] **Step 3: Update LoadLeaderboardRewardsAsync — add equipment_set_id**

Updated SELECT:
```sql
SELECT id, season_id, rank_min, rank_max, package_id, equipment_set_id
FROM season_leaderboard_rewards WHERE season_id = @seasonId ORDER BY rank_min
```

Updated object construction:
```csharp
result.Add(new SeasonLeaderboardRewardRow
{
    Id             = reader.GetInt32(0),
    SeasonId       = reader.GetInt32(1),
    RankMin        = reader.GetInt32(2),
    RankMax        = reader.GetInt32(3),
    PackageId      = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
    EquipmentSetId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
});
```

- [ ] **Step 4: Add LoadEquipmentSetsAsync**

Add this method to `SeasonRepository`. It queries the same table as `AdminTool/EquipmentSets/EquipmentSetRepository.LoadAllSetsAsync()`:

```csharp
public async Task<List<EquipmentSetRow>> LoadEquipmentSetsAsync()
{
    var result = new List<EquipmentSetRow>();
    await using var cn = new SqlConnection(_connection.BuildConnectionString());
    await cn.OpenAsync();
    await using var cmd = cn.CreateCommand();
    cmd.CommandText = "SELECT set_id, name FROM equipment_sets ORDER BY name";
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        result.Add(new EquipmentSetRow
        {
            SetId = reader.GetInt32(0),
            Name  = reader.IsDBNull(1) ? "" : reader.GetString(1),
        });
    return result;
}
```

Add the using at the top of the file:

```csharp
using Perpetuum.AdminTool.EquipmentSets;
```

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs
git commit -m "feat(admintool/seasons): read equipment_set_id in load methods; add LoadEquipmentSetsAsync"
```

---

## Task 10: Update SeasonDetailViewModel

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs`

Six changes: add `EquipmentSets` property, update `LoadAsync`, fix `AddTier`, add `QueueSaveLeaderboardRewardCommand`, fix `AddLeaderboardReward`.

- [ ] **Step 1: Add EquipmentSets property**

In the field declarations section (near `public ObservableCollection<PackageRow> Packages { get; }`), add:

```csharp
public IReadOnlyList<EquipmentSetRow> EquipmentSets { get; private set; } =
    Array.Empty<EquipmentSetRow>();
```

Add the using:

```csharp
using Perpetuum.AdminTool.EquipmentSets;
```

- [ ] **Step 2: Update LoadAsync — load equipment sets and wire SelectedEquipmentSet on rows**

At the start of the `try` block in `LoadAsync`, before the `BuildMaterialLists` call or immediately after, add:

```csharp
EquipmentSets = await _repo.LoadEquipmentSetsAsync();
OnPropertyChanged(nameof(EquipmentSets));
```

In the `Objectives.Clear()` block, update the objective loading loop:

```csharp
Objectives.Clear();
if (Season.Id > 0)
    foreach (var o in await _repo.LoadObjectivesAsync(Season.Id))
    {
        if (o.PackageId.HasValue)
            o.SelectedPackage = Packages.FirstOrDefault(p => p.Id == o.PackageId);
        if (o.EquipmentSetId.HasValue)
            o.SelectedEquipmentSet = EquipmentSets.FirstOrDefault(s => s.SetId == o.EquipmentSetId);
        o.InitializeMaterialLists(_oreAndLiquidMaterials, _organicMaterials);
        Objectives.Add(o);
    }
```

In the `Tiers.Clear()` block, update the tier loading loop:

```csharp
Tiers.Clear();
if (Season.Id > 0)
    foreach (var t in await _repo.LoadTiersAsync(Season.Id))
    {
        if (t.PackageId.HasValue)
            t.SelectedPackage = Packages.FirstOrDefault(p => p.Id == t.PackageId);
        if (t.EquipmentSetId.HasValue)
            t.SelectedEquipmentSet = EquipmentSets.FirstOrDefault(s => s.SetId == t.EquipmentSetId);
        Tiers.Add(t);
    }
```

In the `LeaderboardRewards.Clear()` block:

```csharp
LeaderboardRewards.Clear();
if (Season.Id > 0)
    foreach (var l in await _repo.LoadLeaderboardRewardsAsync(Season.Id))
    {
        if (l.PackageId.HasValue)
            l.SelectedPackage = Packages.FirstOrDefault(p => p.Id == l.PackageId);
        if (l.EquipmentSetId.HasValue)
            l.SelectedEquipmentSet = EquipmentSets.FirstOrDefault(s => s.SetId == l.EquipmentSetId);
        LeaderboardRewards.Add(l);
    }
```

- [ ] **Step 3: Fix AddTier — remove Packages guard, clear default reward**

Replace the `AddTier` command body with:

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
    var row = new SeasonTierRow
    {
        SeasonId       = Season.Id,
        TierNumber     = Tiers.Count + 1,
        TierName       = $"Tier {Tiers.Count + 1}",
        PointsRequired = (Tiers.Count + 1) * 1000,
        IsNew          = true
    };
    Tiers.Add(row);
    StatusIsError = false;
    StatusMessage = "Added tier row. Set a Package or Equipment Set reward, then click 'Queue Save'.";
}
```

- [ ] **Step 4: Add QueueSaveLeaderboardRewardCommand**

Add this command method to `SeasonDetailViewModel`:

```csharp
[RelayCommand]
private void QueueSaveLeaderboardReward(SeasonLeaderboardRewardRow? row)
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
        _queue.Add(SeasonChanges.BuildInsertLeaderboardReward(row));
        StatusMessage = $"Queued INSERT for leaderboard reward (ranks {row.RankMin}-{row.RankMax}).";
    }
    else
    {
        _queue.Add(SeasonChanges.BuildUpdateLeaderboardReward(row));
        StatusMessage = $"Queued UPDATE for leaderboard reward id {row.Id}.";
    }
    StatusIsError = false;
}
```

- [ ] **Step 5: Fix AddLeaderboardReward — remove Packages guard and auto-queue**

Replace the `AddLeaderboardReward` command body with:

```csharp
[RelayCommand]
private void AddLeaderboardReward()
{
    if (Season.Id <= 0)
    {
        MessageBox.Show("Save the season (General tab) first.", "Season unsaved",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    var row = new SeasonLeaderboardRewardRow
    {
        SeasonId = Season.Id,
        RankMin  = 1,
        RankMax  = 1,
        IsNew    = true
    };
    LeaderboardRewards.Add(row);
    StatusIsError = false;
    StatusMessage = "Added leaderboard reward row. Set ranks and a reward, then click 'Queue Save'.";
}
```

- [ ] **Step 6: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs
git commit -m "feat(admintool/seasons): EquipmentSets list, SelectedEquipmentSet wiring, QueueSaveLeaderboardReward command"
```

---

## Task 11: Update SeasonDetailView.xaml

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml`

Three tabs need changes: Objectives (tab 2), Tiers (tab 3), Leaderboard (tab 4).

- [ ] **Step 1: Objectives tab — update Reward Package column and add Equipment Set column**

Find the `DataGridTemplateColumn` with `Header="Reward Package"` (around line 248). Replace it with two columns:

```xml
<DataGridTemplateColumn Header="Package Reward" Width="150">
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
<DataGridTemplateColumn Header="Set Reward" Width="150">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <TextBlock Margin="4,0" VerticalAlignment="Center"
                       Text="{Binding SelectedEquipmentSet.Name, FallbackValue='(none)'}"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
    <DataGridTemplateColumn.CellEditingTemplate>
        <DataTemplate>
            <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.EquipmentSets}"
                      DisplayMemberPath="Name"
                      SelectedItem="{Binding SelectedEquipmentSet, UpdateSourceTrigger=PropertyChanged}"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellEditingTemplate>
</DataGridTemplateColumn>
```

- [ ] **Step 2: Tiers tab — update Reward Package column and add Equipment Set column**

Find the `DataGridTemplateColumn` with `Header="Reward Package"` in the Tiers tab (around line 304). Replace it with two columns (same pattern as step 1, but `Width="*"` on the first):

```xml
<DataGridTemplateColumn Header="Package Reward" Width="*">
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
<DataGridTemplateColumn Header="Set Reward" Width="150">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <TextBlock Margin="4,0" VerticalAlignment="Center"
                       Text="{Binding SelectedEquipmentSet.Name, FallbackValue='(none)'}"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
    <DataGridTemplateColumn.CellEditingTemplate>
        <DataTemplate>
            <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.EquipmentSets}"
                      DisplayMemberPath="Name"
                      SelectedItem="{Binding SelectedEquipmentSet, UpdateSourceTrigger=PropertyChanged}"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellEditingTemplate>
</DataGridTemplateColumn>
```

- [ ] **Step 3: Leaderboard tab — update Reward Package column, add Equipment Set column, add Queue Save button**

Find the `DataGridTemplateColumn` with `Header="Reward Package"` in the Leaderboard tab (around line 359). Replace it with two columns plus a Queue Save button column. The Remove button column stays at the end.

Replace the existing single "Reward Package" column with:

```xml
<DataGridTemplateColumn Header="Package Reward" Width="*">
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
<DataGridTemplateColumn Header="Set Reward" Width="150">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <TextBlock Margin="4,0" VerticalAlignment="Center"
                       Text="{Binding SelectedEquipmentSet.Name, FallbackValue='(none)'}"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
    <DataGridTemplateColumn.CellEditingTemplate>
        <DataTemplate>
            <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.EquipmentSets}"
                      DisplayMemberPath="Name"
                      SelectedItem="{Binding SelectedEquipmentSet, UpdateSourceTrigger=PropertyChanged}"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellEditingTemplate>
</DataGridTemplateColumn>
```

Then add a Queue Save column before the existing Remove column:

```xml
<DataGridTemplateColumn Header="" Width="110">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <Button Content="Queue Save" Padding="6,1"
                    Command="{Binding Source={StaticResource VmProxy}, Path=Data.QueueSaveLeaderboardRewardCommand}"
                    CommandParameter="{Binding}"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml
git commit -m "feat(admintool/seasons): add Equipment Set reward columns and Leaderboard Queue Save button"
```

---

## Task 12: Build Admin Tool and manual validation

**Files:**
- No changes — build and test step

- [ ] **Step 1: Build the solution**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors. Common issues if build fails:
- `EquipmentSetRow` not found — check the `using Perpetuum.AdminTool.EquipmentSets;` is in all three row files and in `SeasonRepository.cs`
- `QueueSaveLeaderboardRewardCommand` not found in XAML binding — confirm the `[RelayCommand]` attribute generates `QueueSaveLeaderboardRewardCommand` (CommunityToolkit MVVM source-gen naming: method `QueueSaveLeaderboardReward` → command `QueueSaveLeaderboardRewardCommand`)
- `EquipmentSets` binding in XAML returns empty — confirm `OnPropertyChanged(nameof(EquipmentSets))` fires after `LoadEquipmentSetsAsync()` completes

- [ ] **Step 2: Manual test — tier with equipment set reward**

1. Start the Admin Tool; connect to the database.
2. Navigate to a season (or create a test season).
3. On the Tiers tab, click **+ Add Tier**.
4. In the new row, click the "Set Reward" cell and select an equipment set from the dropdown.
5. Confirm the "Package Reward" cell clears to `(none)`.
6. Click **Queue Save** on the row.
7. Click **Commit** in the main toolbar.
8. Verify in DB: `SELECT package_id, equipment_set_id FROM season_tiers WHERE season_id = <id>` — expected: `package_id = NULL`, `equipment_set_id = <chosen set id>`.
9. Start the server with the season active. Trigger the tier unlock for a test character (use admin command or direct DB insert of enough points).
10. Verify `accountredeemableitems` has a new row with a `definition` belonging to the chosen equipment set (check via `SELECT definition FROM equipment_set_members WHERE set_id = <id>`).
11. Log in with the test character; redeem the item; confirm it lands in the public container.

- [ ] **Step 3: Manual test — objective with equipment set reward (daily)**

1. On the Objectives tab, add a daily objective. Set "Set Reward" to an equipment set.
2. Queue Save and Commit.
3. Trigger objective completion for a test character.
4. Verify `accountredeemableitems` has a new row with a definition from the set.

- [ ] **Step 4: Manual test — leaderboard reward with equipment set**

1. On the Leaderboard tab, click **+ Add Bracket**.
2. Set ranks (e.g. 1–1) and select an equipment set in the "Set Reward" cell.
3. Click **Queue Save**, then **Commit**.
4. End the season (admin command or wait).
5. Verify the rank-1 character receives a redeemable item from the set.

- [ ] **Step 5: Regression test — existing package rewards still work**

1. Create a tier using the **Package Reward** ComboBox (select an existing package).
2. Confirm "Set Reward" cell clears to `(none)`.
3. Queue Save and Commit.
4. Trigger tier unlock; verify items from the package appear in `accountredeemableitems` as before.

- [ ] **Step 6: Regression test — recurring season clone**

1. Create a recurring season with one tier using an equipment set reward.
2. After season end, verify the cloned season's tier also has `equipment_set_id` populated (check DB directly).

- [ ] **Step 7: Final commit if any fixes were applied during testing**

```
git add -p
git commit -m "fix(seasons): post-testing corrections for IMPROVEMENT-033"
```
