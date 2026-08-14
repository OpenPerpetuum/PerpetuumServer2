using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.ExportedTypes;

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

    [Fact]
    public async Task CreateWithStatAndQueue_GeneratesCombinedIdentityInsert()
    {
        var queue = new ChangeQueue();
        var field = new AggregateFieldInfo { Id = (int)AggregateField.mass, Name = "mass" };
        var viewModel = new EntityCatalogViewModel(
            new StubEntityRepository([], new Dictionary<int, AggregateFieldInfo> { [field.Id] = field }),
            queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.NewEntityName = "new_test_entity";
        viewModel.CreateEntityCommand.Execute(null);
        viewModel.NewStatField = field;
        viewModel.NewStatValue = 12.5;
        viewModel.AddStatCommand.Execute(null);

        viewModel.QueueSelectedChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Contains("INSERT INTO entitydefaults", change.ToSql());
        Assert.Contains("SCOPE_IDENTITY", change.ToSql());
        Assert.Contains("INSERT INTO aggregatevalues", change.ToSql());
        Assert.Contains("new_test_entity", queue.PendingNewEntityNames);
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public async Task QueueStatUpdateAndDelete_RestoresLoadedStatBaseline()
    {
        EntityDefaultRow row = CreateRow(100, "alpha_robot");
        row.Stats.Add(new StatRow(100, AggregateField.mass, 1, wasInDb: true));
        row.OriginalStats[(int)AggregateField.mass] = 1;
        var queue = new ChangeQueue();
        var viewModel = new EntityCatalogViewModel(new StubEntityRepository([row]), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        row.Stats[0].Value = 3;

        viewModel.QueueSelectedChangesCommand.Execute(null);

        Assert.Contains(queue.Items, change => change.ToSql().Contains("UPDATE aggregatevalues SET value = 3"));
        Assert.Equal(1, Assert.Single(row.Stats).Value);
    }

    [Fact]
    public async Task QueueDelete_EmitsDestructiveDependencyOrderedChanges()
    {
        EntityDefaultRow row = CreateRow(100, "alpha_robot");
        var queue = new ChangeQueue();
        var viewModel = new EntityCatalogViewModel(new StubEntityRepository([row]), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.QueueDeleteCommand.Execute(null);

        Assert.Equal(2, queue.Items.Count);
        Assert.All(queue.Items, change => Assert.True(change.IsDestructive));
        Assert.StartsWith("DELETE FROM aggregatevalues", queue.Items[0].ToSql());
        Assert.StartsWith("DELETE FROM entitydefaults", queue.Items[1].ToSql());
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public async Task ExportSelected_ShowsPortableSql()
    {
        EntityDefaultRow row = CreateRow(100, "alpha_robot");
        var viewModel = new EntityCatalogViewModel(
            new StubEntityRepository([row]), new ChangeQueue(), new StubContentExporter());
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.ExportSelectedCommand.ExecuteAsync(null);

        Assert.Equal("item export 100", viewModel.ExportScript);
        Assert.True(viewModel.HasExportScript);
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

    private sealed class StubEntityRepository(
        IReadOnlyList<EntityDefaultRow> rows,
        IReadOnlyDictionary<int, AggregateFieldInfo>? fields = null) : IEntityRepository
    {
        public Task<EntitiesSnapshot> LoadAsync()
        {
            return Task.FromResult(new EntitiesSnapshot
            {
                Rows = rows,
                Fields = fields ?? new Dictionary<int, AggregateFieldInfo>()
            });
        }
    }
}
