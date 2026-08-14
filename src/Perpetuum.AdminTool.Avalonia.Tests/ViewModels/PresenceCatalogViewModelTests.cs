using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Npc;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class PresenceCatalogViewModelTests
{
    [Fact]
    public async Task LoadAndFilter_ResolveZoneSpawnNames()
    {
        PresenceRow alpha = Row(1, "alpha_presence", 100);
        PresenceRow beta = Row(2, "beta_presence", 200);
        PresenceLoad load = Load([alpha, beta], [(100, "Alpha zone"), (200, "Beta zone")]);
        var viewModel = new PresenceCatalogViewModel(new StubRepository(load), new ChangeQueue());

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.FilterText = "Beta zone";

        Assert.Single(viewModel.Rows);
        Assert.Same(beta, viewModel.Rows[0]);
        Assert.Equal("Beta zone", beta.SpawnName);
    }

    [Fact]
    public async Task QueueUpdate_GeneratesTargetedSqlAndRestoresBaseline()
    {
        PresenceRow row = Row(7, "alpha_presence", 100);
        var queue = new ChangeQueue();
        var viewModel = new PresenceCatalogViewModel(
            new StubRepository(Load([row], [(100, "Alpha zone")])), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        row.Enabled = false;
        row.GrowthSeconds = 30;

        viewModel.QueueSelectedChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Equal(
            "UPDATE npcpresence SET enabled = 0, growthseconds = 30 WHERE id = 7",
            change.ToSql());
        Assert.True(row.Enabled);
        Assert.Null(row.GrowthSeconds);
    }

    [Fact]
    public void CreateAndQueueInsert_RemovesUnsavedRow()
    {
        var queue = new ChangeQueue();
        var viewModel = new PresenceCatalogViewModel(
            new StubRepository(new PresenceLoad()), queue)
        {
            NewName = "new_presence",
            NewSpawnId = 123,
            NewTopX = 10,
            NewTopY = 20,
            NewBottomX = 30,
            NewBottomY = 40
        };

        viewModel.CreatePresenceCommand.Execute(null);
        viewModel.QueueSelectedChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Contains("INSERT INTO npcpresence", change.ToSql());
        Assert.Contains("'new_presence', 10, 20, 30, 40", change.ToSql());
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public async Task QueueDelete_IsDestructive()
    {
        PresenceRow row = Row(9, "doomed_presence", null);
        var queue = new ChangeQueue();
        var viewModel = new PresenceCatalogViewModel(
            new StubRepository(Load([row], [])), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.QueueDeleteCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.True(change.IsDestructive);
        Assert.Equal("DELETE FROM npcpresence WHERE id = 9", change.ToSql());
    }

    private static PresenceRow Row(int id, string name, int? spawnId) => new(new PresenceSnapshot
    {
        Id = id,
        Name = name,
        SpawnId = spawnId,
        TopX = 1,
        TopY = 2,
        BottomX = 3,
        BottomY = 4,
        Enabled = true,
        IsRespawnAllowed = true
    });

    private static PresenceLoad Load(
        IReadOnlyList<PresenceRow> rows,
        IReadOnlyList<(int Id, string Name)> spawns)
    {
        var load = new PresenceLoad();
        load.Rows.AddRange(rows);
        load.ZoneSpawnPicks.AddRange(spawns.Select(spawn => new ZoneSpawnPickItem
        {
            SpawnId = spawn.Id,
            Name = spawn.Name
        }));
        return load;
    }

    private sealed class StubRepository(PresenceLoad load) : IPresenceRepository
    {
        public Task<PresenceLoad> LoadAllAsync() => Task.FromResult(load);
    }
}
