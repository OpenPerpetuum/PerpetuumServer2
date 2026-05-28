using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketOrdersViewModel : ObservableObject
    {
        private static readonly HashSet<int> PlasmaIds = new() { 3271, 3272, 3273, 3274 };
        private const int EnglishLangId = 0;

        private readonly AutoMarketRepository   _repo;
        private readonly TranslationsViewModel? _translations;
        private List<AutoMarketOrderRow>         _allOrders = new();

        [ObservableProperty] private bool    _isLoading;
        [ObservableProperty] private string  _statusMessage = "";
        [ObservableProperty] private bool    _statusIsError;
        [ObservableProperty] private string? _orderTypeFilter;
        [ObservableProperty] private string? _categoryFilter;

        public ObservableCollection<AutoMarketOrderRow> FilteredOrders { get; } = new();

        public static IReadOnlyList<string?> OrderTypeOptions { get; } =
            new List<string?> { null, "Buy", "Sell", "Buyback" };
        public static IReadOnlyList<string?> CategoryOptions { get; } =
            new List<string?> { null, "Plasma", "Raw Material", "Production Item" };

        public AutoMarketOrdersViewModel(AutoMarketRepository repo, TranslationsViewModel? translations)
        {
            _repo         = repo;
            _translations = translations;
        }

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading orders...";
            StatusIsError = false;
            try
            {
                var raw   = await _repo.LoadOrdersAsync();
                var store = _translations?.Store;

                string Translate(string defName)
                {
                    if (string.IsNullOrEmpty(defName) || store == null) return defName;
                    var row = store.Rows.FirstOrDefault(r => r.Key == defName);
                    var t   = row?[EnglishLangId];
                    return string.IsNullOrEmpty(t) ? defName : t;
                }

                var prodItems = (await _repo.LoadTradeListAsync())
                    .Select(r => r.DefinitionName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                _allOrders = raw.Select(d =>
                {
                    var category  = PlasmaIds.Contains(d.ItemDefinition) ? "Plasma"
                                  : prodItems.Contains(d.DefinitionName)  ? "Production Item"
                                  : "Raw Material";
                    var orderType = d.IsSell                          ? "Sell"
                                  : category == "Production Item"     ? "Buyback"
                                  : "Buy";
                    return new AutoMarketOrderRow
                    {
                        DisplayName = Translate(d.DefinitionName),
                        OrderType   = orderType,
                        Price       = d.Price,
                        Amount      = d.Quantity,
                        MarketName  = Translate(d.MarketDefinitionName),
                        Category    = category,
                    };
                }).ToList();

                ApplyFilter();
                StatusMessage = $"Loaded {_allOrders.Count} order(s) at {DateTime.UtcNow:HH:mm:ss} UTC.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        private bool CanRefresh() => !IsLoading;
        partial void OnOrderTypeFilterChanged(string? _) => ApplyFilter();
        partial void OnCategoryFilterChanged(string? _)  => ApplyFilter();

        private void ApplyFilter()
        {
            var filtered = _allOrders.AsEnumerable();
            if (OrderTypeFilter != null) filtered = filtered.Where(r => r.OrderType == OrderTypeFilter);
            if (CategoryFilter  != null) filtered = filtered.Where(r => r.Category  == CategoryFilter);
            FilteredOrders.Clear();
            foreach (var r in filtered) FilteredOrders.Add(r);
        }
    }
}
