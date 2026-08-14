using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.EquipmentSets;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class EquipmentSetsCatalogViewModelTests
{
    [Fact]
    public async Task LoadAndDetails_PopulatePortableRepositoryResults()
    {
        var repository = new StubRepository();
        var viewModel = new EquipmentSetsCatalogViewModel(repository, new ChangeQueue());

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.LoadSelectedDetailsCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Sets);
        Assert.Single(viewModel.Members);
        Assert.Single(viewModel.Thresholds);
        Assert.Equal("damage_bonus", viewModel.Thresholds[0].FieldSystemName);
    }

    [Fact]
    public async Task QueueCreateAndRename_GenerateSqlWithoutClaimingDatabaseState()
    {
        var queue = new ChangeQueue();
        var viewModel = new EquipmentSetsCatalogViewModel(new StubRepository(), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.NewSetName = "new_set";
        viewModel.QueueCreateSetCommand.Execute(null);
        viewModel.SelectedSet!.Name = "renamed_set";

        viewModel.QueueRenameSetCommand.Execute(null);

        Assert.Equal(2, queue.Items.Count);
        Assert.Contains("INSERT INTO equipment_sets", queue.Items[0].ToSql());
        Assert.Contains("name = N'renamed_set'", queue.Items[1].ToSql());
        Assert.Equal("starter_set", viewModel.SelectedSet.Name);
        Assert.DoesNotContain(viewModel.Sets, set => set.Name == "new_set");
    }

    [Fact]
    public async Task QueueMemberAddition_ValidatesDefinitionAndGeneratesInsert()
    {
        var queue = new ChangeQueue();
        var viewModel = new EquipmentSetsCatalogViewModel(new StubRepository(), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.NewMemberDefinition = 100;

        viewModel.QueueAddMemberCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Equal(
            "INSERT INTO equipment_set_members (set_id, definition) VALUES (5, 100)",
            change.ToSql());
    }

    [Fact]
    public async Task NewThreshold_QueuesUpsertAndLeavesDatabaseViewUnchanged()
    {
        var queue = new ChangeQueue();
        var viewModel = new EquipmentSetsCatalogViewModel(new StubRepository(), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.NewRequiredPieces = 4;
        viewModel.NewAggregateFieldId = 50;
        viewModel.NewBonusValue = 1.25;
        viewModel.AddUnsavedThresholdCommand.Execute(null);

        viewModel.QueueThresholdChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Contains("MERGE INTO equipment_set_bonus_thresholds", change.ToSql());
        Assert.Contains("4 AS required_pieces", change.ToSql());
        Assert.Empty(viewModel.Thresholds);
    }

    [Fact]
    public async Task QueueSetDelete_IsDestructiveCascade()
    {
        var queue = new ChangeQueue();
        var viewModel = new EquipmentSetsCatalogViewModel(new StubRepository(), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.QueueDeleteSetCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.True(change.IsDestructive);
        Assert.Contains("DELETE FROM equipment_set_members", change.ToSql());
        Assert.Empty(viewModel.Sets);
    }

    private sealed class StubRepository : IEquipmentSetRepository
    {
        public Task<List<EquipmentSetRow>> LoadAllSetsAsync() =>
            Task.FromResult(new List<EquipmentSetRow>
            {
                new() { SetId = 5, Name = "starter_set" }
            });

        public Task<List<EquipmentSetMemberRow>> LoadMembersAsync(int setId) =>
            Task.FromResult(new List<EquipmentSetMemberRow>
            {
                new() { SetId = setId, Definition = 100, DefinitionName = "starter_module" }
            });

        public Task<List<EquipmentSetThresholdRow>> LoadThresholdsAsync(int setId) =>
            Task.FromResult(new List<EquipmentSetThresholdRow>
            {
                new() { SetId = setId, RequiredPieces = 2, AggregateFieldId = 50, BonusValue = 0.5 }
            });

        public Task<List<AggregateFieldInfo>> LoadAggregateFieldsAsync() =>
            Task.FromResult(new List<AggregateFieldInfo>
            {
                new() { Id = 50, Name = "damage_bonus" }
            });

        public Task<List<SetMemberPickItem>> LoadMemberChoicesAsync() =>
            Task.FromResult(new List<SetMemberPickItem>
            {
                new() { Definition = 100, DefinitionName = "starter_module" }
            });
    }
}
