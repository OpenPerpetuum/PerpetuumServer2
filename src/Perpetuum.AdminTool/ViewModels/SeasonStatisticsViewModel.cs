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
        private int _seasonId;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private int _totalParticipants;
        [ObservableProperty] private int _activeLast7Days;
        [ObservableProperty] private double _avgPointsPerDay;
        [ObservableProperty] private string _retentionRate = "—";

        public ObservableCollection<TierDistributionRow> TierDistribution { get; } = new();
        public ObservableCollection<LeaderboardEntryRow> Top10 { get; } = new();
        public ObservableCollection<ObjectiveCompletionRow> ObjectiveCompletion { get; } = new();

        public SeasonStatisticsViewModel(SeasonRepository repo)
        {
            _repo = repo;
        }

        public async Task LoadAsync(int seasonId)
        {
            _seasonId = seasonId;
            IsLoading = true;
            try
            {
                TotalParticipants = await _repo.LoadParticipantCountAsync(seasonId);
                ActiveLast7Days = await _repo.LoadActiveLast7DaysAsync(seasonId);
                RetentionRate = TotalParticipants == 0 ? "—"
                    : $"{(ActiveLast7Days * 100.0 / TotalParticipants):F1}%";
                AvgPointsPerDay = await _repo.LoadAvgPointsPerDayAsync(seasonId);

                TierDistribution.Clear();
                foreach (var r in await _repo.LoadTierDistributionAsync(seasonId)) TierDistribution.Add(r);

                Top10.Clear();
                foreach (var r in await _repo.LoadTop10LeaderboardAsync(seasonId)) Top10.Add(r);

                ObjectiveCompletion.Clear();
                foreach (var r in await _repo.LoadObjectiveCompletionAsync(seasonId)) ObjectiveCompletion.Add(r);
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private async Task Refresh() => await LoadAsync(_seasonId);
    }
}
