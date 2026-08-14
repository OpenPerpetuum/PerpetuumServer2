using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.Core.Tests.Editing;

public sealed class SqlScriptExporterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "perpetuum-admin-tool-export-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Export_WritesUtf8TransactionAndNeverOverwritesAnExistingFile()
    {
        var exporter = new SqlScriptExporter();
        IPendingChange[] changes = [new RawSqlChange("probe", "SELECT 1;")];
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string first = await exporter.ExportAsync(
            _directory, "native changes", changes, "admin", cancellationToken);
        string second = await exporter.ExportAsync(
            _directory, "native changes", changes, "admin", cancellationToken);

        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
        Assert.Contains(
            "BEGIN TRANSACTION;",
            await File.ReadAllTextAsync(first, cancellationToken));
        Assert.Contains(
            "-- Author: admin",
            await File.ReadAllTextAsync(first, cancellationToken));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(_directory),
            path => path.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Export_RejectsAnEmptyQueue()
    {
        var exporter = new SqlScriptExporter();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            exporter.ExportAsync(
                _directory,
                "changes",
                [],
                cancellationToken: TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
