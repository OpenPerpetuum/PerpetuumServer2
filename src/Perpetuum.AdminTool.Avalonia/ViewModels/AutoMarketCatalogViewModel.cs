using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class AutoMarketCatalogViewModel : ObservableObject
{
    private static readonly HashSet<int> PlasmaIds = [3271, 3272, 3273, 3274];
    private readonly IAutoMarketRepository _repository;
    private readonly IEntityRepository _entityRepository;
    private readonly ChangeQueue _queue;
    private readonly Func<string, string> _translate;
    private List<AutoMarketOrderRow> _allOrders = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage = "Load an AutoMarket section to inspect live data.";
    [ObservableProperty] private AutoMarketConfigRow? _selectedConfig;
    [ObservableProperty] private AutoMarketTradeListRow? _selectedTradeItem;
    [ObservableProperty] private AddAutoMarketItemPickItem? _selectedAddItem;
    [ObservableProperty] private AutoMarketCoveredMaterialRow? _selectedMaterial;
    [ObservableProperty] private bool _showOverridesOnly;
    [ObservableProperty] private string _orderTypeFilter = "All";
    [ObservableProperty] private string _categoryFilter = "All";

    public AutoMarketCatalogViewModel(
        IAutoMarketRepository repository,
        IEntityRepository entityRepository,
        ChangeQueue queue,
        Func<string, string>? translate = null)
    {
        _repository = repository;
        _entityRepository = entityRepository;
        _queue = queue;
        _translate = translate ?? (key => key);
    }

    public ObservableCollection<AutoMarketConfigRow> ConfigRows { get; } = new();
    public ObservableCollection<AutoMarketTradeListRow> TradeRows { get; } = new();
    public ObservableCollection<AutoMarketRawMaterialRow> DerivedMaterials { get; } = new();
    public ObservableCollection<AddAutoMarketItemPickItem> AddItemChoices { get; } = new();
    public ObservableCollection<AutoMarketCoveredMaterialRow> MaterialRows { get; } = new();
    public ObservableCollection<AutoMarketCoveredMaterialRow> FilteredMaterialRows { get; } = new();
    public ObservableCollection<AutoMarketNicFlowRow> NicFlow { get; } = new();
    public ObservableCollection<AutoMarketPricingTraceRow> PricingTrace { get; } = new();
    public ObservableCollection<AutoMarketGatherRow> GatherBreakdown { get; } = new();
    public ObservableCollection<AutoMarketOrderRow> FilteredOrders { get; } = new();
    public IReadOnlyList<string> OrderTypeOptions { get; } = ["All", "Buy", "Sell", "Buyback"];
    public IReadOnlyList<string> CategoryOptions { get; } = ["All", "Plasma", "Raw Material", "Production Item"];
    public bool IsNotLoading => !IsLoading;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));
    partial void OnShowOverridesOnlyChanged(bool value) => ApplyMaterialFilter();
    partial void OnOrderTypeFilterChanged(string value) => ApplyOrderFilter();
    partial void OnCategoryFilterChanged(string value) => ApplyOrderFilter();

    [RelayCommand]
    private async Task LoadConfigurationAsync()
    {
        await RunLoadAsync("configuration and trade list", async () =>
        {
            Task<List<AutoMarketConfigRow>> configTask = _repository.LoadConfigAsync();
            Task<List<AutoMarketTradeListRow>> tradeTask = _repository.LoadTradeListAsync();
            Task<List<AutoMarketRawMaterialRow>> derivedTask = _repository.LoadDerivedMaterialsAsync();
            Task<EntitiesSnapshot> entityTask = _entityRepository.LoadAsync();
            await Task.WhenAll(configTask, tradeTask, derivedTask, entityTask);
            Replace(ConfigRows, configTask.Result);
            foreach (AutoMarketTradeListRow row in tradeTask.Result) row.DisplayName = _translate(row.DefinitionName);
            Replace(TradeRows, tradeTask.Result);
            Replace(DerivedMaterials, derivedTask.Result);
            HashSet<string> existing = TradeRows.Select(row => row.DefinitionName).ToHashSet(StringComparer.Ordinal);
            Replace(AddItemChoices, entityTask.Result.Rows
                .Where(row => row.Enabled && !existing.Contains(row.DefinitionName))
                .Select(row => new AddAutoMarketItemPickItem
                {
                    Definition = row.Definition,
                    DefinitionName = row.DefinitionName,
                    DisplayName = _translate(row.DefinitionName)
                })
                .OrderBy(row => row.DisplayName));
        });
    }

    [RelayCommand]
    private async Task LoadMaterialsAsync()
    {
        await RunLoadAsync("raw materials", async () =>
        {
            List<AutoMarketCoveredMaterialRow> rows = await _repository.LoadCoveredMaterialsAsync();
            foreach (AutoMarketCoveredMaterialRow row in rows) row.DisplayName = _translate(row.DefinitionName);
            Replace(MaterialRows, rows);
            ApplyMaterialFilter();
        });
    }

    [RelayCommand]
    private async Task LoadStatisticsAsync()
    {
        await RunLoadAsync("statistics", async () =>
        {
            Task<List<AutoMarketNicFlowRow>> nicTask = _repository.LoadNicFlowAsync();
            Task<List<AutoMarketPricingTraceRow>> priceTask = _repository.LoadPricingTraceAsync();
            Task<List<AutoMarketGatherRow>> gatherTask = _repository.LoadGatherBreakdownAsync();
            await Task.WhenAll(nicTask, priceTask, gatherTask);
            Replace(NicFlow, nicTask.Result);
            foreach (AutoMarketPricingTraceRow row in priceTask.Result) row.DisplayName = _translate(row.ResourceName);
            Replace(PricingTrace, priceTask.Result);
            foreach (AutoMarketGatherRow row in gatherTask.Result) row.DisplayName = _translate(row.ResourceName);
            Replace(GatherBreakdown, gatherTask.Result);
        });
    }

    [RelayCommand]
    private async Task LoadOrdersAsync()
    {
        await RunLoadAsync("active orders", async () =>
        {
            Task<List<AutoMarketOrderData>> ordersTask = _repository.LoadOrdersAsync();
            Task<List<AutoMarketTradeListRow>> tradeTask = _repository.LoadTradeListAsync();
            await Task.WhenAll(ordersTask, tradeTask);
            HashSet<string> productionItems = tradeTask.Result.Select(row => row.DefinitionName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _allOrders = ordersTask.Result.Select(data =>
            {
                string category = PlasmaIds.Contains(data.ItemDefinition) ? "Plasma"
                    : productionItems.Contains(data.DefinitionName) ? "Production Item" : "Raw Material";
                return new AutoMarketOrderRow
                {
                    DisplayName = _translate(data.DefinitionName),
                    OrderType = data.IsSell ? "Sell" : category == "Production Item" ? "Buyback" : "Buy",
                    Price = data.Price,
                    Amount = data.Quantity,
                    MarketName = _translate(data.MarketDefinitionName),
                    Category = category
                };
            }).ToList();
            ApplyOrderFilter();
        });
    }

    [RelayCommand]
    private void QueueSelectedConfig()
    {
        if (SelectedConfig == null) return;
        AutoMarketConfigRow row = SelectedConfig;
        ReplaceQueuedChange(
            $"automarket_config: update {row.ParamName}",
            $"UPDATE automarket_config SET param_value = {SqlLiteral.Of(row.ParamValue)} WHERE param_name = {SqlLiteral.Of(row.ParamName)};");
        row.OriginalValue = row.ParamValue;
        SetQueuedStatus(row.Label);
    }

    [RelayCommand]
    private void QueueSelectedTradeItem()
    {
        if (SelectedTradeItem == null) return;
        AutoMarketTradeListRow row = SelectedTradeItem;
        ReplaceQueuedChange(
            $"market_orders_configuration: update {row.DefinitionName}",
            $"UPDATE market_orders_configuration SET amount = {SqlLiteral.Of(row.Amount)}, " +
            $"create_sell_orders = {SqlLiteral.Of(row.CreateSellOrders)}, " +
            $"create_buyback_orders = {SqlLiteral.Of(row.CreateBuybackOrders)} " +
            $"WHERE definitionname = {SqlLiteral.Of(row.DefinitionName)};");
        row.OriginalAmount = row.Amount;
        row.OriginalCreateSellOrders = row.CreateSellOrders;
        row.OriginalCreateBuybackOrders = row.CreateBuybackOrders;
        SetQueuedStatus(row.DisplayName);
    }

    [RelayCommand]
    private void QueueRemoveSelectedTradeItem()
    {
        if (SelectedTradeItem == null) return;
        AutoMarketTradeListRow row = SelectedTradeItem;
        string updateDescription = $"market_orders_configuration: update {row.DefinitionName}";
        RemoveQueuedDescription(updateDescription);
        _queue.Add(new RawSqlChange(
            $"market_orders_configuration: delete {row.DefinitionName}",
            $"DELETE FROM market_orders_configuration WHERE definitionname = {SqlLiteral.Of(row.DefinitionName)};",
            isDestructive: true));
        TradeRows.Remove(row);
        StatusIsError = false;
        StatusMessage = $"Queued removal of {row.DisplayName}; direct apply will require APPLY DELETE.";
    }

    [RelayCommand]
    private void QueueAddItem()
    {
        if (SelectedAddItem == null) return;
        AddAutoMarketItemPickItem item = SelectedAddItem;
        _queue.Add(new RawSqlChange(
            $"market_orders_configuration: insert {item.DefinitionName}",
            $"INSERT INTO market_orders_configuration (definitionname, amount) VALUES ({SqlLiteral.Of(item.DefinitionName)}, 1);"));
        TradeRows.Add(new AutoMarketTradeListRow
        {
            DefinitionName = item.DefinitionName,
            DisplayName = item.DisplayName,
            Amount = 1,
            OriginalAmount = 1,
            CreateSellOrders = true,
            OriginalCreateSellOrders = true,
            CreateBuybackOrders = true,
            OriginalCreateBuybackOrders = true
        });
        AddItemChoices.Remove(item);
        SelectedAddItem = null;
        SetQueuedStatus(item.DisplayName);
    }

    [RelayCommand]
    private void QueueSelectedMaterial()
    {
        if (SelectedMaterial == null) return;
        AutoMarketCoveredMaterialRow row = SelectedMaterial;
        string sql = row.IsAtDefaults
            ? $"DELETE FROM automarket_rawmat_overrides WHERE definitionname = {SqlLiteral.Of(row.DefinitionName)};"
            : $"MERGE automarket_rawmat_overrides AS t USING (VALUES ({SqlLiteral.Of(row.DefinitionName)}, " +
              $"{SqlLiteral.OfNullableInt(row.WeeklyCapOverride)}, {(row.CreateBuyOrders ? 1 : 0)}, {(row.CreateSellOrders ? 1 : 0)})) " +
              "AS s (definitionname, weekly_cap_override, create_buy_orders, create_sell_orders) " +
              "ON t.definitionname = s.definitionname " +
              "WHEN MATCHED THEN UPDATE SET weekly_cap_override = s.weekly_cap_override, " +
              "create_buy_orders = s.create_buy_orders, create_sell_orders = s.create_sell_orders " +
              "WHEN NOT MATCHED THEN INSERT (definitionname, weekly_cap_override, create_buy_orders, create_sell_orders) " +
              "VALUES (s.definitionname, s.weekly_cap_override, s.create_buy_orders, s.create_sell_orders);";
        ReplaceQueuedChange($"automarket_rawmat_overrides: {row.DefinitionName}", sql);
        row.OriginalCapOverride = row.WeeklyCapOverride;
        row.OriginalBuyOrders = row.CreateBuyOrders;
        row.OriginalSellOrders = row.CreateSellOrders;
        ApplyMaterialFilter();
        SetQueuedStatus(row.DisplayName);
    }

    [RelayCommand]
    private void QueueRefreshNow()
    {
        ReplaceQueuedChange(
            "AutoMarket: recalculate prices and refresh orders",
            "EXEC recalculate_raw_material_prices;\nEXEC usp_RefreshAutoMarketOrders;");
        StatusIsError = false;
        StatusMessage = "Queued the AutoMarket recalculation and order refresh procedures.";
    }

    private async Task RunLoadAsync(string label, Func<Task> load)
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusIsError = false;
        StatusMessage = $"Loading AutoMarket {label}...";
        try
        {
            await load();
            StatusMessage = $"Loaded AutoMarket {label} at {DateTime.UtcNow:HH:mm:ss} UTC.";
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = $"Unable to load AutoMarket {label}: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    private void ReplaceQueuedChange(string description, string sql)
    {
        RemoveQueuedDescription(description);
        _queue.Add(new RawSqlChange(description, sql));
    }

    private void RemoveQueuedDescription(string description)
    {
        IPendingChange? existing = _queue.Items.FirstOrDefault(change => change.Description == description);
        if (existing != null) _queue.Items.Remove(existing);
    }

    private void SetQueuedStatus(string label)
    {
        StatusIsError = false;
        StatusMessage = $"Queued AutoMarket changes for {label}.";
    }

    private void ApplyMaterialFilter()
    {
        IEnumerable<AutoMarketCoveredMaterialRow> rows = ShowOverridesOnly
            ? MaterialRows.Where(row => row.HasOverride) : MaterialRows;
        Replace(FilteredMaterialRows, rows);
    }

    private void ApplyOrderFilter()
    {
        IEnumerable<AutoMarketOrderRow> rows = _allOrders;
        if (OrderTypeFilter != "All") rows = rows.Where(row => row.OrderType == OrderTypeFilter);
        if (CategoryFilter != "All") rows = rows.Where(row => row.Category == CategoryFilter);
        Replace(FilteredOrders, rows);
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (T row in source) target.Add(row);
    }
}
