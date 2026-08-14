using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class AutoMarketCatalogViewModelTests
{
    [Fact]
    public async Task LoadConfiguration_PopulatesEditableRowsAndTranslatedChoices()
    {
        var viewModel = new AutoMarketCatalogViewModel(
            new StubRepository(), new StubEntityRepository(), new ChangeQueue(),
            key => $"translated:{key}");

        await viewModel.LoadConfigurationCommand.ExecuteAsync(null);

        Assert.Single(viewModel.ConfigRows);
        Assert.Single(viewModel.TradeRows);
        Assert.Single(viewModel.DerivedMaterials);
        Assert.Single(viewModel.AddItemChoices);
        Assert.Equal("translated:def_available", viewModel.AddItemChoices[0].DisplayName);
    }

    [Fact]
    public void EditCommands_QueueReviewedSqlAndMarkRemovalDestructive()
    {
        var queue = new ChangeQueue();
        var viewModel = new AutoMarketCatalogViewModel(
            new StubRepository(), new StubEntityRepository(), queue);
        var config = new AutoMarketConfigRow
        {
            ParamName = "product_sell_margin",
            Label = "margin",
            ParamValue = 1.25,
            OriginalValue = 1
        };
        viewModel.ConfigRows.Add(config);
        viewModel.SelectedConfig = config;

        viewModel.QueueSelectedConfigCommand.Execute(null);
        config.ParamValue = 1.5;
        viewModel.QueueSelectedConfigCommand.Execute(null);

        RawSqlChange configChange = Assert.IsType<RawSqlChange>(Assert.Single(queue.Items));
        Assert.Contains("1.5", configChange.ToSql());

        var trade = new AutoMarketTradeListRow
        {
            DefinitionName = "def_product",
            DisplayName = "Product",
            Amount = 1
        };
        viewModel.TradeRows.Add(trade);
        viewModel.SelectedTradeItem = trade;
        viewModel.QueueRemoveSelectedTradeItemCommand.Execute(null);

        Assert.Equal(2, queue.Items.Count);
        Assert.True(queue.Items[1].IsDestructive);
        Assert.Contains("DELETE FROM market_orders_configuration", queue.Items[1].ToSql());
    }

    [Fact]
    public void RefreshCommand_QueuesBothServerProcedures()
    {
        var queue = new ChangeQueue();
        var viewModel = new AutoMarketCatalogViewModel(
            new StubRepository(), new StubEntityRepository(), queue);

        viewModel.QueueRefreshNowCommand.Execute(null);

        string sql = Assert.Single(queue.Items).ToSql();
        Assert.Contains("recalculate_raw_material_prices", sql);
        Assert.Contains("usp_RefreshAutoMarketOrders", sql);
    }

    private sealed class StubEntityRepository : IEntityRepository
    {
        public Task<EntitiesSnapshot> LoadAsync() => Task.FromResult(new EntitiesSnapshot
        {
            Rows =
            [
                new EntityDefaultRow(new EntityDefaultSnapshot { Definition = 1, DefinitionName = "def_product", Enabled = true }),
                new EntityDefaultRow(new EntityDefaultSnapshot { Definition = 2, DefinitionName = "def_available", Enabled = true })
            ]
        });
    }

    private sealed class StubRepository : IAutoMarketRepository
    {
        public Task<List<AutoMarketConfigRow>> LoadConfigAsync() => Task.FromResult(new List<AutoMarketConfigRow>
        {
            new() { ParamName = "margin", Label = "Margin", ParamValue = 1, OriginalValue = 1 }
        });
        public Task<List<AutoMarketTradeListRow>> LoadTradeListAsync() => Task.FromResult(new List<AutoMarketTradeListRow>
        {
            new() { DefinitionName = "def_product", DisplayName = "def_product", Amount = 1 }
        });
        public Task<List<AutoMarketRawMaterialRow>> LoadDerivedMaterialsAsync() => Task.FromResult(new List<AutoMarketRawMaterialRow>
        {
            new() { RawMaterialName = "def_material", TotalQuantity = 10 }
        });
        public Task<List<AutoMarketNicFlowRow>> LoadNicFlowAsync() => Task.FromResult(new List<AutoMarketNicFlowRow>());
        public Task<List<AutoMarketPricingTraceRow>> LoadPricingTraceAsync() => Task.FromResult(new List<AutoMarketPricingTraceRow>());
        public Task<List<AutoMarketCoveredMaterialRow>> LoadCoveredMaterialsAsync() => Task.FromResult(new List<AutoMarketCoveredMaterialRow>());
        public Task<List<AutoMarketGatherRow>> LoadGatherBreakdownAsync() => Task.FromResult(new List<AutoMarketGatherRow>());
        public Task<List<AutoMarketOrderData>> LoadOrdersAsync() => Task.FromResult(new List<AutoMarketOrderData>());
        public Task RefreshNowAsync() => Task.CompletedTask;
    }
}
