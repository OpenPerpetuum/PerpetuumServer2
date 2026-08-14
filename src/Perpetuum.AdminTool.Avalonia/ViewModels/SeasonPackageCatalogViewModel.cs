using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.EquipmentSets;
using Perpetuum.AdminTool.Export;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Seasons;
using Perpetuum.AdminTool.ViewModels;
using Perpetuum.ExportedTypes;
using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class SeasonPackageCatalogViewModel : ObservableObject
{
    private readonly IPackageRepository _packageRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IEntityRepository _entityRepository;
    private readonly ChangeQueue _queue;
    private readonly Func<string, string> _translate;
    private readonly IContentExporter? _contentExporter;
    private IReadOnlyList<MaterialPickItem> _oreAndLiquidMaterials = Array.Empty<MaterialPickItem>();
    private IReadOnlyList<MaterialPickItem> _organicMaterials = Array.Empty<MaterialPickItem>();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage = "Load packages and seasons from the server.";
    [ObservableProperty] private PackageRow? _selectedPackage;
    [ObservableProperty] private PackageItemRow? _selectedPackageItem;
    [ObservableProperty] private PackageItemPickItem? _selectedPackageItemChoice;
    [ObservableProperty] private int _newPackageItemQuantity = 1;
    [ObservableProperty] private string _newPackageName = "New Package";
    [ObservableProperty] private SeasonRow? _selectedSeason;
    [ObservableProperty] private SeasonActivityRateRow? _selectedActivityRate;
    [ObservableProperty] private SeasonObjectiveRow? _selectedObjective;
    [ObservableProperty] private SeasonTierRow? _selectedTier;
    [ObservableProperty] private SeasonLeaderboardRewardRow? _selectedLeaderboardReward;
    [ObservableProperty] private SeasonActivityType _newActivityType = SeasonActivityType.NpcKill;
    [ObservableProperty] private int _participantCount;
    [ObservableProperty] private int _activeLast7Days;
    [ObservableProperty] private double _averagePointsPerDay;
    [ObservableProperty] private SeasonWizardViewModel? _wizard;
    [ObservableProperty] private SeasonObjectiveRow? _selectedWizardObjective;
    [ObservableProperty] private SeasonTierRow? _selectedWizardTier;
    [ObservableProperty] private SeasonLeaderboardRewardRow? _selectedWizardLeaderboardReward;
    [ObservableProperty] private string _exportScript = string.Empty;
    [ObservableProperty] private int _selectedSeasonDetailTabIndex;

    public SeasonPackageCatalogViewModel(
        IPackageRepository packageRepository,
        ISeasonRepository seasonRepository,
        IEntityRepository entityRepository,
        ChangeQueue queue,
        Func<string, string>? translate = null,
        IContentExporter? contentExporter = null)
    {
        _packageRepository = packageRepository;
        _seasonRepository = seasonRepository;
        _entityRepository = entityRepository;
        _queue = queue;
        _translate = translate ?? (key => key);
        _contentExporter = contentExporter;
    }

    public ObservableCollection<PackageRow> Packages { get; } = new();
    public ObservableCollection<PackageItemRow> PackageItems { get; } = new();
    public ObservableCollection<PackageUsageRow> PackageUsage { get; } = new();
    public ObservableCollection<PackageItemPickItem> PackageItemChoices { get; } = new();
    public ObservableCollection<SeasonRow> Seasons { get; } = new();
    public ObservableCollection<SeasonActivityRateRow> ActivityRates { get; } = new();
    public ObservableCollection<SeasonObjectiveRow> Objectives { get; } = new();
    public ObservableCollection<SeasonTierRow> Tiers { get; } = new();
    public ObservableCollection<SeasonLeaderboardRewardRow> LeaderboardRewards { get; } = new();
    public ObservableCollection<TodaysDailyObjectiveRow> DailyObjectives { get; } = new();
    public ObservableCollection<TierDistributionRow> TierDistribution { get; } = new();
    public ObservableCollection<LeaderboardEntryRow> TopLeaderboard { get; } = new();
    public ObservableCollection<ObjectiveCompletionRow> ObjectiveCompletion { get; } = new();
    public ObservableCollection<EquipmentSetRow> EquipmentSets { get; } = new();
    public IReadOnlyList<SeasonActivityType> ActivityTypes { get; } = Enum.GetValues<SeasonActivityType>();
    public IReadOnlyList<SeasonScoringMode> ScoringModes { get; } = Enum.GetValues<SeasonScoringMode>();
    public bool IsNotLoading => !IsLoading;
    public bool HasWizard => Wizard != null;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));
    partial void OnWizardChanged(SeasonWizardViewModel? value) => OnPropertyChanged(nameof(HasWizard));
    partial void OnSelectedPackageChanged(PackageRow? value) => _ = LoadPackageDetailAsync(value);
    partial void OnSelectedSeasonChanged(SeasonRow? value) => _ = LoadSeasonDetailAsync(value);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusIsError = false;
        StatusMessage = "Loading packages and seasons...";
        try
        {
            Task<List<PackageRow>> packagesTask = _packageRepository.LoadAllPackagesAsync();
            Task<List<SeasonRow>> seasonsTask = _seasonRepository.LoadAllSeasonsAsync();
            Task<EntitiesSnapshot> entitiesTask = _entityRepository.LoadAsync();
            Task<List<EquipmentSetRow>> equipmentTask = _seasonRepository.LoadEquipmentSetsAsync();
            await Task.WhenAll(packagesTask, seasonsTask, entitiesTask, equipmentTask);
            Replace(Packages, packagesTask.Result);
            Replace(Seasons, seasonsTask.Result);
            Replace(EquipmentSets, equipmentTask.Result);
            List<EntityPickItem> entities = entitiesTask.Result.Rows.Select(row => new EntityPickItem
            {
                Definition = row.Definition,
                Name = row.DefinitionName,
                CategoryFlags = row.CategoryFlags,
                Enabled = row.Enabled,
                Hidden = row.Hidden,
                TierType = row.TierType ?? 0,
                TierLevel = row.TierLevel ?? 0
            }).ToList();
            Dictionary<string, string> names = entities.ToDictionary(entity => entity.Name, entity => _translate(entity.Name));
            Replace(PackageItemChoices, PackageItemPickItem.BuildFilteredList(entities, names));
            BuildMaterialLists(entities, names);
            StatusMessage = $"Loaded {Packages.Count} packages and {Seasons.Count} seasons.";
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = $"Unable to load packages and seasons: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    private async Task LoadPackageDetailAsync(PackageRow? package)
    {
        PackageItems.Clear();
        PackageUsage.Clear();
        if (package == null || package.Id <= 0) return;
        try
        {
            Task<List<PackageItemRow>> itemsTask = _packageRepository.LoadPackageItemsAsync(package.Id);
            Task<List<PackageUsageRow>> usageTask = _packageRepository.LoadSeasonUsageAsync(package.Id);
            await Task.WhenAll(itemsTask, usageTask);
            foreach (PackageItemRow item in itemsTask.Result)
            {
                item.SelectedPickItem = PackageItemChoices.FirstOrDefault(choice => choice.Definition == item.Definition);
                item.DisplayName = item.SelectedPickItem?.DisplayName ?? $"definition {item.Definition}";
            }
            Replace(PackageItems, itemsTask.Result);
            Replace(PackageUsage, usageTask.Result);
        }
        catch (Exception ex) { SetError($"Unable to load package detail: {ex.Message}"); }
    }

    [RelayCommand]
    private void QueueNewPackage()
    {
        string name = NewPackageName.Trim();
        if (string.IsNullOrEmpty(name)) { SetError("Package name is required."); return; }
        var row = new PackageRow { Id = 0, Name = name, IsNew = true };
        Packages.Add(row);
        SelectedPackage = row;
        StatusIsError = false;
        StatusMessage = "New package created locally. Add its items, then queue the complete package.";
    }

    [RelayCommand]
    private void QueueSaveNewPackage()
    {
        if (SelectedPackage == null || !SelectedPackage.IsNew) return;
        string name = SelectedPackage.Name.Trim();
        if (string.IsNullOrEmpty(name)) { SetError("Package name is required."); return; }
        _queue.Add(PackageChanges.BuildInsertPackageWithItems(name, PackageItems.ToList()));
        SetQueuedStatus($"{name} with {PackageItems.Count} item(s)");
    }

    [RelayCommand]
    private void QueueRenamePackage()
    {
        if (SelectedPackage == null || SelectedPackage.Id <= 0 || string.IsNullOrWhiteSpace(SelectedPackage.Name)) return;
        _queue.Add(PackageChanges.BuildUpdatePackage(SelectedPackage.Id, SelectedPackage.Name.Trim()));
        SetQueuedStatus(SelectedPackage.Name);
    }

    [RelayCommand]
    private void QueueDeletePackage()
    {
        if (SelectedPackage == null) return;
        if (SelectedPackage.SeasonCount > 0) { SetError("A package referenced by a season cannot be deleted."); return; }
        if (SelectedPackage.Id > 0) _queue.Add(PackageChanges.BuildDeletePackage(SelectedPackage.Id));
        PackageRow row = SelectedPackage;
        SelectedPackage = null;
        Packages.Remove(row);
        SetQueuedStatus(row.Name);
    }

    [RelayCommand]
    private void QueueAddPackageItem()
    {
        if (SelectedPackage == null || SelectedPackageItemChoice == null || NewPackageItemQuantity < 1) return;
        PackageItemPickItem choice = SelectedPackageItemChoice;
        if (SelectedPackage.Id > 0)
            _queue.Add(PackageChanges.BuildInsertPackageItem(SelectedPackage.Id, choice.Definition, NewPackageItemQuantity));
        PackageItems.Add(new PackageItemRow
        {
            Id = 0,
            PackageId = SelectedPackage.Id,
            Definition = choice.Definition,
            DisplayName = choice.DisplayName,
            Quantity = NewPackageItemQuantity,
            IsNew = true,
            SelectedPickItem = choice
        });
        SelectedPackage.ItemCount++;
        if (SelectedPackage.Id > 0)
            SetQueuedStatus(choice.DisplayName);
        else
        {
            StatusIsError = false;
            StatusMessage = "Item added locally. Queue the complete new package when it is ready.";
        }
    }

    [RelayCommand]
    private void QueueRemovePackageItem()
    {
        if (SelectedPackageItem == null) return;
        PackageItemRow row = SelectedPackageItem;
        if (row.Id > 0) _queue.Add(PackageChanges.BuildDeletePackageItem(row.Id));
        PackageItems.Remove(row);
        if (SelectedPackage != null) SelectedPackage.ItemCount = Math.Max(0, SelectedPackage.ItemCount - 1);
        SetQueuedStatus(row.DisplayName);
    }

    [RelayCommand]
    private void StartSeasonWizard()
    {
        Wizard = new SeasonWizardViewModel(_queue, Packages, () =>
        {
            StatusIsError = false;
            StatusMessage = "Queued the complete new-season wizard SQL batch.";
        });
    }

    private async Task LoadSeasonDetailAsync(SeasonRow? season)
    {
        ClearSeasonDetail();
        if (season == null || season.Id <= 0) return;
        IsLoading = true;
        try
        {
            Task<List<SeasonActivityRateRow>> rates = _seasonRepository.LoadActivityRatesAsync(season.Id);
            Task<List<SeasonObjectiveRow>> objectives = _seasonRepository.LoadObjectivesAsync(season.Id);
            Task<List<SeasonTierRow>> tiers = _seasonRepository.LoadTiersAsync(season.Id);
            Task<List<SeasonLeaderboardRewardRow>> rewards = _seasonRepository.LoadLeaderboardRewardsAsync(season.Id);
            Task<int> participants = _seasonRepository.LoadParticipantCountAsync(season.Id);
            Task<int> active = _seasonRepository.LoadActiveLast7DaysAsync(season.Id);
            Task<double> average = _seasonRepository.LoadAvgPointsPerDayAsync(season.Id);
            Task<List<TierDistributionRow>> distribution = _seasonRepository.LoadTierDistributionAsync(season.Id);
            Task<List<LeaderboardEntryRow>> leaderboard = _seasonRepository.LoadTop10LeaderboardAsync(season.Id);
            Task<List<ObjectiveCompletionRow>> completion = _seasonRepository.LoadObjectiveCompletionAsync(season.Id);
            Task<List<TodaysDailyObjectiveRow>> daily = _seasonRepository.LoadTodaysDailyObjectivesAsync(season.Id);
            await Task.WhenAll(rates, objectives, tiers, rewards, participants, active, average,
                distribution, leaderboard, completion, daily);
            Dictionary<SeasonActivityType, SeasonActivityRateRow> ratesByType = rates.Result.ToDictionary(row => row.ActivityType);
            Replace(ActivityRates, Enum.GetValues<SeasonActivityType>().Select(type =>
                ratesByType.TryGetValue(type, out SeasonActivityRateRow? row)
                    ? row
                    : new SeasonActivityRateRow { SeasonId = season.Id, ActivityType = type, UnitScale = 1 }));
            foreach (SeasonObjectiveRow row in objectives.Result)
            {
                row.SelectedPackage = Packages.FirstOrDefault(package => package.Id == row.PackageId);
                row.SelectedEquipmentSet = EquipmentSets.FirstOrDefault(set => set.SetId == row.EquipmentSetId);
                row.InitializeMaterialLists(_oreAndLiquidMaterials, _organicMaterials);
            }
            foreach (SeasonTierRow row in tiers.Result)
            {
                row.SelectedPackage = Packages.FirstOrDefault(package => package.Id == row.PackageId);
                row.SelectedEquipmentSet = EquipmentSets.FirstOrDefault(set => set.SetId == row.EquipmentSetId);
            }
            foreach (SeasonLeaderboardRewardRow row in rewards.Result)
            {
                row.SelectedPackage = Packages.FirstOrDefault(package => package.Id == row.PackageId);
                row.SelectedEquipmentSet = EquipmentSets.FirstOrDefault(set => set.SetId == row.EquipmentSetId);
            }
            Replace(Objectives, objectives.Result);
            Replace(Tiers, tiers.Result);
            Replace(LeaderboardRewards, rewards.Result);
            Replace(TierDistribution, distribution.Result);
            Replace(TopLeaderboard, leaderboard.Result);
            Replace(ObjectiveCompletion, completion.Result);
            Replace(DailyObjectives, daily.Result);
            ParticipantCount = participants.Result;
            ActiveLast7Days = active.Result;
            AveragePointsPerDay = average.Result;
            StatusMessage = $"Loaded season detail for {season.Name}.";
        }
        catch (Exception ex) { SetError($"Unable to load season detail: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void QueueSeasonChanges()
    {
        if (SelectedSeason == null) return;
        if (string.IsNullOrWhiteSpace(SelectedSeason.Name) || SelectedSeason.EndTime <= SelectedSeason.StartTime)
        { SetError("Season requires a name and an end time after its start time."); return; }
        _queue.Add(SelectedSeason.IsNew ? SeasonChanges.BuildInsert(SelectedSeason) : SeasonChanges.BuildUpdate(SelectedSeason));
        SetQueuedStatus(SelectedSeason.Name);
    }

    [RelayCommand]
    private void QueueSeasonActivation()
    {
        if (SelectedSeason == null || SelectedSeason.Id <= 0) return;
        _queue.Add(SelectedSeason.IsActive
            ? SeasonChanges.BuildDeactivate(SelectedSeason.Id)
            : SeasonChanges.BuildActivate(SelectedSeason.Id));
        SelectedSeason.IsActive = !SelectedSeason.IsActive;
        SetQueuedStatus(SelectedSeason.Name);
    }

    [RelayCommand]
    private void AddActivityRate()
    {
        if (SelectedSeason == null) return;
        ActivityRates.Add(new SeasonActivityRateRow
        {
            SeasonId = SelectedSeason.Id,
            ActivityType = NewActivityType,
            PointsPerUnit = 1,
            UnitScale = 1
        });
    }

    [RelayCommand]
    private void QueueSelectedActivityRate()
    {
        if (SelectedActivityRate == null) return;
        if (SelectedSeason != null) SelectedActivityRate.SeasonId = SelectedSeason.Id;
        _queue.Add(SeasonChanges.BuildUpsertActivityRate(SelectedActivityRate));
        SetQueuedStatus(SelectedActivityRate.ActivityTypeLabel);
    }

    [RelayCommand] private void AddObjective() { if (SelectedSeason != null) { var row = new SeasonObjectiveRow { SeasonId = SelectedSeason.Id, Name = "New Objective", TargetValue = 1, IsNew = true, DisplayOrder = Objectives.Count }; row.InitializeMaterialLists(_oreAndLiquidMaterials, _organicMaterials); Objectives.Add(row); SelectedObjective = row; } }
    [RelayCommand] private void QueueSelectedObjective() { if (SelectedObjective != null) { _queue.Add(SelectedObjective.Id > 0 ? SeasonChanges.BuildUpdateObjective(SelectedObjective) : SeasonChanges.BuildInsertObjective(SelectedObjective)); SetQueuedStatus(SelectedObjective.Name); } }
    [RelayCommand] private void QueueRemoveSelectedObjective() { if (SelectedObjective != null) { SeasonObjectiveRow row = SelectedObjective; if (row.Id > 0) _queue.Add(SeasonChanges.BuildDeleteObjective(row)); Objectives.Remove(row); SetQueuedStatus(row.Name); } }
    [RelayCommand] private void AddTier() { if (SelectedSeason != null) Tiers.Add(new SeasonTierRow { SeasonId = SelectedSeason.Id, TierNumber = Tiers.Count + 1, TierName = $"Tier {Tiers.Count + 1}", PointsRequired = (Tiers.Count + 1) * 1000, IsNew = true }); }
    [RelayCommand] private void QueueSelectedTier() { if (SelectedTier != null) { _queue.Add(SelectedTier.Id > 0 ? SeasonChanges.BuildUpdateTier(SelectedTier) : SeasonChanges.BuildInsertTier(SelectedTier)); SetQueuedStatus(SelectedTier.TierName); } }
    [RelayCommand] private void QueueRemoveSelectedTier() { if (SelectedTier != null) { SeasonTierRow row = SelectedTier; if (row.Id > 0) _queue.Add(SeasonChanges.BuildDeleteTier(row)); Tiers.Remove(row); SetQueuedStatus(row.TierName); } }
    [RelayCommand] private void AddLeaderboardReward() { if (SelectedSeason != null) LeaderboardRewards.Add(new SeasonLeaderboardRewardRow { SeasonId = SelectedSeason.Id, RankMin = 1, RankMax = 1, IsNew = true }); }
    [RelayCommand] private void QueueSelectedLeaderboardReward() { if (SelectedLeaderboardReward != null) { if (SelectedLeaderboardReward.RankMin > SelectedLeaderboardReward.RankMax) { SetError("Leaderboard minimum rank cannot exceed maximum rank."); return; } _queue.Add(SelectedLeaderboardReward.Id > 0 ? SeasonChanges.BuildUpdateLeaderboardReward(SelectedLeaderboardReward) : SeasonChanges.BuildInsertLeaderboardReward(SelectedLeaderboardReward)); SetQueuedStatus($"ranks {SelectedLeaderboardReward.RankMin}-{SelectedLeaderboardReward.RankMax}"); } }
    [RelayCommand] private void QueueRemoveSelectedLeaderboardReward() { if (SelectedLeaderboardReward != null) { SeasonLeaderboardRewardRow row = SelectedLeaderboardReward; if (row.Id > 0) _queue.Add(SeasonChanges.BuildDeleteLeaderboardReward(row)); LeaderboardRewards.Remove(row); SetQueuedStatus($"ranks {row.RankMin}-{row.RankMax}"); } }

    private void ClearSeasonDetail()
    {
        ActivityRates.Clear(); Objectives.Clear(); Tiers.Clear(); LeaderboardRewards.Clear();
        DailyObjectives.Clear(); TierDistribution.Clear(); TopLeaderboard.Clear(); ObjectiveCompletion.Clear();
        ParticipantCount = ActiveLast7Days = 0; AveragePointsPerDay = 0;
    }

    [RelayCommand]
    private void RemoveSelectedWizardObjective()
    {
        if (Wizard != null && SelectedWizardObjective != null) Wizard.Objectives.Remove(SelectedWizardObjective);
    }

    [RelayCommand]
    private void RemoveSelectedWizardTier()
    {
        if (Wizard != null && SelectedWizardTier != null) Wizard.Tiers.Remove(SelectedWizardTier);
    }

    [RelayCommand]
    private void RemoveSelectedWizardLeaderboardReward()
    {
        if (Wizard != null && SelectedWizardLeaderboardReward != null)
            Wizard.LeaderboardRewards.Remove(SelectedWizardLeaderboardReward);
    }

    [RelayCommand]
    private async Task ExportSelectedSeasonAsync()
    {
        if (SelectedSeason == null || SelectedSeason.Id <= 0 || _contentExporter == null)
        {
            SetError("Select a saved season first.");
            return;
        }
        IsLoading = true;
        StatusIsError = false;
        StatusMessage = $"Generating a portable export for {SelectedSeason.Name}...";
        try
        {
            ExportScript = await _contentExporter.ExportSeasonAsync(SelectedSeason.Id);
            SelectedSeasonDetailTabIndex = 6;
            StatusMessage = "Export generated and opened in the Export SQL tab.";
        }
        catch (Exception ex) { SetError($"Unable to export season: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private void BuildMaterialLists(IEnumerable<EntityPickItem> entities, IReadOnlyDictionary<string, string> names)
    {
        var oreAndLiquid = new List<MaterialPickItem>();
        var organic = new List<MaterialPickItem>();
        foreach (EntityPickItem entity in entities.Where(entity => entity.Enabled && !entity.Hidden))
        {
            var item = new MaterialPickItem(entity.Definition, names.GetValueOrDefault(entity.Name, entity.Name));
            if (IsCategoryMatch(entity.CategoryFlags, (long)CategoryFlags.cf_ore) ||
                IsCategoryMatch(entity.CategoryFlags, (long)CategoryFlags.cf_liquid))
                oreAndLiquid.Add(item);
            else if (IsCategoryMatch(entity.CategoryFlags, (long)CategoryFlags.cf_organic))
                organic.Add(item);
        }
        _oreAndLiquidMaterials = oreAndLiquid.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        _organicMaterials = organic.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsCategoryMatch(long entityFlags, long category)
    {
        long mask = PackageItemPickItem.CategoryFlagsMask(category);
        return (entityFlags & mask) == category;
    }

    private void SetError(string message) { StatusIsError = true; StatusMessage = message; }
    private void SetQueuedStatus(string label) { StatusIsError = false; StatusMessage = $"Queued package/season changes for {label}."; }
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> rows) { target.Clear(); foreach (T row in rows) target.Add(row); }
}
