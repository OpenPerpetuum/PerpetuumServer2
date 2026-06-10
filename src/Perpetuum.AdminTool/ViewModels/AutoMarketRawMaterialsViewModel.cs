using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketRawMaterialsViewModel : ObservableObject
    {
        private readonly AutoMarketRepository   _repo;
        private readonly ChangeQueue            _queue;
        private readonly TranslationsViewModel? _translations;
        private const int EnglishLangId = 0;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredRows))]
        private bool _showOverridesOnly;

        public ObservableCollection<AutoMarketCoveredMaterialRow> Rows { get; } = new();

        public IEnumerable<AutoMarketCoveredMaterialRow> FilteredRows =>
            _showOverridesOnly ? Rows.Where(r => r.HasOverride) : (IEnumerable<AutoMarketCoveredMaterialRow>)Rows;

        public AutoMarketRawMaterialsViewModel(
            AutoMarketRepository repo,
            ChangeQueue queue,
            TranslationsViewModel? translations = null)
        {
            _repo         = repo;
            _queue        = queue;
            _translations = translations;
        }

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading raw materials...";
            StatusIsError = false;
            try
            {
                var rows  = await _repo.LoadCoveredMaterialsAsync();
                var store = _translations?.Store;

                Rows.Clear();
                foreach (var r in rows)
                {
                    if (store != null)
                    {
                        var tr = store.Rows.FirstOrDefault(x => x.Key == r.DefinitionName);
                        var t  = tr?[EnglishLangId];
                        if (!string.IsNullOrEmpty(t)) r.DisplayName = t;
                    }
                    if (string.IsNullOrEmpty(r.DisplayName)) r.DisplayName = r.DefinitionName;
                    Rows.Add(r);
                }

                OnPropertyChanged(nameof(FilteredRows));
                StatusMessage = $"Loaded {Rows.Count} materials at {DateTime.UtcNow:HH:mm:ss} UTC.";
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

        [RelayCommand]
        private void QueueSave(AutoMarketCoveredMaterialRow row)
        {
            var description = $"automarket_rawmat_overrides: {row.DefinitionName}";
            var existing    = _queue.Items.FirstOrDefault(c => c.Description == description);
            if (existing != null) _queue.Items.Remove(existing);

            string sql;
            if (row.IsAtDefaults)
            {
                sql = $"DELETE FROM automarket_rawmat_overrides WHERE definitionname = {SqlLiteral.Of(row.DefinitionName)}";
            }
            else
            {
                sql =
                    $"MERGE automarket_rawmat_overrides AS t " +
                    $"USING (VALUES ({SqlLiteral.Of(row.DefinitionName)}, {SqlLiteral.OfNullableInt(row.WeeklyCapOverride)}, " +
                    $"{(row.CreateBuyOrders ? 1 : 0)}, {(row.CreateSellOrders ? 1 : 0)})) " +
                    $"AS s (definitionname, weekly_cap_override, create_buy_orders, create_sell_orders) " +
                    $"ON t.definitionname = s.definitionname " +
                    $"WHEN MATCHED THEN UPDATE SET " +
                    $"  weekly_cap_override = s.weekly_cap_override, " +
                    $"  create_buy_orders   = s.create_buy_orders, " +
                    $"  create_sell_orders  = s.create_sell_orders " +
                    $"WHEN NOT MATCHED THEN INSERT (definitionname, weekly_cap_override, create_buy_orders, create_sell_orders) " +
                    $"VALUES (s.definitionname, s.weekly_cap_override, s.create_buy_orders, s.create_sell_orders);";
            }

            _queue.Add(new RawSqlChange(description, sql));
            row.OriginalCapOverride = row.WeeklyCapOverride;
            row.OriginalBuyOrders   = row.CreateBuyOrders;
            row.OriginalSellOrders  = row.CreateSellOrders;
            StatusMessage = $"{row.DisplayName} queued.";
            OnPropertyChanged(nameof(FilteredRows));
        }
    }
}
