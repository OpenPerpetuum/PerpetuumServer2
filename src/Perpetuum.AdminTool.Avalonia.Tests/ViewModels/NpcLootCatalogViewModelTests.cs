using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Loot;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class NpcLootCatalogViewModelTests
{
    [Fact]
    public async Task LoadAndFilter_UseJoinedEntityNames()
    {
        NpcLootRow alpha = Row(1, 100, 200, "alpha_npc", "basic_ammo");
        NpcLootRow beta = Row(2, 101, 201, "beta_npc", "rare_core");
        var viewModel = new NpcLootCatalogViewModel(
            new StubRepository([alpha, beta]), new ChangeQueue());

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.FilterText = "rare";

        Assert.Single(viewModel.Rows);
        Assert.Same(beta, viewModel.Rows[0]);
    }

    [Fact]
    public async Task QueueUpdate_GeneratesTargetedSqlAndRestoresBaseline()
    {
        NpcLootRow row = Row(1, 100, 200, "alpha_npc", "basic_ammo");
        var queue = new ChangeQueue();
        var viewModel = new NpcLootCatalogViewModel(new StubRepository([row]), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        row.Probability = 0.25;

        viewModel.QueueSelectedChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Equal("UPDATE npcloot SET probability = 0.25 WHERE id = 1", change.ToSql());
        Assert.Equal(0.5, row.Probability);
    }

    [Fact]
    public void CreateAndQueueInsert_ValidatesAndRemovesUnsavedRow()
    {
        var queue = new ChangeQueue();
        var viewModel = new NpcLootCatalogViewModel(new StubRepository([]), queue)
        {
            NewNpcDefinition = 100,
            NewLootDefinition = 200,
            NewMinQuantity = 1,
            NewMaxQuantity = 3,
            NewProbability = 0.75
        };

        viewModel.CreateRuleCommand.Execute(null);
        viewModel.QueueSelectedChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Contains("INSERT INTO npcloot", change.ToSql());
        Assert.Contains("VALUES (100, 200, 1, 3, 0.75", change.ToSql());
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public async Task QueueDelete_IsDestructive()
    {
        NpcLootRow row = Row(1, 100, 200, "alpha_npc", "basic_ammo");
        var queue = new ChangeQueue();
        var viewModel = new NpcLootCatalogViewModel(new StubRepository([row]), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.QueueDeleteCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.True(change.IsDestructive);
        Assert.Equal("DELETE FROM npcloot WHERE id = 1", change.ToSql());
    }

    private static NpcLootRow Row(
        int id, int npcDefinition, int lootDefinition, string npcName, string lootName)
    {
        return new NpcLootRow(new NpcLootSnapshot
        {
            Id = id,
            Definition = npcDefinition,
            LootDefinition = lootDefinition,
            MinQuantity = 1,
            Quantity = 2,
            Probability = 0.5
        })
        {
            DefinitionName = npcName,
            LootDefinitionName = lootName
        };
    }

    private sealed class StubRepository(IReadOnlyList<NpcLootRow> rows) : INpcLootRepository
    {
        public Task<List<NpcLootRow>> LoadAllAsync() => Task.FromResult(rows.ToList());
    }
}
