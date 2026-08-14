using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Templates;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class RobotTemplateRelationsCatalogViewModelTests
{
    [Fact]
    public async Task LoadAndFilter_IncludeJoinedNames()
    {
        RobotTemplateRelationRow alpha = CreateRow(100, 10, "alpha_robot", "patrol");
        RobotTemplateRelationRow beta = CreateRow(200, 20, "beta_robot", "guard");
        var viewModel = new RobotTemplateRelationsCatalogViewModel(
            new StubRepository([alpha, beta]),
            new ChangeQueue());

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.FilterText = "guard";

        Assert.Single(viewModel.Rows);
        Assert.Same(beta, viewModel.Rows[0]);
    }

    [Fact]
    public async Task QueueUpdate_AddsTargetedSqlAndRestoresBaseline()
    {
        RobotTemplateRelationRow row = CreateRow(100, 10, "alpha_robot", "patrol");
        var queue = new ChangeQueue();
        var viewModel = new RobotTemplateRelationsCatalogViewModel(
            new StubRepository([row]),
            queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        row.KillEp = 25;

        viewModel.QueueSelectedChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Equal(
            "UPDATE robottemplaterelation SET killep = 25 WHERE definition = 100",
            change.ToSql());
        Assert.Null(row.KillEp);
    }

    [Fact]
    public void CreateAndQueueInsert_ProducesCompleteInsert()
    {
        var queue = new ChangeQueue();
        var viewModel = new RobotTemplateRelationsCatalogViewModel(
            new StubRepository([]),
            queue)
        {
            NewDefinition = 300,
            NewTemplateId = 30
        };

        viewModel.CreateRelationCommand.Execute(null);
        viewModel.SelectedRow!.RaceId = 2;
        viewModel.QueueSelectedChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Contains("INSERT INTO robottemplaterelation", change.ToSql());
        Assert.Contains("VALUES (300, 30, 0, 2", change.ToSql());
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public async Task QueueDelete_IsDestructive()
    {
        RobotTemplateRelationRow row = CreateRow(100, 10, "alpha_robot", "patrol");
        var queue = new ChangeQueue();
        var viewModel = new RobotTemplateRelationsCatalogViewModel(
            new StubRepository([row]),
            queue);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.QueueDeleteCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.True(change.IsDestructive);
        Assert.Equal("DELETE FROM robottemplaterelation WHERE definition = 100", change.ToSql());
    }

    private static RobotTemplateRelationRow CreateRow(
        int definition,
        int templateId,
        string definitionName,
        string templateName)
    {
        return new RobotTemplateRelationRow(new RobotTemplateRelationSnapshot
        {
            Definition = definition,
            TemplateId = templateId,
            ItemScoreSum = 5,
            RaceId = 1
        })
        {
            DefinitionName = definitionName,
            TemplateName = templateName
        };
    }

    private sealed class StubRepository(IReadOnlyList<RobotTemplateRelationRow> rows)
        : IRobotTemplateRelationRepository
    {
        public Task<List<RobotTemplateRelationRow>> LoadAllAsync()
        {
            return Task.FromResult(rows.ToList());
        }
    }
}
