using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Economy;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EconomyMoneySupplyViewModel : ObservableObject
    {
        private readonly EconomyMoneySupplyRepository _repo;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;
        [ObservableProperty] private long   _totalNic;
        [ObservableProperty] private long   _medianNic;
        [ObservableProperty] private double _top1PctShare;
        [ObservableProperty] private long   _idleNic;

        public ObservableCollection<EconomySnapshotRow> SnapshotRows { get; } = new();
        public ObservableCollection<EconomyWealthRow>   Top10Rows    { get; } = new();

        public EconomyMoneySupplyViewModel(EconomyMoneySupplyRepository repo) => _repo = repo;

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading...";
            StatusIsError = false;
            try
            {
                var data = await _repo.LoadAsync();

                TotalNic     = data.TotalNic;
                MedianNic    = data.MedianNic;
                Top1PctShare = data.Top1PctShare;
                IdleNic      = data.IdleNic;

                SnapshotRows.Clear();
                foreach (var r in data.SnapshotRows) SnapshotRows.Add(r);

                Top10Rows.Clear();
                foreach (var r in data.Top10Rows) Top10Rows.Add(r);

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
    }
}
