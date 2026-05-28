using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketViewModel : ObservableObject
    {
        private readonly AutoMarketRepository _repo;

        [ObservableProperty] private bool   _isRefreshing;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public AutoMarketConfigViewModel     Config     { get; }
        public AutoMarketTradeListViewModel  TradeList  { get; }
        public AutoMarketStatisticsViewModel Statistics { get; }
        public AutoMarketOrdersViewModel     Orders     { get; }

        public AutoMarketViewModel(
            AutoMarketRepository repo,
            ChangeQueue queue,
            LookupCache lookups,
            TranslationsViewModel? translations = null)
        {
            _repo      = repo;
            Config     = new AutoMarketConfigViewModel(repo, queue);
            TradeList  = new AutoMarketTradeListViewModel(repo, queue, lookups, translations);
            Statistics = new AutoMarketStatisticsViewModel(repo);
            Orders     = new AutoMarketOrdersViewModel(repo, translations);
        }

        public async Task LoadAsync()
        {
            await Task.WhenAll(Config.LoadAsync(), TradeList.LoadAsync());
        }

        [RelayCommand(CanExecute = nameof(CanRefreshNow))]
        private async Task RefreshNow()
        {
            IsRefreshing  = true;
            StatusIsError = false;
            StatusMessage = "Refreshing AutoMarket orders...";
            try
            {
                await _repo.RefreshNowAsync();
                StatusMessage = $"Refresh complete at {DateTime.UtcNow:HH:mm:ss} UTC.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Refresh failed: {ex.Message}";
            }
            finally { IsRefreshing = false; }
        }

        private bool CanRefreshNow() => !IsRefreshing;
        partial void OnIsRefreshingChanged(bool value) => RefreshNowCommand.NotifyCanExecuteChanged();
    }
}
