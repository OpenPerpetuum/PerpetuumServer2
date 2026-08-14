using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Templates;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class RobotTemplateCatalogViewModelTests
{
    [Fact]
    public async Task LoadAndFilter_UsePortableRepositoryResults()
    {
        RobotTemplateRow alpha = CreateRow(10, "alpha_patrol");
        RobotTemplateRow beta = CreateRow(20, "beta_guard");
        var viewModel = new RobotTemplateCatalogViewModel(
            new StubRepository([alpha, beta]),
            new ChangeQueue());

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.FilterText = "guard";

        Assert.Single(viewModel.Rows);
        Assert.Same(beta, viewModel.Rows[0]);
        Assert.Contains("2 robot template", viewModel.StatusMessage);
    }

    [Fact]
    public async Task QueueUpdate_AddsSqlAndRestoresDatabaseBaseline()
    {
        RobotTemplateRow row = CreateRow(10, "alpha_patrol");
        var queue = new ChangeQueue();
        var viewModel = new RobotTemplateCatalogViewModel(new StubRepository([row]), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);
        row.Note = "new note";

        viewModel.QueueSelectedChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Contains("note = N'new note'", change.ToSql());
        Assert.Null(row.Note);
    }

    [Fact]
    public void CreateAndQueueInsert_ProducesInsertAndRemovesUnsavedRow()
    {
        var queue = new ChangeQueue();
        var viewModel = new RobotTemplateCatalogViewModel(new StubRepository([]), queue)
        {
            NewTemplateName = "new_patrol"
        };

        viewModel.CreateTemplateCommand.Execute(null);
        Assert.Single(viewModel.Rows);
        viewModel.SelectedRow!.Description = "[robot=1]";

        viewModel.QueueSelectedChangesCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.Contains("INSERT INTO robottemplates", change.ToSql());
        Assert.Contains("N'new_patrol'", change.ToSql());
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public async Task QueueDelete_MarksChangeDestructiveAndRemovesRow()
    {
        RobotTemplateRow row = CreateRow(10, "alpha_patrol");
        var queue = new ChangeQueue();
        var viewModel = new RobotTemplateCatalogViewModel(new StubRepository([row]), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.QueueDeleteCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.True(change.IsDestructive);
        Assert.Equal("DELETE FROM robottemplates WHERE id = 10", change.ToSql());
        Assert.Empty(viewModel.Rows);
    }

    private static RobotTemplateRow CreateRow(int id, string name)
    {
        return new RobotTemplateRow(new RobotTemplateSnapshot
        {
            Id = id,
            Name = name,
            Description = "[robot=1]"
        });
    }

    private sealed class StubRepository(IReadOnlyList<RobotTemplateRow> rows)
        : IRobotTemplateRepository
    {
        public Task<List<RobotTemplateRow>> LoadAllAsync()
        {
            return Task.FromResult(rows.ToList());
        }
    }
}
