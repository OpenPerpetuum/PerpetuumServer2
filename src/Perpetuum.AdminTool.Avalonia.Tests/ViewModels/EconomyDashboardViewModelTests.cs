using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class EconomyDashboardViewModelTests
{
    [Fact]
    public async Task MoneySupplyLoad_PopulatesHeadlineAndWealthData()
    {
        EconomyDashboardViewModel viewModel = Create(new ChangeQueue());

        await viewModel.LoadMoneySupplyCommand.ExecuteAsync(null);

        Assert.Equal(1_000, viewModel.TotalNic);
        Assert.Equal(100, viewModel.MedianNic);
        Assert.Single(viewModel.SnapshotRows);
        Assert.Single(viewModel.TopCharacters);
        Assert.Single(viewModel.TopCorporations);
    }

    [Fact]
    public void BasketAndInsuranceCommands_QueueReviewedMutations()
    {
        var queue = new ChangeQueue();
        EconomyDashboardViewModel viewModel = Create(queue);
        var basket = new EconomyPriceIndexBasketItem
        {
            Id = 4,
            Definition = 77,
            DefinitionName = "def_basket",
            Weight = 2.5
        };
        viewModel.BasketItems.Add(basket);
        viewModel.SelectedBasketItem = basket;

        viewModel.QueueSelectedBasketWeightCommand.Execute(null);
        viewModel.QueueRemoveSelectedBasketItemCommand.Execute(null);
        viewModel.QueueInsuranceRecalculationCommand.Execute(null);

        Assert.Equal(3, queue.Items.Count);
        Assert.Contains("weight = 2.5", queue.Items[0].ToSql());
        Assert.True(queue.Items[1].IsDestructive);
        Assert.Contains("usp_RecalculateInsurancePrices", queue.Items[2].ToSql());
    }

    private static EconomyDashboardViewModel Create(ChangeQueue queue) => new(
        new StubNicRepository(),
        new StubMoneyRepository(),
        new StubMarketRepository(),
        new StubSinkRepository(),
        new StubInsuranceRepository(),
        new StubEntityRepository(),
        queue);

    private sealed class StubNicRepository : IEconomyRepository
    {
        public Task<(List<EconomyNicFlowRow> In, List<EconomyNicFlowRow> Out)> LoadNicFlowAsync() =>
            Task.FromResult((new List<EconomyNicFlowRow>(), new List<EconomyNicFlowRow>()));
    }

    private sealed class StubMoneyRepository : IEconomyMoneySupplyRepository
    {
        public Task<EconomyMoneySupplyData> LoadAsync() => Task.FromResult(new EconomyMoneySupplyData
        {
            TotalNic = 1_000,
            MedianNic = 100,
            SnapshotRows = [new EconomySnapshotRow()],
            Top10Rows = [new EconomyWealthRow()],
            Top10CorpRows = [new EconomyCorporationWealthRow()]
        });
    }

    private sealed class StubMarketRepository : IEconomyMarketHealthRepository
    {
        public Task<EconomyMarketData> LoadMarketDataAsync() => Task.FromResult(new EconomyMarketData());
        public Task<IReadOnlyList<EconomyPriceIndexBasketItem>> LoadBasketAsync() =>
            Task.FromResult<IReadOnlyList<EconomyPriceIndexBasketItem>>([]);
    }

    private sealed class StubSinkRepository : IEconomySinkRepository
    {
        public Task<EconomySinkData> LoadAsync() => Task.FromResult(new EconomySinkData());
    }

    private sealed class StubInsuranceRepository : IEconomyInsuranceRepository
    {
        public Task<List<InsuranceConfigRow>> LoadConfigAsync() => Task.FromResult(new List<InsuranceConfigRow>());
        public Task<List<InsurancePriceRow>> LoadPricesAsync() => Task.FromResult(new List<InsurancePriceRow>());
        public Task RecalculateAsync() => Task.CompletedTask;
    }

    private sealed class StubEntityRepository : IEntityRepository
    {
        public Task<EntitiesSnapshot> LoadAsync() => Task.FromResult(new EntitiesSnapshot());
    }
}
