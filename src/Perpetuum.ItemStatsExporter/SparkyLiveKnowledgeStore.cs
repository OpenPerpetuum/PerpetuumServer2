using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyLiveKnowledgeStore
{
    private const int BusyTimeoutMilliseconds = 2000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _connectionString;

    public SparkyLiveKnowledgeStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("A live knowledge database path is required.", nameof(databasePath));
        }

        string fullDatabasePath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDatabasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = BusyTimeoutMilliseconds / 1000
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS LiveKnowledgeCurrent (
                Environment TEXT NOT NULL,
                Provider TEXT NOT NULL,
                SchemaVersion INTEGER NOT NULL,
                CapturedAtUtc TEXT NOT NULL,
                ContentHash TEXT NOT NULL,
                PayloadJson TEXT NOT NULL,
                PRIMARY KEY (Environment, Provider)
            );
            CREATE TABLE IF NOT EXISTS LiveSeasonFinal (
                Environment TEXT NOT NULL,
                SeasonId INTEGER NOT NULL,
                Revision INTEGER NOT NULL,
                ContentHash TEXT NOT NULL,
                CapturedAtUtc TEXT NOT NULL,
                PayloadJson TEXT NOT NULL,
                PRIMARY KEY (Environment, SeasonId, Revision)
            );
            CREATE INDEX IF NOT EXISTS IX_LiveSeasonFinal_Latest
                ON LiveSeasonFinal (Environment, SeasonId, Revision DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SparkyLiveKnowledgeApplyResult> ReplaceCurrentAsync(
        SparkyApiProfile profile,
        SparkyLiveKnowledgeUpdate update,
        CancellationToken cancellationToken)
    {
        ValidateForStore(profile, update, "replace-current");
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var existing = await ReadCurrentMetadataAsync(connection, transaction, profile, update.Provider, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (update.CapturedAtUtc < existing.Value.CapturedAtUtc)
            {
                return new SparkyLiveKnowledgeApplyResult("out_of_order", update.ContentHash);
            }

            if (string.Equals(update.ContentHash, existing.Value.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                if (update.CapturedAtUtc > existing.Value.CapturedAtUtc)
                {
                    await UpdateCurrentCaptureTimeAsync(connection, transaction, profile, update, cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return new SparkyLiveKnowledgeApplyResult("refreshed", update.ContentHash);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new SparkyLiveKnowledgeApplyResult("unchanged", update.ContentHash);
            }
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO LiveKnowledgeCurrent (Environment, Provider, SchemaVersion, CapturedAtUtc, ContentHash, PayloadJson)
            VALUES ($environment, $provider, $schemaVersion, $capturedAtUtc, $contentHash, $payloadJson)
            ON CONFLICT(Environment, Provider) DO UPDATE SET
                SchemaVersion = excluded.SchemaVersion,
                CapturedAtUtc = excluded.CapturedAtUtc,
                ContentHash = excluded.ContentHash,
                PayloadJson = excluded.PayloadJson;
            """;
        command.Parameters.AddWithValue("$environment", ProfileKey(profile));
        command.Parameters.AddWithValue("$provider", update.Provider);
        command.Parameters.AddWithValue("$schemaVersion", update.SchemaVersion);
        command.Parameters.AddWithValue("$capturedAtUtc", FormatTimestamp(update.CapturedAtUtc));
        command.Parameters.AddWithValue("$contentHash", update.ContentHash.ToLowerInvariant());
        command.Parameters.AddWithValue("$payloadJson", JsonSerializer.Serialize(update, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new SparkyLiveKnowledgeApplyResult("applied", update.ContentHash.ToLowerInvariant());
    }

    public async Task<SparkyLiveKnowledgeApplyResult> RefreshCurrentAsync(
        SparkyApiProfile profile,
        SparkyLiveKnowledgeUpdate update,
        CancellationToken cancellationToken)
    {
        ValidateForStore(profile, update, "refresh-current");
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var existing = await ReadCurrentMetadataAsync(connection, transaction, profile, update.Provider, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SparkyLiveKnowledgeApplyResult("current_missing", update.ContentHash);
        }

        if (!string.Equals(existing.Value.ContentHash, update.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SparkyLiveKnowledgeApplyResult("hash_mismatch", update.ContentHash);
        }

        if (update.CapturedAtUtc < existing.Value.CapturedAtUtc)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SparkyLiveKnowledgeApplyResult("out_of_order", update.ContentHash);
        }

        if (update.CapturedAtUtc == existing.Value.CapturedAtUtc)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SparkyLiveKnowledgeApplyResult("unchanged", update.ContentHash);
        }

        await UpdateCurrentCaptureTimeAsync(connection, transaction, profile, update, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new SparkyLiveKnowledgeApplyResult("refreshed", update.ContentHash);
    }

    public async Task<SparkyLiveKnowledgeApplyResult> FinalizeSeasonAsync(
        SparkyApiProfile profile,
        int routeSeasonId,
        SparkyLiveKnowledgeUpdate update,
        CancellationToken cancellationToken)
    {
        ValidateForStore(profile, update, "finalize-season");
        if (routeSeasonId <= 0 || update.Finalization!.SeasonId != routeSeasonId)
        {
            throw new InvalidDataException("The route season ID does not match the finalization payload.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        int latestRevision = 0;
        string? latestHash = null;
        DateTimeOffset? latestCapture = null;
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = """
                SELECT Revision, ContentHash, CapturedAtUtc
                FROM LiveSeasonFinal
                WHERE Environment = $environment AND SeasonId = $seasonId
                ORDER BY Revision DESC LIMIT 1;
                """;
            read.Parameters.AddWithValue("$environment", ProfileKey(profile));
            read.Parameters.AddWithValue("$seasonId", routeSeasonId);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                latestRevision = reader.GetInt32(0);
                latestHash = reader.GetString(1);
                latestCapture = ParseTimestamp(reader.GetString(2));
            }
        }

        if (string.Equals(latestHash, update.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SparkyLiveKnowledgeApplyResult("duplicate", update.ContentHash, latestRevision);
        }

        if (latestCapture.HasValue && update.CapturedAtUtc <= latestCapture.Value)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SparkyLiveKnowledgeApplyResult("out_of_order", update.ContentHash, latestRevision);
        }

        int revision = latestRevision + 1;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO LiveSeasonFinal (Environment, SeasonId, Revision, ContentHash, CapturedAtUtc, PayloadJson)
                VALUES ($environment, $seasonId, $revision, $contentHash, $capturedAtUtc, $payloadJson);
                """;
            insert.Parameters.AddWithValue("$environment", ProfileKey(profile));
            insert.Parameters.AddWithValue("$seasonId", routeSeasonId);
            insert.Parameters.AddWithValue("$revision", revision);
            insert.Parameters.AddWithValue("$contentHash", update.ContentHash.ToLowerInvariant());
            insert.Parameters.AddWithValue("$capturedAtUtc", FormatTimestamp(update.CapturedAtUtc));
            insert.Parameters.AddWithValue("$payloadJson", JsonSerializer.Serialize(update, JsonOptions));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new SparkyLiveKnowledgeApplyResult("finalized", update.ContentHash.ToLowerInvariant(), revision);
    }

    public async Task<SparkyLiveKnowledgeProfileState> LoadProfileAsync(
        SparkyApiProfile profile,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        SparkyLiveKnowledgeUpdate? current = null;
        await using (var readCurrent = connection.CreateCommand())
        {
            readCurrent.CommandText = """
                SELECT CapturedAtUtc, PayloadJson
                FROM LiveKnowledgeCurrent
                WHERE Environment = $environment AND Provider = 'seasons';
                """;
            readCurrent.Parameters.AddWithValue("$environment", ProfileKey(profile));
            await using var reader = await readCurrent.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var stored = DeserializeUpdate(reader.GetString(1));
                current = stored with { CapturedAtUtc = ParseTimestamp(reader.GetString(0)) };
            }
        }

        var finals = new List<SparkySeasonFinalRecord>();
        await using (var readFinals = connection.CreateCommand())
        {
            readFinals.CommandText = """
                SELECT f.SeasonId, f.Revision, f.ContentHash, f.CapturedAtUtc, f.PayloadJson
                FROM LiveSeasonFinal f
                INNER JOIN (
                    SELECT SeasonId, MAX(Revision) AS Revision
                    FROM LiveSeasonFinal
                    WHERE Environment = $environment
                    GROUP BY SeasonId
                ) latest ON latest.SeasonId = f.SeasonId AND latest.Revision = f.Revision
                WHERE f.Environment = $environment
                ORDER BY f.SeasonId;
                """;
            readFinals.Parameters.AddWithValue("$environment", ProfileKey(profile));
            await using var reader = await readFinals.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var stored = DeserializeUpdate(reader.GetString(4));
                finals.Add(new SparkySeasonFinalRecord(
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    ParseTimestamp(reader.GetString(3)),
                    stored.Finalization ?? throw new InvalidDataException("Stored season finalization is missing its payload.")));
            }
        }

        return new SparkyLiveKnowledgeProfileState(current, finals);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA foreign_keys = ON; PRAGMA synchronous = FULL; PRAGMA busy_timeout = {BusyTimeoutMilliseconds};";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<(string ContentHash, DateTimeOffset CapturedAtUtc)?> ReadCurrentMetadataAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SparkyApiProfile profile,
        string provider,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT ContentHash, CapturedAtUtc FROM LiveKnowledgeCurrent WHERE Environment = $environment AND Provider = $provider;";
        command.Parameters.AddWithValue("$environment", ProfileKey(profile));
        command.Parameters.AddWithValue("$provider", provider);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? (reader.GetString(0), ParseTimestamp(reader.GetString(1)))
            : null;
    }

    private static async Task UpdateCurrentCaptureTimeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SparkyApiProfile profile,
        SparkyLiveKnowledgeUpdate update,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE LiveKnowledgeCurrent SET CapturedAtUtc = $capturedAtUtc WHERE Environment = $environment AND Provider = $provider;";
        command.Parameters.AddWithValue("$capturedAtUtc", FormatTimestamp(update.CapturedAtUtc));
        command.Parameters.AddWithValue("$environment", ProfileKey(profile));
        command.Parameters.AddWithValue("$provider", update.Provider);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateForStore(SparkyApiProfile profile, SparkyLiveKnowledgeUpdate update, string operation)
    {
        if (!string.Equals(update.Operation, operation, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The live knowledge update operation does not match the storage operation.");
        }

        var validation = SparkyLiveKnowledgeValidator.ValidateUpdate(update, profile, update.CapturedAtUtc);
        if (!validation.IsValid)
        {
            throw new InvalidDataException($"The live knowledge update is invalid ({validation.RejectionCode}).");
        }
    }

    private static SparkyLiveKnowledgeUpdate DeserializeUpdate(string json) =>
        JsonSerializer.Deserialize<SparkyLiveKnowledgeUpdate>(json, JsonOptions)
        ?? throw new InvalidDataException("Stored live knowledge payload is empty.");

    private static string ProfileKey(SparkyApiProfile profile) => profile.ToString();

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
