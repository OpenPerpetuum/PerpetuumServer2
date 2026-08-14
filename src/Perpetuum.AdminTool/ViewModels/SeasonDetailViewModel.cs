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
using Perpetuum.AdminTool.Export;
using Perpetuum.AdminTool.Views;
using Perpetuum.AdminTool.EquipmentSets;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Seasons;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Translations;
using Perpetuum.ExportedTypes;
using Perpetuum.Services.Seasons;
using SeasonRepository = Perpetuum.AdminTool.Seasons.SeasonRepository;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class SeasonDetailViewModel : ObservableObject
    {
        private readonly SeasonRepository _repo;
        private readonly PackageRepository _pkgRepo;
        private readonly ChangeQueue _queue;
        private readonly LookupCache _cache;
        private readonly ConnectionSettings _connection;
        private readonly TranslationsViewModel? _translations;
        private IReadOnlyList<MaterialPickItem> _oreAndLiquidMaterials = Array.Empty<MaterialPickItem>();
        private IReadOnlyList<MaterialPickItem> _organicMaterials = Array.Empty<MaterialPickItem>();

        [ObservableProperty] private SeasonRow _season;
        [ObservableProperty] private int _selectedTabIndex;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;

        public ObservableCollection<SeasonActivityRateRow> ActivityRates { get; } = new();
        public ObservableCollection<SeasonObjectiveRow> Objectives { get; } = new();
        public ObservableCollection<SeasonTierRow> Tiers { get; } = new();
        public ObservableCollection<SeasonLeaderboardRewardRow> LeaderboardRewards { get; } = new();

        public ObservableCollection<PackageRow> Packages { get; }
        public IReadOnlyList<EquipmentSetRow> EquipmentSets { get; private set; } =
            Array.Empty<EquipmentSetRow>();
        public PackagesViewModel PackagesVm { get; }
        public SeasonStatisticsViewModel StatisticsVm { get; }

        public IReadOnlyList<ActivityTypeOption> ActivityTypeOptions { get; } =
            new[]
            {
                new ActivityTypeOption(SeasonActivityType.NpcKill,                 "NPC Kill"),
                new ActivityTypeOption(SeasonActivityType.PvpKill,                 "PvP Kill"),
                new ActivityTypeOption(SeasonActivityType.MissionComplete,         "Mission Complete"),
                new ActivityTypeOption(SeasonActivityType.MineralMined,            "Mineral Mined"),
                new ActivityTypeOption(SeasonActivityType.EpSpent,                 "EP Spent"),
                new ActivityTypeOption(SeasonActivityType.EpEarned,                "EP Earned"),
                new ActivityTypeOption(SeasonActivityType.NicEarned,               "NIC Earned"),
                new ActivityTypeOption(SeasonActivityType.NicSpent,                "NIC Spent"),
                new ActivityTypeOption(SeasonActivityType.IntrusionPoint,          "Intrusion Point"),
                new ActivityTypeOption(SeasonActivityType.PlantHarvested,          "Plant Harvested"),
                new ActivityTypeOption(SeasonActivityType.Prototyping,             "Prototyping"),
                new ActivityTypeOption(SeasonActivityType.ReverseEngineering,      "Reverse Engineering"),
                new ActivityTypeOption(SeasonActivityType.Production,              "Production"),
                new ActivityTypeOption(SeasonActivityType.ArtifactFound,           "Artifact Found"),
                new ActivityTypeOption(SeasonActivityType.DamageDone,              "Damage Done"),
                new ActivityTypeOption(SeasonActivityType.DamageReceived,          "Damage Received"),
                new ActivityTypeOption(SeasonActivityType.ArmorRestored,           "Armor Restored"),
                new ActivityTypeOption(SeasonActivityType.EnergyDrainDealt,        "Energy Drain Dealt"),
                new ActivityTypeOption(SeasonActivityType.EnergyDrainReceived,     "Energy Drain Received"),
                new ActivityTypeOption(SeasonActivityType.EnergyTransferDealt,     "Energy Transfer Dealt"),
                new ActivityTypeOption(SeasonActivityType.EnergyTransferReceived,  "Energy Transfer Received"),
            };

        public IReadOnlyList<ObjectiveFilterOption> ObjectiveFilterOptions { get; } = new[]
        {
            new ObjectiveFilterOption(ObjectiveFilterMode.All,     "All"),
            new ObjectiveFilterOption(ObjectiveFilterMode.OneTime, "One-time only"),
            new ObjectiveFilterOption(ObjectiveFilterMode.Daily,   "Daily only"),
        };

        public IReadOnlyList<ScoringModeOption> ScoringModeOptions { get; } = new[]
        {
            new ScoringModeOption(SeasonScoringMode.ActivityAndGlobal, "Activity + Global Score"),
            new ScoringModeOption(SeasonScoringMode.ObjectivesOnly,    "Objectives Only"),
        };

        [ObservableProperty]
        private ObjectiveFilterMode _objectiveFilter = ObjectiveFilterMode.All;

        partial void OnObjectiveFilterChanged(ObjectiveFilterMode value) =>
            OnPropertyChanged(nameof(FilteredObjectives));

        public IEnumerable<SeasonObjectiveRow> FilteredObjectives => ObjectiveFilter switch
        {
            ObjectiveFilterMode.OneTime => Objectives.Where(o => !o.IsDaily),
            ObjectiveFilterMode.Daily   => Objectives.Where(o => o.IsDaily),
            _                           => Objectives,
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
            ObservableCollection<PackageRow> packages,
            TranslationsViewModel? translations = null)
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
            _translations = translations;
            Objectives.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (SeasonObjectiveRow r in e.NewItems)
                        r.PropertyChanged += OnObjectivePropertyChanged;
                if (e.OldItems != null)
                    foreach (SeasonObjectiveRow r in e.OldItems)
                        r.PropertyChanged -= OnObjectivePropertyChanged;
                OnPropertyChanged(nameof(FilteredObjectives));
            };
        }

        private void OnObjectivePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SeasonObjectiveRow.IsDaily))
                OnPropertyChanged(nameof(FilteredObjectives));
        }

        private void BuildMaterialLists(TranslationsViewModel? translations)
        {
            const int EnglishLangId = 0;
            Dictionary<string, string>? englishNames = null;
            if (translations?.Store?.Rows != null)
            {
                englishNames = translations.Store.Rows
                    .GroupBy(r => r.Key)
                    .ToDictionary(g => g.Key, g => g.First()[EnglishLangId]);
            }

            var oreAndLiquid = new List<MaterialPickItem>();
            var organic = new List<MaterialPickItem>();

            foreach (var e in _cache.Entities)
            {
                if (!e.Enabled || e.Hidden) continue;

                var displayName = (englishNames != null &&
                                   englishNames.TryGetValue(e.Name, out var eng) &&
                                   !string.IsNullOrEmpty(eng))
                    ? eng : e.Name;

                if (IsCategoryMatch(e.CategoryFlags, (long)CategoryFlags.cf_ore) ||
                    IsCategoryMatch(e.CategoryFlags, (long)CategoryFlags.cf_liquid))
                    oreAndLiquid.Add(new MaterialPickItem(e.Definition, displayName));
                else if (IsCategoryMatch(e.CategoryFlags, (long)CategoryFlags.cf_organic))
                    organic.Add(new MaterialPickItem(e.Definition, displayName));
            }

            _oreAndLiquidMaterials = oreAndLiquid
                .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _organicMaterials = organic
                .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsCategoryMatch(long entityFlags, long category)
        {
            var mask = PackageItemPickItem.CategoryFlagsMask(category);
            return (entityFlags & mask) == category;
        }

        public async Task LoadAsync()
        {
            try
            {
                BuildMaterialLists(_translations);
                EquipmentSets = await _repo.LoadEquipmentSetsAsync();
                OnPropertyChanged(nameof(EquipmentSets));
                StatusIsError = false;
                StatusMessage = "Loading season detail...";

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
                    {
                        if (o.PackageId.HasValue)
                            o.SelectedPackage = Packages.FirstOrDefault(p => p.Id == o.PackageId);
                        if (o.EquipmentSetId.HasValue)
                            o.SelectedEquipmentSet = EquipmentSets.FirstOrDefault(s => s.SetId == o.EquipmentSetId);
                        o.InitializeMaterialLists(_oreAndLiquidMaterials, _organicMaterials);
                        Objectives.Add(o);
                    }

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
                SeasonId     = Season.Id,
                Name         = "New Objective",
                Description  = "",
                ActivityType = SeasonActivityType.NpcKill,
                TargetValue  = 1,
                BonusPoints  = 0,
                DisplayOrder = Objectives.Count,
                IsNew        = true,
                IsDaily      = ObjectiveFilter == ObjectiveFilterMode.Daily,
            };
            row.InitializeMaterialLists(_oreAndLiquidMaterials, _organicMaterials);
            Objectives.Add(row);
            StatusIsError = false;
            StatusMessage = "Added objective row. Edit fields, then click 'Queue Save' on the row.";
        }

        [RelayCommand]
        private void RemoveObjective(SeasonObjectiveRow? row)
        {
            if (row == null) return;
            var ok = MessageBox.Show($"Remove objective '{row.Name}'?",
                "Remove objective", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            if (row.Id > 0) _queue.Add(SeasonChanges.BuildDeleteObjective(row));
            Objectives.Remove(row);
            StatusIsError = false;
            StatusMessage = row.Id > 0
                ? $"Queued DELETE for objective id {row.Id}."
                : "Removed unsaved objective.";
        }

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

        [RelayCommand]
        private void RemoveTier(SeasonTierRow? row)
        {
            if (row == null) return;
            var ok = MessageBox.Show($"Remove tier '{row.TierName}'?",
                "Remove tier", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            if (row.Id > 0) _queue.Add(SeasonChanges.BuildDeleteTier(row));
            Tiers.Remove(row);
            StatusIsError = false;
            StatusMessage = row.Id > 0
                ? $"Queued DELETE for tier id {row.Id}."
                : "Removed unsaved tier.";
        }

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
            if (row.RankMin > row.RankMax)
            {
                StatusIsError = true;
                StatusMessage = $"Invalid rank range: min ({row.RankMin}) must not be greater than max ({row.RankMax}).";
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

        [RelayCommand]
        private void RemoveLeaderboardReward(SeasonLeaderboardRewardRow? row)
        {
            if (row == null) return;
            var ok = MessageBox.Show($"Remove leaderboard reward (ranks {row.RankMin}-{row.RankMax})?",
                "Remove leaderboard reward", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            if (row.Id > 0) _queue.Add(SeasonChanges.BuildDeleteLeaderboardReward(row));
            LeaderboardRewards.Remove(row);
            StatusIsError = false;
            StatusMessage = row.Id > 0
                ? $"Queued DELETE for leaderboard reward id {row.Id}."
                : "Removed unsaved leaderboard reward.";
        }

        [ObservableProperty] private bool _isExporting;
        partial void OnIsExportingChanged(bool _) => ExportCommand.NotifyCanExecuteChanged();

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportAsync()
        {
            IsExporting   = true;
            StatusMessage = "Generating export script...";
            StatusIsError = false;
            try
            {
                var script = await SeasonExporter.ExportAsync(Season.Id, _connection);
                var vm     = new ExportScriptViewModel($"Season: {Season.Name}", script);
                var win    = new ExportScriptWindow(vm) { Owner = System.Windows.Application.Current?.MainWindow };
                win.ShowDialog();
                StatusMessage = "Export complete.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Export failed: {ex.Message}";
            }
            finally
            {
                IsExporting = false;
            }
        }

        private bool CanExport() => !IsExporting;
    }

    public enum ObjectiveFilterMode { All, OneTime, Daily }
    public record ObjectiveFilterOption(ObjectiveFilterMode Value, string Label);
}
