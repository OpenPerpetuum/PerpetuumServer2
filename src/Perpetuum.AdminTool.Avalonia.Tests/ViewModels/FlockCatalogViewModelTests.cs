using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Npc;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class FlockCatalogViewModelTests
{
    [Fact]
    public async Task LoadAndFilter_UseJoinedPresenceAndDefinitionNames()
    {
        FlockRow alpha = Row(1, "alpha_flock", 10, 100, "Alpha presence", "alpha_npc");
        FlockRow beta = Row(2, "beta_flock", 20, 200, "Beta presence", "beta_npc");
        var viewModel = new FlockCatalogViewModel(
            new StubRepository(Load([alpha, beta])), new ChangeQueue());

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.FilterText = "beta_npc";

        Assert.Single(viewModel.Rows);
        Assert.Same(beta, viewModel.Rows[0]);
    }

    [Fact]
    public async Task QueueUpdate_GeneratesTargetedSqlAndRestoresBaseline()
    {
        FlockRow row = Row(7, "alpha_flock", 10, 100, "Alpha presence", "alpha_npc");
        var queue = new ChangeQueue();
        var viewModel = new FlockCatalogViewModel(new StubRepository(Load([row])), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        row.FlockMemberCount = 4;
        row.Enabled = false;

        viewModel.QueueSelectedChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Equal(
            "UPDATE npcflock SET flockmembercount = 4, enabled = 0 WHERE id = 7",
            change.ToSql());
        Assert.Equal(2, row.FlockMemberCount);
        Assert.True(row.Enabled);
    }

    [Fact]
    public async Task CreateAndQueueInsert_UsesLoadedForeignKeysAndRemovesUnsavedRow()
    {
        var queue = new ChangeQueue();
        var viewModel = new FlockCatalogViewModel(new StubRepository(Load([])), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.NewName = "new_flock";
        viewModel.NewPresenceId = 10;
        viewModel.NewDefinition = 100;
        viewModel.NewMemberCount = 3;

        viewModel.CreateFlockCommand.Execute(null);
        viewModel.QueueSelectedChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Contains("INSERT INTO npcflock", change.ToSql());
        Assert.Contains("'new_flock', 10, 3, 100", change.ToSql());
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public async Task QueueDelete_IsDestructive()
    {
        FlockRow row = Row(9, "doomed_flock", 10, 100, "Alpha presence", "alpha_npc");
        var queue = new ChangeQueue();
        var viewModel = new FlockCatalogViewModel(new StubRepository(Load([row])), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.QueueDeleteCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.True(change.IsDestructive);
        Assert.Equal("DELETE FROM npcflock WHERE id = 9", change.ToSql());
    }

    private static FlockRow Row(
        int id, string name, int presenceId, int definition, string presenceName, string definitionName) =>
        new(new FlockSnapshot
        {
            Id = id,
            Name = name,
            PresenceId = presenceId,
            Definition = definition,
            FlockMemberCount = 2,
            RespawnMultiplierLow = 1,
            Enabled = true
        })
        {
            PresenceName = presenceName,
            DefinitionName = definitionName
        };

    private static FlockLoad Load(IReadOnlyList<FlockRow> rows)
    {
        var load = new FlockLoad();
        load.Rows.AddRange(rows);
        load.PresencePicks.Add(new PresencePickItem { Id = 10, Name = "Alpha presence" });
        load.PresencePicks.Add(new PresencePickItem { Id = 20, Name = "Beta presence" });
        load.DefinitionPicks.Add(new EntityPickItem { Definition = 100, Name = "alpha_npc" });
        load.DefinitionPicks.Add(new EntityPickItem { Definition = 200, Name = "beta_npc" });
        return load;
    }

    private sealed class StubRepository(FlockLoad load) : IFlockRepository
    {
        public Task<FlockLoad> LoadAllAsync() => Task.FromResult(load);
        public Task<List<FlockSummary>> LoadByPresenceAsync(int presenceId) =>
            Task.FromResult(new List<FlockSummary>());
    }
}
