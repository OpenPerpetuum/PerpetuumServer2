using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketConfigViewModel : ObservableObject
    {
        private readonly AutoMarketRepository _repo;
        private readonly ChangeQueue _queue;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public ObservableCollection<AutoMarketConfigRow> Rows { get; } = new();

        public AutoMarketConfigViewModel(AutoMarketRepository repo, ChangeQueue queue)
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
                var rows = await _repo.LoadConfigAsync();
                Rows.Clear();
                foreach (var r in rows) Rows.Add(r);
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private void QueueSave(AutoMarketConfigRow row)
        {
            var description = $"automarket_config: update {row.ParamName}";
            var existing    = _queue.Items.FirstOrDefault(c => c.Description == description);
            if (existing != null) _queue.Items.Remove(existing);
            _queue.Add(new RawSqlChange(
                description,
                $"UPDATE automarket_config SET param_value = {SqlLiteral.Of(row.ParamValue)} " +
                $"WHERE param_name = {SqlLiteral.Of(row.ParamName)}"));
            row.OriginalValue = row.ParamValue;
            StatusMessage = $"{row.Label} queued.";
        }
    }
}
