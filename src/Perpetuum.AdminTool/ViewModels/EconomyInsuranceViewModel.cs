using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EconomyInsuranceViewModel : ObservableObject
    {
        private readonly EconomyInsuranceRepository _repo;
        private readonly ChangeQueue                _queue;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private bool   _isRecalculating;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;
        [ObservableProperty] private bool   _showSinkWarning;

        public ObservableCollection<InsuranceConfigRow> ConfigRows { get; } = new();
        public ObservableCollection<InsurancePriceRow>  PriceRows  { get; } = new();

        public EconomyInsuranceViewModel(EconomyInsuranceRepository repo, ChangeQueue queue)
        {
            _repo  = repo;
            _queue = queue;
        }

        public async Task LoadAsync()
        {
            IsLoading     = true;
            StatusMessage = "";
            StatusIsError = false;
            try
            {
                var config = await _repo.LoadConfigAsync();
                var prices = await _repo.LoadPricesAsync();

                ConfigRows.Clear();
                foreach (var r in config) ConfigRows.Add(r);

                PriceRows.Clear();
                foreach (var r in prices) PriceRows.Add(r);

                UpdateSinkWarning();
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private void QueueSave(InsuranceConfigRow row)
        {
            var description = $"insurance_config: update {row.ParamName}";
            var existing    = _queue.Items.FirstOrDefault(c => c.Description == description);
            if (existing != null) _queue.Items.Remove(existing);
            _queue.Add(new RawSqlChange(
                description,
                $"UPDATE insurance_config SET param_value = {SqlLiteral.Of(row.ParamValue)} " +
                $"WHERE param_name = {SqlLiteral.Of(row.ParamName)}"));
            row.OriginalValue = row.ParamValue;
            StatusMessage = $"{row.Label} queued.";
            UpdateSinkWarning();
        }

        [RelayCommand(CanExecute = nameof(CanRecalculate))]
        private async Task RecalculateNowAsync()
        {
            IsRecalculating = true;
            StatusIsError   = false;
            StatusMessage   = "Recalculating insurance prices...";
            try
            {
                await _repo.RecalculateAsync();
                var prices = await _repo.LoadPricesAsync();
                PriceRows.Clear();
                foreach (var r in prices) PriceRows.Add(r);
                StatusMessage = $"Prices recalculated at {DateTime.UtcNow:HH:mm:ss} UTC.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Recalculate failed: {ex.Message}";
            }
            finally { IsRecalculating = false; }
        }

        private bool CanRecalculate() => !IsRecalculating && !IsLoading;

        partial void OnIsRecalculatingChanged(bool value) => RecalculateNowCommand.NotifyCanExecuteChanged();
        partial void OnIsLoadingChanged(bool value)       => RecalculateNowCommand.NotifyCanExecuteChanged();

        private void UpdateSinkWarning()
        {
            var feePct    = ConfigRows.FirstOrDefault(r => r.ParamName == "fee_pct")?.ParamValue    ?? 0;
            var payoutPct = ConfigRows.FirstOrDefault(r => r.ParamName == "payout_pct")?.ParamValue ?? 0;
            ShowSinkWarning = payoutPct >= feePct;
        }
    }
}
