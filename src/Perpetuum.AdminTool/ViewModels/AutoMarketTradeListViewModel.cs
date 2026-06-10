using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Translations;
using Perpetuum.AdminTool.Views;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketTradeListViewModel : ObservableObject
    {
        private readonly AutoMarketRepository  _repo;
        private readonly ChangeQueue           _queue;
        private readonly LookupCache           _lookups;
        private readonly TranslationsViewModel? _translations;
        private const int EnglishLangId = 0;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public ObservableCollection<AutoMarketTradeListRow>   Rows             { get; } = new();
        public ObservableCollection<AutoMarketRawMaterialRow> DerivedMaterials { get; } = new();

        public AutoMarketTradeListViewModel(
            AutoMarketRepository repo,
            ChangeQueue queue,
            LookupCache lookups,
            TranslationsViewModel? translations)
        {
            _repo         = repo;
            _queue        = queue;
            _lookups      = lookups;
            _translations = translations;
        }

        public async Task LoadAsync()
        {
            IsLoading     = true;
            StatusMessage = "";
            StatusIsError = false;
            try
            {
                var store = _translations?.Store;
                var rows  = await _repo.LoadTradeListAsync();
                Rows.Clear();
                foreach (var r in rows)
                {
                    if (store != null)
                    {
                        var tr = store.Rows.FirstOrDefault(x => x.Key == r.DefinitionName);
                        var t  = tr?[EnglishLangId];
                        if (!string.IsNullOrEmpty(t)) r.DisplayName = t;
                    }
                    Rows.Add(r);
                }
                await RefreshDerivedAsync();
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        private async Task RefreshDerivedAsync()
        {
            try
            {
                var mats = await _repo.LoadDerivedMaterialsAsync();
                DerivedMaterials.Clear();
                foreach (var m in mats) DerivedMaterials.Add(m);
            }
            catch { /* non-fatal — sub-panel stays empty */ }
        }

        [RelayCommand]
        private void QueueSave(AutoMarketTradeListRow row)
        {
            var description = $"market_orders_configuration: update {row.DefinitionName}";
            var existing    = _queue.Items.FirstOrDefault(c => c.Description == description);
            if (existing != null) _queue.Items.Remove(existing);
            _queue.Add(new RawSqlChange(
                description,
                $"UPDATE market_orders_configuration " +
                $"SET amount = {SqlLiteral.Of(row.Amount)}, " +
                $"create_sell_orders = {SqlLiteral.Of(row.CreateSellOrders)}, " +
                $"create_buyback_orders = {SqlLiteral.Of(row.CreateBuybackOrders)} " +
                $"WHERE definitionname = {SqlLiteral.Of(row.DefinitionName)}"));
            row.OriginalAmount              = row.Amount;
            row.OriginalCreateSellOrders    = row.CreateSellOrders;
            row.OriginalCreateBuybackOrders = row.CreateBuybackOrders;
            StatusMessage = $"{row.DisplayName} amount queued.";
        }

        [RelayCommand]
        private void Remove(AutoMarketTradeListRow row)
        {
            var msg = $"Remove '{row.DisplayName}' from the trade list?\n\n" +
                      "AutoMarket will no longer place orders for this item.";
            if (MessageBox.Show(msg, "Remove item",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                != MessageBoxResult.Yes) return;

            // Cancel any pending save for this row
            var saveDesc = $"market_orders_configuration: update {row.DefinitionName}";
            var existing = _queue.Items.FirstOrDefault(c => c.Description == saveDesc);
            if (existing != null) _queue.Items.Remove(existing);

            _queue.Add(new RawSqlChange(
                $"market_orders_configuration: delete {row.DefinitionName}",
                $"DELETE FROM market_orders_configuration WHERE definitionname = {SqlLiteral.Of(row.DefinitionName)}",
                isDestructive: true));
            Rows.Remove(row);
            StatusMessage = $"'{row.DisplayName}' queued for removal.";
        }

        public void AddItem(Window owner)
        {
            var existing = Rows.Select(r => r.DefinitionName).ToHashSet();
            var vm  = new AddAutoMarketItemViewModel(_lookups, _translations, existing);
            var win = new AddAutoMarketItemWindow(vm) { Owner = owner };
            if (win.ShowDialog() != true || vm.SelectedItem == null) return;

            var item = vm.SelectedItem;
            _queue.Add(new RawSqlChange(
                $"market_orders_configuration: insert {item.DefinitionName}",
                $"INSERT INTO market_orders_configuration (definitionname, amount) " +
                $"VALUES ({SqlLiteral.Of(item.DefinitionName)}, 1)"));

            Rows.Add(new AutoMarketTradeListRow
            {
                DefinitionName              = item.DefinitionName,
                DisplayName                 = item.DisplayName,
                Amount                      = 1,
                OriginalAmount              = 1,
                CreateSellOrders            = true,
                OriginalCreateSellOrders    = true,
                CreateBuybackOrders         = true,
                OriginalCreateBuybackOrders = true,
            });
            StatusMessage = $"'{item.DisplayName}' queued for insert.";
        }
    }
}
