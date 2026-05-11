using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
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

        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _description = "";
        [ObservableProperty] private DateTime _startTime = DateTime.UtcNow.Date;
        [ObservableProperty] private DateTime _endTime = DateTime.UtcNow.Date.AddDays(30);
        [ObservableProperty] private string _startTimeText = "00:00";
        [ObservableProperty] private string _endTimeText = "00:00";

        public ObservableCollection<SeasonActivityRateRow> ActivityRates { get; } = new();
        public ObservableCollection<SeasonObjectiveRow> Objectives { get; } = new();
        public ObservableCollection<SeasonTierRow> Tiers { get; } = new();
        public ObservableCollection<SeasonLeaderboardRewardRow> LeaderboardRewards { get; } = new();

        public ObservableCollection<PackageRow> Packages => _packages;
        public bool HasPackages => _packages.Count > 0;

        // Fix 2: only show activity types that have been enabled (PointsPerUnit > 0) in Step 2
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
        public bool HasLeaderboardRewards => LeaderboardRewards.Count > 0;

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

        public IReadOnlyList<ActivityTypeOption> ObjectiveActivityTypeOptions { get; } =
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

        public string Step1Validation { get; private set; } = "";
        public string FinishHint => "Queues the season (Draft) with all configured rates, objectives, tiers, and rewards in one SQL batch. Activate via season detail after committing.";

        public SeasonWizardViewModel(
            ChangeQueue queue,
            ObservableCollection<PackageRow> packages,
            Action onComplete)
        {
            _queue = queue;
            _packages = packages;
            _onComplete = onComplete;

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
                OnPropertyChanged(nameof(HasLeaderboardRewards));
                OnPropertyChanged(nameof(ReviewLeaderboardHeader));
                OnPropertyChanged(nameof(ReviewLeaderboardLines));
            };
        }

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
                OnPropertyChanged(nameof(HasActiveRates));
                OnPropertyChanged(nameof(ReviewObjectivesHeader));
                OnPropertyChanged(nameof(ReviewObjectiveLines));
                OnPropertyChanged(nameof(ReviewTiersHeader));
                OnPropertyChanged(nameof(ReviewTierLines));
                OnPropertyChanged(nameof(ReviewLeaderboardHeader));
                OnPropertyChanged(nameof(ReviewLeaderboardLines));
            }
        }

        partial void OnNameChanged(string value) => ValidateStep1();
        partial void OnStartTimeChanged(DateTime value) => ValidateStep1();
        partial void OnEndTimeChanged(DateTime value) => ValidateStep1();
        partial void OnStartTimeTextChanged(string value) => ApplyTimeText(value, isStart: true);
        partial void OnEndTimeTextChanged(string value)   => ApplyTimeText(value, isStart: false);

        private void ApplyTimeText(string text, bool isStart)
        {
            if (TryParseHHmm(text, out var ts))
            {
                if (isStart) StartTime = StartTime.Date + ts;
                else         EndTime   = EndTime.Date   + ts;
            }
            ValidateStep1();
        }

        private static bool TryParseHHmm(string text, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Trim().Split(':');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return false;
            if (h < 0 || h > 23 || m < 0 || m > 59) return false;
            result = new TimeSpan(h, m, 0);
            return true;
        }

        private void ValidateStep1()
        {
            if (string.IsNullOrWhiteSpace(Name))
                Step1Validation = "Season name is required.";
            else if (!TryParseHHmm(StartTimeText, out _))
                Step1Validation = "Start time must be in HH:mm format (UTC).";
            else if (!TryParseHHmm(EndTimeText, out _))
                Step1Validation = "End time must be in HH:mm format (UTC).";
            else if (EndTime <= StartTime)
                Step1Validation = "End time must be after start time.";
            else
                Step1Validation = "";
            OnPropertyChanged(nameof(Step1Validation));
        }

        [RelayCommand]
        private void Back() { if (CanGoBack) CurrentStep--; }

        [RelayCommand]
        private void Next()
        {
            if (CurrentStep == 1)
            {
                ValidateStep1();
                if (!string.IsNullOrEmpty(Step1Validation)) return;
            }
            if (CanGoNext) CurrentStep++;
        }

        [RelayCommand]
        private void AddObjectiveRow()
        {
            Objectives.Add(new SeasonObjectiveRow
            {
                SeasonId = 0,
                Name = "New Objective",
                Description = "",
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
            var pkg = _packages.Count > 0 ? _packages[0] : null;
            var row = new SeasonTierRow
            {
                SeasonId       = 0,
                TierNumber     = Tiers.Count + 1,
                TierName       = $"Tier {Tiers.Count + 1}",
                PointsRequired = (Tiers.Count + 1) * 1000,
                PackageId      = pkg?.Id ?? 0,
                IsNew          = true
            };
            row.SelectedPackage = pkg;
            Tiers.Add(row);
        }

        [RelayCommand]
        private void RemoveTierRow(SeasonTierRow? row)
        {
            if (row != null) Tiers.Remove(row);
        }

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

        [RelayCommand]
        private void RemoveLeaderboardRow(SeasonLeaderboardRewardRow? row)
        {
            if (row != null) LeaderboardRewards.Remove(row);
        }

        [RelayCommand]
        private void Finish()
        {
            ValidateStep1();
            if (!string.IsNullOrEmpty(Step1Validation)) return;
            _queue.Add(BuildSeasonScript());
            _onComplete();
        }

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
    }
}
