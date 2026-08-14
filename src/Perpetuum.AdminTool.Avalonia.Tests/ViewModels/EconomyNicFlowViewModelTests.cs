using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Economy;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class EconomyNicFlowViewModelTests
{
    [Fact]
    public async Task Load_ReplacesBothCollectionsAndReportsSuccess()
    {
        var repository = new StubEconomyRepository(
            [new EconomyNicFlowRow { Category = "Mission Rewards", Today = 100 }],
            [new EconomyNicFlowRow { Category = "Market Fees & Taxes", Today = 25 }]);
        var viewModel = new EconomyNicFlowViewModel(repository);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Single(viewModel.NicIn);
        Assert.Single(viewModel.NicOut);
        Assert.Equal(100, viewModel.NicIn[0].Today);
        Assert.False(viewModel.StatusIsError);
        Assert.Contains("2 NIC flow rows", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Load_ReportsRepositoryFailureWithoutThrowingOnUiCommand()
    {
        var viewModel = new EconomyNicFlowViewModel(
            new ThrowingEconomyRepository(new InvalidOperationException("schema missing")));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.StatusIsError);
        Assert.Contains("schema missing", viewModel.StatusMessage);
        Assert.Empty(viewModel.NicIn);
        Assert.Empty(viewModel.NicOut);
    }

    private sealed class StubEconomyRepository(
        List<EconomyNicFlowRow> nicIn,
        List<EconomyNicFlowRow> nicOut) : IEconomyRepository
    {
        public Task<(List<EconomyNicFlowRow> In, List<EconomyNicFlowRow> Out)> LoadNicFlowAsync()
        {
            return Task.FromResult((nicIn, nicOut));
        }
    }

    private sealed class ThrowingEconomyRepository(Exception exception) : IEconomyRepository
    {
        public Task<(List<EconomyNicFlowRow> In, List<EconomyNicFlowRow> Out)> LoadNicFlowAsync()
        {
            return Task.FromException<(List<EconomyNicFlowRow>, List<EconomyNicFlowRow>)>(exception);
        }
    }
}
