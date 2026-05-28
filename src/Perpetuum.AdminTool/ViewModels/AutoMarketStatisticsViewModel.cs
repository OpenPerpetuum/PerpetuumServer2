using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketStatisticsViewModel : ObservableObject
    {
        private readonly AutoMarketRepository _repo;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public ObservableCollection<AutoMarketNicFlowRow>      NicFlow         { get; } = new();
        public ObservableCollection<AutoMarketPricingTraceRow> PricingTrace    { get; } = new();
        public ObservableCollection<AutoMarketGatherRow>       GatherBreakdown { get; } = new();

        public AutoMarketStatisticsViewModel(AutoMarketRepository repo) => _repo = repo;

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading statistics...";
            StatusIsError = false;
            try
            {
                var nicTask    = _repo.LoadNicFlowAsync();
                var priceTask  = _repo.LoadPricingTraceAsync();
                var gatherTask = _repo.LoadGatherBreakdownAsync();
                await Task.WhenAll(nicTask, priceTask, gatherTask);

                NicFlow.Clear();
                foreach (var r in nicTask.Result) NicFlow.Add(r);
                PricingTrace.Clear();
                foreach (var r in priceTask.Result) PricingTrace.Add(r);
                GatherBreakdown.Clear();
                foreach (var r in gatherTask.Result) GatherBreakdown.Add(r);

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
