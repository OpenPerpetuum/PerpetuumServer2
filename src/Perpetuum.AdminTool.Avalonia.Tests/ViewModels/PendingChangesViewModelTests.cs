using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class PendingChangesViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "perpetuum-admin-tool-pending-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void QueueChanges_UpdatePreviewAndEscalateDestructiveConfirmation()
    {
        var queue = new ChangeQueue();
        PendingChangesViewModel viewModel = CreateViewModel(queue);

        queue.Add(new RawSqlChange("safe update", "UPDATE test SET value=1;"));

        Assert.Equal(1, viewModel.PendingChangeCount);
        Assert.Equal("APPLY", viewModel.RequiredConfirmation);
        Assert.Contains("safe update", viewModel.ScriptPreview);
        Assert.False(viewModel.ApplyDirectCommand.CanExecute(null));

        viewModel.ConfirmationText = "APPLY";
        Assert.True(viewModel.ApplyDirectCommand.CanExecute(null));

        queue.Add(new RawSqlChange("delete row", "DELETE FROM test;", isDestructive: true));

        Assert.Equal(string.Empty, viewModel.ConfirmationText);
        Assert.Equal("APPLY DELETE", viewModel.RequiredConfirmation);
        Assert.Equal(1, viewModel.DestructiveCount);
        Assert.False(viewModel.ApplyDirectCommand.CanExecute(null));
    }

    [Fact]
    public async Task ApplyDirect_RequiresExactPhraseAndClearsOnlyAfterSuccess()
    {
        var queue = new ChangeQueue();
        queue.Add(new RawSqlChange("safe update", "UPDATE test SET value=1;"));
        var applier = new RecordingChangeApplier();
        PendingChangesViewModel viewModel = CreateViewModel(queue, applier: applier);

        viewModel.ConfirmationText = "apply";
        Assert.False(viewModel.ApplyDirectCommand.CanExecute(null));

        viewModel.ConfirmationText = "APPLY";
        await viewModel.ApplyDirectCommand.ExecuteAsync(null);

        Assert.Equal(1, applier.CallCount);
        Assert.Empty(queue.Items);
        Assert.Contains("successfully", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ExportScript_PersistsDirectoryAndClearsExportedQueue()
    {
        var queue = new ChangeQueue();
        queue.Add(new RawSqlChange("safe update", "UPDATE test SET value=1;"));
        var exporter = new RecordingScriptExporter();
        AppSettingsStore store = CreateStore();
        var viewModel = new PendingChangesViewModel(
            store,
            queue,
            new RecordingChangeApplier(),
            exporter,
            "admin@example.invalid")
        {
            OutputDirectory = Path.Combine(_directory, "exports")
        };

        await viewModel.ExportScriptCommand.ExecuteAsync(null);

        Assert.Equal(1, exporter.CallCount);
        Assert.Equal("admin@example.invalid", exporter.AuthorEmail);
        Assert.Empty(queue.Items);
        Assert.Equal(viewModel.OutputDirectory, store.Settings.SqlOutputDirectory);
        Assert.True(File.Exists(store.FilePath));
    }

    [Fact]
    public async Task ApplyFailure_PreservesQueueForRetryOrExport()
    {
        var queue = new ChangeQueue();
        queue.Add(new RawSqlChange("safe update", "UPDATE test SET value=1;"));
        PendingChangesViewModel viewModel = CreateViewModel(
            queue,
            applier: new ThrowingChangeApplier(new InvalidOperationException("database unavailable")));
        viewModel.ConfirmationText = "APPLY";

        await viewModel.ApplyDirectCommand.ExecuteAsync(null);

        Assert.Single(queue.Items);
        Assert.True(viewModel.StatusIsError);
        Assert.Contains("database unavailable", viewModel.StatusMessage);
    }

    private PendingChangesViewModel CreateViewModel(
        ChangeQueue queue,
        IChangeApplier? applier = null,
        ISqlScriptExporter? exporter = null)
    {
        return new PendingChangesViewModel(
            CreateStore(),
            queue,
            applier ?? new RecordingChangeApplier(),
            exporter ?? new RecordingScriptExporter(),
            "admin@example.invalid");
    }

    private AppSettingsStore CreateStore()
    {
        return new AppSettingsStore(Path.Combine(_directory, "settings.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class RecordingChangeApplier : IChangeApplier
    {
        public int CallCount { get; private set; }

        public Task ExecuteAsync(
            IReadOnlyList<IPendingChange> changes,
            string? authorEmail = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingChangeApplier(Exception exception) : IChangeApplier
    {
        public Task ExecuteAsync(
            IReadOnlyList<IPendingChange> changes,
            string? authorEmail = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException(exception);
        }
    }

    private sealed class RecordingScriptExporter : ISqlScriptExporter
    {
        public int CallCount { get; private set; }
        public string? AuthorEmail { get; private set; }

        public Task<string> ExportAsync(
            string outputDirectory,
            string filePrefix,
            IReadOnlyList<IPendingChange> changes,
            string? authorEmail = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            AuthorEmail = authorEmail;
            return Task.FromResult(Path.Combine(outputDirectory, "changes.sql"));
        }
    }
}
