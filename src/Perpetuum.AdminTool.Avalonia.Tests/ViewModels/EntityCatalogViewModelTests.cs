using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class EntityCatalogViewModelTests
{
    [Fact]
    public async Task LoadAndFilter_UsePortableRepositoryResults()
    {
        EntityDefaultRow alpha = CreateRow(100, "alpha_robot");
        EntityDefaultRow beta = CreateRow(200, "beta_module");
        var viewModel = new EntityCatalogViewModel(
            new StubEntityRepository([alpha, beta]),
            new ChangeQueue());

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.FilterText = "robot";

        Assert.Single(viewModel.Rows);
        Assert.Same(alpha, viewModel.Rows[0]);
        Assert.Contains("2 entity definitions", viewModel.StatusMessage);
    }

    [Fact]
    public async Task QueueSelectedChanges_AddsSqlOnceAndRefreshesTheBaseline()
    {
        EntityDefaultRow row = CreateRow(100, "alpha_robot");
        var queue = new ChangeQueue();
        var viewModel = new EntityCatalogViewModel(new StubEntityRepository([row]), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        row.Mass = 42.5;

        viewModel.QueueSelectedChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Contains("[mass] = 42.5", change.ToSql());
        Assert.Contains("Queued 1 change", viewModel.StatusMessage);
        Assert.Equal(1, row.Mass);

        viewModel.QueueSelectedChangesCommand.Execute(null);
        Assert.Single(queue.Items);
        Assert.Equal("No changes to queue.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RevertSelectedChanges_RestoresLoadedValues()
    {
        EntityDefaultRow row = CreateRow(100, "alpha_robot");
        var viewModel = new EntityCatalogViewModel(
            new StubEntityRepository([row]),
            new ChangeQueue());
        await viewModel.LoadCommand.ExecuteAsync(null);
        row.Health = 1;

        viewModel.RevertSelectedChangesCommand.Execute(null);

        Assert.Equal(100, row.Health);
    }

    private static EntityDefaultRow CreateRow(int definition, string name)
    {
        return new EntityDefaultRow(new EntityDefaultSnapshot
        {
            Definition = definition,
            DefinitionName = name,
            Mass = 1,
            Volume = 1,
            Health = 100,
            Quantity = 1,
            Enabled = true
        });
    }

    private sealed class StubEntityRepository(IReadOnlyList<EntityDefaultRow> rows) : IEntityRepository
    {
        public Task<EntitiesSnapshot> LoadAsync()
        {
            return Task.FromResult(new EntitiesSnapshot { Rows = rows });
        }
    }
}
