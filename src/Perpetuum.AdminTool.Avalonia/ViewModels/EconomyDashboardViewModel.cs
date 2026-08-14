using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class EconomyDashboardViewModel : ObservableObject
{
    private readonly IEconomyRepository _nicFlowRepository;
    private readonly IEconomyMoneySupplyRepository _moneySupplyRepository;
    private readonly IEconomyMarketHealthRepository _marketHealthRepository;
    private readonly IEconomySinkRepository _sinkRepository;
    private readonly IEconomyInsuranceRepository _insuranceRepository;
    private readonly IEntityRepository _entityRepository;
    private readonly ChangeQueue _queue;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage = "Load an economy section to inspect live data.";
    [ObservableProperty] private long _totalNic;
    [ObservableProperty] private long _medianNic;
    [ObservableProperty] private double _top1PctShare;
    [ObservableProperty] private long _idleNic;
    [ObservableProperty] private int _ageBucketToday;
    [ObservableProperty] private int _ageBucketD1To7;
    [ObservableProperty] private int _ageBucketD7To30;
    [ObservableProperty] private int _ageBucketD30Plus;
    [ObservableProperty] private int _autoMarketOrderCount;
    [ObservableProperty] private int _playerOrderCount;
    [ObservableProperty] private int _activePlayerCount;
    [ObservableProperty] private double _insuranceCoveragePct;
    [ObservableProperty] private EconomyPriceIndexBasketItem? _selectedBasketItem;
    [ObservableProperty] private EntityPickItem? _selectedNewBasketItem;
    [ObservableProperty] private InsuranceConfigRow? _selectedInsuranceConfig;

    public EconomyDashboardViewModel(
        IEconomyRepository nicFlowRepository,
        IEconomyMoneySupplyRepository moneySupplyRepository,
        IEconomyMarketHealthRepository marketHealthRepository,
        IEconomySinkRepository sinkRepository,
        IEconomyInsuranceRepository insuranceRepository,
        IEntityRepository entityRepository,
        ChangeQueue queue)
    {
        _nicFlowRepository = nicFlowRepository;
        _moneySupplyRepository = moneySupplyRepository;
        _marketHealthRepository = marketHealthRepository;
        _sinkRepository = sinkRepository;
        _insuranceRepository = insuranceRepository;
        _entityRepository = entityRepository;
        _queue = queue;
    }

    public ObservableCollection<EconomyNicFlowRow> NicIn { get; } = new();
    public ObservableCollection<EconomyNicFlowRow> NicOut { get; } = new();
    public ObservableCollection<EconomySnapshotRow> SnapshotRows { get; } = new();
    public ObservableCollection<EconomyWealthRow> TopCharacters { get; } = new();
    public ObservableCollection<EconomyCorporationWealthRow> TopCorporations { get; } = new();
    public ObservableCollection<EconomyVelocityRow> VelocityRows { get; } = new();
    public ObservableCollection<EconomyPriceIndexRow> PriceIndexRows { get; } = new();
    public ObservableCollection<EconomyPriceIndexBasketItem> BasketItems { get; } = new();
    public ObservableCollection<EntityPickItem> BasketChoices { get; } = new();
    public ObservableCollection<EconomySinkRow> SinkRows { get; } = new();
    public ObservableCollection<InsuranceConfigRow> InsuranceConfigRows { get; } = new();
    public ObservableCollection<InsurancePriceRow> InsurancePriceRows { get; } = new();
    public bool IsNotLoading => !IsLoading;
    public bool ShowInsuranceSinkWarning
    {
        get
        {
            double fee = InsuranceConfigRows.FirstOrDefault(row => row.ParamName == "fee_pct")?.ParamValue ?? 0;
            double payout = InsuranceConfigRows.FirstOrDefault(row => row.ParamName == "payout_pct")?.ParamValue ?? 0;
            return payout >= fee;
        }
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

    [RelayCommand]
    private async Task LoadNicFlowAsync() => await RunLoadAsync("NIC flow", async () =>
    {
        (List<EconomyNicFlowRow> nicIn, List<EconomyNicFlowRow> nicOut) =
            await _nicFlowRepository.LoadNicFlowAsync();
        Replace(NicIn, nicIn);
        Replace(NicOut, nicOut);
    });

    [RelayCommand]
    private async Task LoadMoneySupplyAsync() => await RunLoadAsync("money supply", async () =>
    {
        EconomyMoneySupplyData data = await _moneySupplyRepository.LoadAsync();
        TotalNic = data.TotalNic;
        MedianNic = data.MedianNic;
        Top1PctShare = data.Top1PctShare;
        IdleNic = data.IdleNic;
        Replace(SnapshotRows, data.SnapshotRows);
        Replace(TopCharacters, data.Top10Rows);
        Replace(TopCorporations, data.Top10CorpRows);
    });

    [RelayCommand]
    private async Task LoadMarketHealthAsync() => await RunLoadAsync("market health", async () =>
    {
        Task<EconomyMarketData> marketTask = _marketHealthRepository.LoadMarketDataAsync();
        Task<IReadOnlyList<EconomyPriceIndexBasketItem>> basketTask = _marketHealthRepository.LoadBasketAsync();
        Task<EntitiesSnapshot> entitiesTask = _entityRepository.LoadAsync();
        await Task.WhenAll(marketTask, basketTask, entitiesTask);
        EconomyMarketData data = marketTask.Result;
        Replace(VelocityRows, data.VelocityRows);
        Replace(PriceIndexRows, data.PriceIndexRows);
        AgeBucketToday = data.AgeBuckets.Today;
        AgeBucketD1To7 = data.AgeBuckets.D1To7;
        AgeBucketD7To30 = data.AgeBuckets.D7To30;
        AgeBucketD30Plus = data.AgeBuckets.D30Plus;
        AutoMarketOrderCount = data.AutoMarketOrderCount;
        PlayerOrderCount = data.PlayerOrderCount;
        Replace(BasketItems, basketTask.Result);
        HashSet<int> selected = BasketItems.Select(item => item.Definition).ToHashSet();
        Replace(BasketChoices, entitiesTask.Result.Rows.Where(row => row.Enabled && !selected.Contains(row.Definition))
            .Select(row => new EntityPickItem
            {
                Definition = row.Definition,
                Name = row.DefinitionName,
                Enabled = row.Enabled,
                CategoryFlags = row.CategoryFlags
            }).OrderBy(item => item.Name));
    });

    [RelayCommand]
    private async Task LoadSinksAsync() => await RunLoadAsync("sink effectiveness", async () =>
    {
        EconomySinkData data = await _sinkRepository.LoadAsync();
        ActivePlayerCount = data.ActivePlayerCount;
        InsuranceCoveragePct = data.InsuranceCoveragePct;
        Replace(SinkRows, data.SinkRows);
    });

    [RelayCommand]
    private async Task LoadInsuranceAsync() => await RunLoadAsync("insurance", async () =>
    {
        Task<List<InsuranceConfigRow>> configTask = _insuranceRepository.LoadConfigAsync();
        Task<List<InsurancePriceRow>> pricesTask = _insuranceRepository.LoadPricesAsync();
        await Task.WhenAll(configTask, pricesTask);
        Replace(InsuranceConfigRows, configTask.Result);
        Replace(InsurancePriceRows, pricesTask.Result);
        OnPropertyChanged(nameof(ShowInsuranceSinkWarning));
    });

    [RelayCommand]
    private void QueueSelectedBasketWeight()
    {
        if (SelectedBasketItem == null) return;
        EconomyPriceIndexBasketItem item = SelectedBasketItem;
        ReplaceQueuedChange(
            $"economy_price_index_basket: update id={item.Id}",
            $"UPDATE economy_price_index_basket SET weight = {SqlLiteral.Of(item.Weight)} WHERE id = {SqlLiteral.Of(item.Id)};");
        SetQueuedStatus(item.DefinitionName);
    }

    [RelayCommand]
    private void QueueRemoveSelectedBasketItem()
    {
        if (SelectedBasketItem == null) return;
        EconomyPriceIndexBasketItem item = SelectedBasketItem;
        if (item.Id > 0)
            _queue.Add(new RawSqlChange(
                $"economy_price_index_basket: delete id={item.Id}",
                $"DELETE FROM economy_price_index_basket WHERE id = {SqlLiteral.Of(item.Id)};",
                isDestructive: true));
        BasketItems.Remove(item);
        SetQueuedStatus(item.DefinitionName);
    }

    [RelayCommand]
    private void QueueAddBasketItem()
    {
        if (SelectedNewBasketItem == null) return;
        EntityPickItem choice = SelectedNewBasketItem;
        _queue.Add(new RawSqlChange(
            $"economy_price_index_basket: insert {choice.Name}",
            $"INSERT INTO economy_price_index_basket (definition, weight) VALUES ({SqlLiteral.Of(choice.Definition)}, 1.0);"));
        BasketItems.Add(new EconomyPriceIndexBasketItem
        {
            Id = 0,
            Definition = choice.Definition,
            DefinitionName = choice.Name,
            Weight = 1
        });
        BasketChoices.Remove(choice);
        SelectedNewBasketItem = null;
        SetQueuedStatus(choice.Name);
    }

    [RelayCommand]
    private void QueueSelectedInsuranceConfig()
    {
        if (SelectedInsuranceConfig == null) return;
        InsuranceConfigRow row = SelectedInsuranceConfig;
        ReplaceQueuedChange(
            $"insurance_config: update {row.ParamName}",
            $"UPDATE insurance_config SET param_value = {SqlLiteral.Of(row.ParamValue)} WHERE param_name = {SqlLiteral.Of(row.ParamName)};");
        row.OriginalValue = row.ParamValue;
        OnPropertyChanged(nameof(ShowInsuranceSinkWarning));
        SetQueuedStatus(row.Label);
    }

    [RelayCommand]
    private void QueueInsuranceRecalculation()
    {
        ReplaceQueuedChange("Insurance: recalculate prices", "EXEC usp_RecalculateInsurancePrices;");
        StatusIsError = false;
        StatusMessage = "Queued insurance price recalculation.";
    }

    private async Task RunLoadAsync(string label, Func<Task> load)
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusIsError = false;
        StatusMessage = $"Loading {label}...";
        try
        {
            await load();
            StatusMessage = $"Loaded {label} at {DateTime.UtcNow:HH:mm:ss} UTC.";
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = $"Unable to load {label}: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    private void ReplaceQueuedChange(string description, string sql)
    {
        IPendingChange? existing = _queue.Items.FirstOrDefault(change => change.Description == description);
        if (existing != null) _queue.Items.Remove(existing);
        _queue.Add(new RawSqlChange(description, sql));
    }

    private void SetQueuedStatus(string label)
    {
        StatusIsError = false;
        StatusMessage = $"Queued economy changes for {label}.";
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (T row in source) target.Add(row);
    }
}
