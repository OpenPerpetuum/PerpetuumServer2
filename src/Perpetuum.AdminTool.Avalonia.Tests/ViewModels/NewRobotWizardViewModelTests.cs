using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.NewRobot;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class NewRobotWizardViewModelTests
{
    [Fact]
    public void QueueRobot_CreatesPartsBonusesTemplateAndRelation()
    {
        var queue = new ChangeQueue();
        var viewModel = new NewRobotWizardViewModel(
            new StubItemRepository(),
            new StubRobotRepository(),
            new StubEntityRepository(),
            queue);
        viewModel.BasicPanel.DefinitionName = "def_native_robot";
        viewModel.BasicPanel.CategoryFlags = 256;
        viewModel.HeadStatsPanel.Rows.Add(new NewStatRow { FieldId = 10, NewValue = 1.5 });
        viewModel.BonusesPanel.Rows.Add(new NewBonusRow
        {
            ExtensionId = 4,
            NewBonus = 0.05,
            TargetPropertyId = 10,
            EffectEnhancer = true,
            Note = "native test"
        });
        viewModel.TemplateRelationPanelViewModel.RaceId = 2;

        viewModel.QueueItemCommand.Execute(null);

        RawSqlChange change = Assert.IsType<RawSqlChange>(Assert.Single(queue.Items));
        string sql = change.ToSql();
        Assert.Contains("DECLARE @robotDef", sql);
        Assert.Contains("DECLARE @headDef", sql);
        Assert.Contains("DECLARE @chassisDef", sql);
        Assert.Contains("DECLARE @legDef", sql);
        Assert.Contains("DECLARE @inventoryDef", sql);
        Assert.Contains("INSERT INTO chassisbonus", sql);
        Assert.Contains("INSERT INTO robottemplates", sql);
        Assert.Contains("INSERT INTO robottemplaterelation", sql);
        Assert.Contains("#head=n", sql);
        Assert.Contains("def_native_robot_inventory_desc", queue.PendingNewEntityNames);
    }

    [Fact]
    public void QueueRobot_RejectsDuplicateChassisBonuses()
    {
        var queue = new ChangeQueue();
        var viewModel = new NewRobotWizardViewModel(
            new StubItemRepository(), new StubRobotRepository(), new StubEntityRepository(), queue);
        viewModel.BasicPanel.DefinitionName = "def_native_robot";
        viewModel.BasicPanel.CategoryFlags = 1;
        viewModel.BonusesPanel.Rows.Add(new NewBonusRow { ExtensionId = 4, TargetPropertyId = 10 });
        viewModel.BonusesPanel.Rows.Add(new NewBonusRow { ExtensionId = 4, TargetPropertyId = 10 });

        viewModel.QueueItemCommand.Execute(null);

        Assert.Empty(queue.Items);
        Assert.True(viewModel.StatusIsError);
        Assert.Contains("duplicate", viewModel.StatusMessage);
    }

    private sealed class StubEntityRepository : IEntityRepository
    {
        public Task<EntitiesSnapshot> LoadAsync() => Task.FromResult(new EntitiesSnapshot());
    }

    private sealed class StubItemRepository : INewItemRepository
    {
        public Task<NewItemLookups> LoadAsync(
            IReadOnlyList<AggregateFieldInfo> aggregateFields,
            IReadOnlyList<EntityPickItem> entities,
            Dictionary<string, string>? englishNames = null) =>
            Task.FromResult(new NewItemLookups());

        public Task<CloneExtendedData> LoadCloneExtendedAsync(int definition) =>
            Task.FromResult(new CloneExtendedData());
    }

    private sealed class StubRobotRepository : INewRobotRepository
    {
        public Task<RobotTemplateRelationData?> LoadTemplateRelationAsync(int robotDefinition) =>
            Task.FromResult<RobotTemplateRelationData?>(null);

        public Task<IReadOnlyList<ChassisBonusRow>> LoadChassisBonusesAsync(int chassisDefinition) =>
            Task.FromResult<IReadOnlyList<ChassisBonusRow>>([]);
    }
}
