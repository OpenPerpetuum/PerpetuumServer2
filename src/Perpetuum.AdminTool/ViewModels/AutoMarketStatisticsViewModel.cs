using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketStatisticsViewModel : ObservableObject
    {
        private readonly AutoMarketRepository   _repo;
        private readonly TranslationsViewModel? _translations;
        private const int EnglishLangId = 0;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public ObservableCollection<AutoMarketNicFlowRow>      NicFlow         { get; } = new();
        public ObservableCollection<AutoMarketPricingTraceRow> PricingTrace    { get; } = new();
        public ObservableCollection<AutoMarketGatherRow>       GatherBreakdown { get; } = new();

        public AutoMarketStatisticsViewModel(AutoMarketRepository repo, TranslationsViewModel? translations = null)
        {
            _repo         = repo;
            _translations = translations;
        }

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

                var store = _translations?.Store;

                NicFlow.Clear();
                foreach (var r in nicTask.Result) NicFlow.Add(r);

                PricingTrace.Clear();
                foreach (var r in priceTask.Result)
                {
                    r.DisplayName = Translate(store, r.ResourceName);
                    PricingTrace.Add(r);
                }

                GatherBreakdown.Clear();
                foreach (var r in gatherTask.Result)
                {
                    r.DisplayName = Translate(store, r.ResourceName);
                    GatherBreakdown.Add(r);
                }

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

        private string Translate(TranslationStore? store, string defName)
        {
            if (store == null) return defName;
            var t = store.Rows.FirstOrDefault(x => x.Key == defName)?[EnglishLangId];
            return string.IsNullOrEmpty(t) ? defName : t;
        }
    }
}
