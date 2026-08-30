using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Economy;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EconomySinkEffectivenessViewModel : ObservableObject
    {
        private readonly EconomySinkRepository _repo;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;
        [ObservableProperty] private int    _activePlayerCount;
        [ObservableProperty] private double _insuranceCoveragePct;

        public ObservableCollection<EconomySinkRow> SinkRows { get; } = new();

        public EconomySinkEffectivenessViewModel(EconomySinkRepository repo) => _repo = repo;

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading...";
            StatusIsError = false;
            try
            {
                var data = await _repo.LoadAsync();

                ActivePlayerCount    = data.ActivePlayerCount;
                InsuranceCoveragePct = data.InsuranceCoveragePct;

                SinkRows.Clear();
                foreach (var r in data.SinkRows) SinkRows.Add(r);

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
