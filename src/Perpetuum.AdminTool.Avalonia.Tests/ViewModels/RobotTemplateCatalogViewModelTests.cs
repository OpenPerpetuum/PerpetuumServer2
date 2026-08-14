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
            new StubEditorRepository(),
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
        var viewModel = new RobotTemplateCatalogViewModel(
            new StubRepository([row]), new StubEditorRepository(), queue);
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
        var viewModel = new RobotTemplateCatalogViewModel(
            new StubRepository([]), new StubEditorRepository(), queue)
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
        var viewModel = new RobotTemplateCatalogViewModel(
            new StubRepository([row]), new StubEditorRepository(), queue);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.QueueDeleteCommand.Execute(null);

        IPendingChange change = Assert.Single(queue.Items);
        Assert.True(change.IsDestructive);
        Assert.Equal("DELETE FROM robottemplates WHERE id = 10", change.ToSql());
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public async Task StructuredEditor_AppliesRequiredPartsAndPreservesUnknownKeys()
    {
        RobotTemplateRow row = CreateRow(10, "alpha_patrol");
        row.Description = "#items=[|cargo=i2a]";
        var editorRepository = new StubEditorRepository(
        [
            EditorEntity(1, "robot", 0x1),
            EditorEntity(2, "head", 0x150),
            EditorEntity(3, "chassis", 0x250),
            EditorEntity(4, "leg", 0x350),
            EditorEntity(5, "container", 0x30915)
        ]);
        var viewModel = new RobotTemplateCatalogViewModel(
            new StubRepository([row]), editorRepository, new ChangeQueue());
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.LoadStructuredEditorCommand.ExecuteAsync(null);
        viewModel.StructuredEditor!.RobotDefinition = 1;
        viewModel.StructuredEditor.HeadDefinition = 2;
        viewModel.StructuredEditor.ChassisDefinition = 3;
        viewModel.StructuredEditor.LegDefinition = 4;
        viewModel.StructuredEditor.ContainerDefinition = 5;
        viewModel.ApplyStructuredEditorCommand.Execute(null);

        Assert.Contains("#robot=i1", row.Description);
        Assert.Contains("#items=[|cargo=i2a]", row.Description);
        Assert.Null(viewModel.StructuredEditor);
    }

    [Fact]
    public async Task ExportSelected_ShowsPortableSql()
    {
        RobotTemplateRow row = CreateRow(10, "alpha_patrol");
        var viewModel = new RobotTemplateCatalogViewModel(
            new StubRepository([row]), new StubEditorRepository(), new ChangeQueue(), new StubContentExporter());
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.ExportSelectedCommand.ExecuteAsync(null);

        Assert.Equal("robot export 10", viewModel.ExportScript);
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

    private static RobotTemplateEditorEntity EditorEntity(int definition, string name, long categoryFlags)
    {
        return new RobotTemplateEditorEntity
        {
            Definition = definition,
            Name = name,
            CategoryFlags = categoryFlags,
            Enabled = true
        };
    }

    private sealed class StubRepository(IReadOnlyList<RobotTemplateRow> rows)
        : IRobotTemplateRepository
    {
        public Task<List<RobotTemplateRow>> LoadAllAsync()
        {
            return Task.FromResult(rows.ToList());
        }
    }

    private sealed class StubEditorRepository : IRobotTemplateEditorRepository
    {
        private readonly List<RobotTemplateEditorEntity> _rows;

        public StubEditorRepository(IEnumerable<RobotTemplateEditorEntity>? rows = null)
        {
            _rows = rows?.ToList() ?? [];
        }

        public Task<List<RobotTemplateEditorEntity>> LoadAllAsync() =>
            Task.FromResult(_rows);
    }
}
