using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Economy;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EconomyViewModel : ObservableObject
    {
        private readonly EconomyRepository _repo;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public ObservableCollection<EconomyNicFlowRow> NicIn  { get; } = new();
        public ObservableCollection<EconomyNicFlowRow> NicOut { get; } = new();

        // Net balance computed from non-total category rows to avoid double-counting the Total row
        public long NetToday      => TotalIn(r => r.Today)      - TotalOut(r => r.Today);
        public long NetLast7Days  => TotalIn(r => r.Last7Days)  - TotalOut(r => r.Last7Days);
        public long NetLast30Days => TotalIn(r => r.Last30Days) - TotalOut(r => r.Last30Days);
        public long NetAllTime    => TotalIn(r => r.AllTime)    - TotalOut(r => r.AllTime);

        public EconomyViewModel(EconomyRepository repo)
        {
            _repo = repo;
        }

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading...";
            StatusIsError = false;
            try
            {
                var (nicIn, nicOut) = await _repo.LoadNicFlowAsync();

                NicIn.Clear();
                foreach (var row in nicIn) NicIn.Add(row);

                NicOut.Clear();
                foreach (var row in nicOut) NicOut.Add(row);

                OnPropertyChanged(nameof(NetToday));
                OnPropertyChanged(nameof(NetLast7Days));
                OnPropertyChanged(nameof(NetLast30Days));
                OnPropertyChanged(nameof(NetAllTime));

                StatusMessage = $"Loaded at {DateTime.UtcNow:HH:mm:ss} UTC.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        private bool CanRefresh() => !IsLoading;
        partial void OnIsLoadingChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();

        private long TotalIn(Func<EconomyNicFlowRow, long> sel)
            => NicIn.Where(r => !r.IsTotal).Sum(sel);

        private long TotalOut(Func<EconomyNicFlowRow, long> sel)
            => NicOut.Where(r => !r.IsTotal).Sum(sel);
    }
}
