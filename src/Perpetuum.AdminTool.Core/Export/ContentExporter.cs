using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Export;

public interface IContentExporter
{
    Task<string> ExportItemAsync(int definition, CancellationToken cancellationToken = default);
    Task<string> ExportRobotAsync(int templateId, CancellationToken cancellationToken = default);
    Task<string> ExportSeasonAsync(int seasonId, CancellationToken cancellationToken = default);
}

public interface IContentExporterFactory
{
    IContentExporter Create(ConnectionSettings connection);
}

public sealed class ContentExporterFactory : IContentExporterFactory
{
    public IContentExporter Create(ConnectionSettings connection) => new ContentExporter(connection);
}

public sealed class ContentExporter(ConnectionSettings connection) : IContentExporter
{
    public async Task<string> ExportItemAsync(int definition, CancellationToken cancellationToken = default)
    {
        await using var database = new SqlConnection(connection.BuildConnectionString());
        await database.OpenAsync(cancellationToken);
        List<RawSqlChange> changes = await ItemExporter.ExportAsync(definition, database);
        if (changes.Count == 0) throw new InvalidOperationException($"Entity definition {definition} was not found.");
        return SqlScriptBuilder.Build(changes);
    }

    public Task<string> ExportRobotAsync(int templateId, CancellationToken cancellationToken = default) =>
        RobotExporter.ExportAsync(templateId, connection);

    public Task<string> ExportSeasonAsync(int seasonId, CancellationToken cancellationToken = default) =>
        SeasonExporter.ExportAsync(seasonId, connection);
}
