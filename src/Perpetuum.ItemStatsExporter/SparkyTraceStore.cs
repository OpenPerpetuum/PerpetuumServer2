using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyTraceStore : ISparkyProfileRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;

    public SparkyTraceStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("A trace database path is required.", nameof(databasePath));
        }

        string fullDatabasePath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDatabasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = 1
        }.ToString();
        InitializeSchema();
    }

    public async Task<SparkyTraceAppendResult> AppendAsync(
        SparkyTraceEvent traceEvent,
        SparkyApiProfile environment,
        bool captureContent,
        CancellationToken cancellationToken = default) =>
        await AppendBatchAsync([traceEvent], environment, captureContent, cancellationToken).ConfigureAwait(false);

    public async Task<SparkyTraceAppendResult> AppendBatchAsync(
        IReadOnlyList<SparkyTraceEvent> events,
        SparkyApiProfile environment,
        bool captureContent,
        CancellationToken cancellationToken = default)
    {
        if (events.Count is < 1 or > SparkyTraceEventValidator.MaxBatchCount ||
            events.Any(traceEvent => !SparkyTraceEventValidator.IsValidStoreEvent(traceEvent)))
        {
            throw new InvalidDataException("The Sparky trace batch is invalid.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var acknowledged = new HashSet<Guid>();
        var conflicts = new HashSet<Guid>();

        foreach (var sourceEvent in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var storedEvent = captureContent
                ? sourceEvent
                : sourceEvent with
                {
                    Question = null,
                    Answer = null,
                    SearchQuery = null,
                    EntityCandidates = null
                };
            string metadataHash = ComputeMetadataHash(storedEvent);
            await using var insert = connection.CreateCommand();
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = """
                INSERT INTO Trace (
                    Environment, EventId, RequestId, Source, Stage, Outcome, RecordedAtUtc,
                    ElapsedMilliseconds, DetailCode, Intent, Focus, SearchQuery,
                    EntityCandidatesJson, SelectedSourceIdsJson, ModelId, ConfigRevision,
                    BundleVersion, Question, Answer, FinishStatus, MetadataHash)
                VALUES (
                    $environment, $eventId, $requestId, $source, $stage, $outcome, $recordedAtUtc,
                    $elapsedMilliseconds, $detailCode, $intent, $focus, $searchQuery,
                    $entityCandidatesJson, $selectedSourceIdsJson, $modelId, $configRevision,
                    $bundleVersion, $question, $answer, $finishStatus, $metadataHash)
                ON CONFLICT(Environment, EventId) DO NOTHING;
                """;
            BindEvent(insert, storedEvent, environment, metadataHash);
            int inserted = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (inserted > 0)
            {
                acknowledged.Add(storedEvent.EventId);
                continue;
            }

            await using var existing = connection.CreateCommand();
            existing.Transaction = (SqliteTransaction)transaction;
            existing.CommandText = "SELECT MetadataHash FROM Trace WHERE Environment = $environment AND EventId = $eventId;";
            existing.Parameters.AddWithValue("$environment", environment.ToString());
            existing.Parameters.AddWithValue("$eventId", storedEvent.EventId.ToString("D"));
            string? existingHash = await existing.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            if (string.Equals(existingHash, metadataHash, StringComparison.Ordinal))
            {
                acknowledged.Add(storedEvent.EventId);
            }
            else
            {
                conflicts.Add(storedEvent.EventId);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new SparkyTraceAppendResult(acknowledged.ToArray(), conflicts.ToArray());
    }

    public async Task<SparkyTraceTimeline?> GetTraceAsync(
        Guid requestId,
        SparkyApiProfile environment,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A request ID is required.", nameof(requestId));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EventId, RequestId, Source, Stage, Outcome, RecordedAtUtc, ElapsedMilliseconds,
                   DetailCode, Intent, Focus, SearchQuery, EntityCandidatesJson, SelectedSourceIdsJson,
                   ModelId, ConfigRevision, BundleVersion, Question, Answer, FinishStatus
            FROM Trace
            WHERE Environment = $environment AND RequestId = $requestId
            ORDER BY RecordedAtUtc, EventId;
            """;
        command.Parameters.AddWithValue("$environment", environment.ToString());
        command.Parameters.AddWithValue("$requestId", requestId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var events = new List<SparkyTraceEvent>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            events.Add(ReadEvent(reader));
        }

        return events.Count == 0 ? null : new SparkyTraceTimeline(requestId, environment, events);
    }

    public async Task<IReadOnlyList<SparkyTraceSummary>> QueryTracesAsync(
        SparkyTraceQuery query,
        CancellationToken cancellationToken = default)
    {
        int limit = Math.Clamp(query.Limit, 1, 100);
        if (query.FromUtc is { Offset: var fromOffset } && fromOffset != TimeSpan.Zero ||
            query.ToUtc is { Offset: var toOffset } && toOffset != TimeSpan.Zero ||
            query.FromUtc > query.ToUtc ||
            query.Source is not null && !SparkyTraceEventValidator.IsAllowedSource(query.Source, allowAiSource: true) ||
            query.Outcome is not null && !SparkyTraceEventValidator.IsAllowedOutcome(query.Outcome) ||
            query.Label is not null && !SparkyTraceEventValidator.IsAllowedFeedbackLabel(query.Label))
        {
            throw new ArgumentException("Trace query dates must be UTC and in chronological order.", nameof(query));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT RequestId
            FROM Trace
            WHERE Environment = $environment
              AND ($fromUtc IS NULL OR RecordedAtUtc >= $fromUtc)
              AND ($toUtc IS NULL OR RecordedAtUtc <= $toUtc)
            GROUP BY RequestId
            HAVING ($source IS NULL OR MAX(CASE WHEN Source = $source THEN 1 ELSE 0 END) = 1)
               AND ($outcome IS NULL OR MAX(CASE WHEN Outcome = $outcome THEN 1 ELSE 0 END) = 1)
               AND ($label IS NULL OR EXISTS (
                    SELECT 1 FROM Feedback
                    WHERE Feedback.Environment = Trace.Environment
                      AND Feedback.RequestId = Trace.RequestId
                      AND Feedback.Label = $label))
            ORDER BY MAX(RecordedAtUtc) DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$environment", query.Environment.ToString());
        command.Parameters.AddWithValue("$fromUtc", query.FromUtc is null ? DBNull.Value : FormatTimestamp(query.FromUtc.Value));
        command.Parameters.AddWithValue("$toUtc", query.ToUtc is null ? DBNull.Value : FormatTimestamp(query.ToUtc.Value));
        command.Parameters.AddWithValue("$source", (object?)query.Source ?? DBNull.Value);
        command.Parameters.AddWithValue("$outcome", (object?)query.Outcome ?? DBNull.Value);
        command.Parameters.AddWithValue("$label", (object?)query.Label ?? DBNull.Value);
        command.Parameters.AddWithValue("$limit", limit);

        var requestIds = new List<Guid>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                requestIds.Add(Guid.Parse(reader.GetString(0)));
            }
        }

        var summaries = new List<SparkyTraceSummary>(requestIds.Count);
        foreach (Guid requestId in requestIds)
        {
            var timeline = await GetTraceAsync(requestId, query.Environment, cancellationToken).ConfigureAwait(false);
            if (timeline is null || timeline.Events.Count == 0)
            {
                continue;
            }

            var timelineEvents = timeline.Events;
            var labels = await GetFeedbackLabelsAsync(requestId, query.Environment, cancellationToken).ConfigureAwait(false);
            summaries.Add(new SparkyTraceSummary(
                requestId,
                query.Environment,
                timelineEvents[0].RecordedAtUtc,
                timelineEvents[^1].RecordedAtUtc,
                timelineEvents.LastOrDefault(item => item.Source is "Game" or "Discord")?.Source,
                (timelineEvents.LastOrDefault(item => item.Stage is "answer" or "delivery" or "api") ?? timelineEvents[^1]).Outcome,
                timelineEvents.FirstOrDefault(item => item.Question is not null)?.Question,
                timelineEvents.LastOrDefault(item => item.Answer is not null)?.Answer,
                labels));
        }

        return summaries;
    }

    public async Task<IReadOnlyList<string>> GetFeedbackLabelsAsync(
        Guid requestId,
        SparkyApiProfile environment,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Label FROM Feedback WHERE Environment = $environment AND RequestId = $requestId ORDER BY Label;";
        command.Parameters.AddWithValue("$environment", environment.ToString());
        command.Parameters.AddWithValue("$requestId", requestId.ToString("D"));
        var labels = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            labels.Add(reader.GetString(0));
        }

        return labels;
    }

    public async Task<IReadOnlyList<SparkyProfileRevisionRecord>> GetProfileRevisionsAsync(
        SparkyApiProfile profile,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT RevisionId, CreatedAtUtc, ActivatedAtUtc, ConfigurationJson, BundleVersion, IsActive, IsDraft
            FROM ProfileRevision WHERE Environment = $environment
            ORDER BY CreatedAtUtc DESC, RevisionId DESC;
            """;
        command.Parameters.AddWithValue("$environment", profile.ToString());
        var revisions = new List<SparkyProfileRevisionRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            revisions.Add(new SparkyProfileRevisionRecord(
                profile,
                reader.GetString(0),
                DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.IsDBNull(2) ? null : DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5) != 0,
                reader.GetInt32(6) != 0));
        }

        return revisions;
    }

    public async Task SaveProfileRevisionAsync(
        SparkyProfileRevisionRecord revision,
        string actor,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (revision.Profile is not (SparkyApiProfile.Dev or SparkyApiProfile.Live) ||
            string.IsNullOrWhiteSpace(revision.RevisionId) || revision.RevisionId.Length > 80 ||
            revision.ConfigurationJson.Length > 64 * 1024 || revision.BundleVersion.Length > 64 ||
            string.IsNullOrWhiteSpace(actor) || actor.Length > 80 || reason?.Length > 500)
        {
            throw new InvalidDataException("The Sparky profile revision is invalid.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO ProfileRevision (Environment, RevisionId, CreatedAtUtc, ConfigurationJson, BundleVersion, IsActive, IsDraft, ActivatedAtUtc)
            VALUES ($environment, $revisionId, $createdAtUtc, $configurationJson, $bundleVersion, 0, 1, NULL);
            """;
        command.Parameters.AddWithValue("$environment", revision.Profile.ToString());
        command.Parameters.AddWithValue("$revisionId", revision.RevisionId);
        command.Parameters.AddWithValue("$createdAtUtc", FormatTimestamp(revision.CreatedAtUtc));
        command.Parameters.AddWithValue("$configurationJson", revision.ConfigurationJson);
        command.Parameters.AddWithValue("$bundleVersion", revision.BundleVersion);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await InsertAuditAsync(connection, (SqliteTransaction)transaction, new SparkyProfileAuditRecord(
            Guid.NewGuid().ToString("N"), revision.Profile, actor, "draft_saved", revision.RevisionId,
            "success", ["draft"], reason, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ActivateProfileRevisionAsync(
        SparkyApiProfile profile,
        string revisionId,
        string actor,
        string action,
        string? sourceRevisionId,
        string? reason,
        IReadOnlyList<string> changedFields,
        CancellationToken cancellationToken = default)
    {
        if (profile is not (SparkyApiProfile.Dev or SparkyApiProfile.Live) ||
            revisionId.Length is < 1 or > 80 || actor.Length is < 1 or > 80 ||
            action.Length is < 1 or > 40 || reason?.Length > 500 || changedFields.Count > 32 ||
            changedFields.Any(field => string.IsNullOrWhiteSpace(field) || field.Length > 80))
        {
            throw new InvalidDataException("The Sparky profile activation request is invalid.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var exists = connection.CreateCommand())
        {
            exists.Transaction = (SqliteTransaction)transaction;
            exists.CommandText = "SELECT 1 FROM ProfileRevision WHERE Environment = $environment AND RevisionId = $revisionId AND IsDraft = 1 LIMIT 1;";
            exists.Parameters.AddWithValue("$environment", profile.ToString());
            exists.Parameters.AddWithValue("$revisionId", revisionId);
            if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        await using (var activate = connection.CreateCommand())
        {
            activate.Transaction = (SqliteTransaction)transaction;
            activate.CommandText = """
                UPDATE ProfileRevision SET IsActive = 0 WHERE Environment = $environment;
                UPDATE ProfileRevision SET IsActive = 1, IsDraft = 0, ActivatedAtUtc = $activatedAtUtc
                WHERE Environment = $environment AND RevisionId = $revisionId;
                """;
            activate.Parameters.AddWithValue("$environment", profile.ToString());
            activate.Parameters.AddWithValue("$revisionId", revisionId);
            activate.Parameters.AddWithValue("$activatedAtUtc", FormatTimestamp(DateTimeOffset.UtcNow));
            await activate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await InsertAuditAsync(connection, (SqliteTransaction)transaction, new SparkyProfileAuditRecord(
            Guid.NewGuid().ToString("N"), profile, actor, action, sourceRevisionId,
            "success", changedFields, reason, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<SparkyProfileAuditRecord>> GetProfileAuditAsync(
        SparkyApiProfile profile,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT AuditId, Actor, Action, SourceRevisionId, Result, ChangedFieldsJson, Reason, CreatedAtUtc
            FROM Audit WHERE Environment = $environment ORDER BY CreatedAtUtc DESC LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$environment", profile.ToString());
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 200));
        var entries = new List<SparkyProfileAuditRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new SparkyProfileAuditRecord(
                reader.GetString(0), profile, reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : JsonSerializer.Deserialize<string[]>(reader.GetString(5), JsonOptions),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }

        return entries;
    }

    public async Task<bool> AddFeedbackAsync(
        SparkyTraceFeedback feedback,
        SparkyApiProfile environment,
        CancellationToken cancellationToken = default)
    {
        if (!SparkyTraceEventValidator.IsValidFeedback(feedback))
        {
            throw new InvalidDataException("The Sparky feedback record is invalid.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var exists = connection.CreateCommand())
        {
            exists.Transaction = (SqliteTransaction)transaction;
            exists.CommandText = "SELECT 1 FROM Trace WHERE Environment = $environment AND RequestId = $requestId LIMIT 1;";
            exists.Parameters.AddWithValue("$environment", environment.ToString());
            exists.Parameters.AddWithValue("$requestId", feedback.RequestId.ToString("D"));
            if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO Feedback (Environment, FeedbackId, RequestId, Label, Note, CreatedAtUtc)
            VALUES ($environment, $feedbackId, $requestId, $label, $note, $createdAtUtc)
            ON CONFLICT(Environment, FeedbackId) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$environment", environment.ToString());
        command.Parameters.AddWithValue("$feedbackId", feedback.FeedbackId.ToString("D"));
        command.Parameters.AddWithValue("$requestId", feedback.RequestId.ToString("D"));
        command.Parameters.AddWithValue("$label", feedback.Label);
        command.Parameters.AddWithValue("$note", (object?)feedback.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", FormatTimestamp(feedback.CreatedAtUtc));
        bool inserted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return inserted;
    }

    public async Task<SparkyEvaluationExample?> CreateEvaluationExampleAsync(
        Guid requestId,
        SparkyApiProfile environment,
        string? expectedIntent,
        IReadOnlyList<string>? expectedProperties,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty || environment is not (SparkyApiProfile.Dev or SparkyApiProfile.Live) ||
            !SparkyTraceEventValidator.IsValidEvaluationExpectation(expectedIntent, expectedProperties))
        {
            throw new InvalidDataException("The Sparky evaluation expectation is invalid.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        string? question;
        await using (var getQuestion = connection.CreateCommand())
        {
            getQuestion.Transaction = (SqliteTransaction)transaction;
            getQuestion.CommandText = "SELECT Question FROM Trace WHERE Environment = $environment AND RequestId = $requestId AND Question IS NOT NULL ORDER BY RecordedAtUtc, EventId LIMIT 1;";
            getQuestion.Parameters.AddWithValue("$environment", environment.ToString());
            getQuestion.Parameters.AddWithValue("$requestId", requestId.ToString("D"));
            question = await getQuestion.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        }

        if (string.IsNullOrWhiteSpace(question) || question.Length > 2000)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var example = new SparkyEvaluationExample(
            Guid.NewGuid(), requestId, question, expectedIntent, expectedProperties!.ToArray(), DateTimeOffset.UtcNow);
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = """
                INSERT INTO EvaluationExample (Environment, ExampleId, RequestId, Question, ExpectedIntent, ExpectedPropertiesJson, CreatedAtUtc)
                VALUES ($environment, $exampleId, $requestId, $question, $expectedIntent, $expectedPropertiesJson, $createdAtUtc);
                """;
            insert.Parameters.AddWithValue("$environment", environment.ToString());
            insert.Parameters.AddWithValue("$exampleId", example.ExampleId.ToString("D"));
            insert.Parameters.AddWithValue("$requestId", requestId.ToString("D"));
            insert.Parameters.AddWithValue("$question", example.Question);
            insert.Parameters.AddWithValue("$expectedIntent", (object?)expectedIntent ?? DBNull.Value);
            insert.Parameters.AddWithValue("$expectedPropertiesJson", JsonSerializer.Serialize(example.ExpectedProperties, JsonOptions));
            insert.Parameters.AddWithValue("$createdAtUtc", FormatTimestamp(example.CreatedAtUtc));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return example;
    }

    public async Task<IReadOnlyList<SparkyEvaluationExample>> GetEvaluationExamplesAsync(
        SparkyApiProfile environment,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ExampleId, RequestId, Question, ExpectedIntent, ExpectedPropertiesJson, CreatedAtUtc
            FROM EvaluationExample WHERE Environment = $environment AND Question IS NOT NULL
            ORDER BY CreatedAtUtc DESC, ExampleId DESC LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$environment", environment.ToString());
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));
        var examples = new List<SparkyEvaluationExample>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            examples.Add(new SparkyEvaluationExample(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                ReadNullableString(reader, 3),
                JsonSerializer.Deserialize<string[]>(reader.GetString(4), JsonOptions) ?? [],
                DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }

        return examples;
    }

    public async Task SaveReplayAsync(SparkyReplayRun replay, CancellationToken cancellationToken = default)
    {
        if (!IsValidReplay(replay))
            throw new InvalidDataException("The Sparky replay result is invalid or too large.");
        string resultsJson = JsonSerializer.Serialize(replay.Results, JsonOptions);
        if (System.Text.Encoding.UTF8.GetByteCount(resultsJson) > SparkyReplayService.MaximumStoredResultBytes)
            throw new InvalidDataException("The Sparky replay result is too large to persist.");

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ReplayRun (Environment, ReplayId, ActiveRevisionId, CandidateRevisionId, CreatedAtUtc, ResultsJson)
            VALUES ($environment, $replayId, $activeRevisionId, $candidateRevisionId, $createdAtUtc, $resultsJson);
            """;
        command.Parameters.AddWithValue("$environment", replay.Environment.ToString());
        command.Parameters.AddWithValue("$replayId", replay.ReplayId.ToString("D"));
        command.Parameters.AddWithValue("$activeRevisionId", replay.ActiveRevisionId);
        command.Parameters.AddWithValue("$candidateRevisionId", replay.CandidateRevisionId);
        command.Parameters.AddWithValue("$createdAtUtc", FormatTimestamp(replay.CreatedAtUtc));
        command.Parameters.AddWithValue("$resultsJson", resultsJson);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SparkyReplayRun?> GetReplayAsync(
        Guid replayId,
        SparkyApiProfile environment,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ActiveRevisionId, CandidateRevisionId, CreatedAtUtc, ResultsJson
            FROM ReplayRun WHERE Environment = $environment AND ReplayId = $replayId LIMIT 1;
            """;
        command.Parameters.AddWithValue("$environment", environment.ToString());
        command.Parameters.AddWithValue("$replayId", replayId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;
        var resultsJson = ReadNullableString(reader, 3);
        return new SparkyReplayRun(
            replayId,
            environment,
            reader.GetString(0),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            resultsJson is null ? [] : JsonSerializer.Deserialize<SparkyReplayComparison[]>(resultsJson, JsonOptions) ?? [],
            ContentExpired: resultsJson is null);
    }

    public async Task<SparkyTraceRetentionResult> ApplyRetentionAsync(
        TimeSpan contentRetention,
        TimeSpan metadataRetention,
        DateTimeOffset? nowUtc = null,
        CancellationToken cancellationToken = default,
        SparkyApiProfile? environment = null)
    {
        if (contentRetention <= TimeSpan.Zero || metadataRetention <= TimeSpan.Zero || contentRetention > metadataRetention)
        {
            throw new ArgumentOutOfRangeException(nameof(contentRetention), "Retention durations must be positive and metadata retention must be at least content retention.");
        }

        DateTimeOffset now = nowUtc ?? DateTimeOffset.UtcNow;
        if (now.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Retention time must be UTC.", nameof(nowUtc));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var clearContent = connection.CreateCommand();
        clearContent.Transaction = (SqliteTransaction)transaction;
        clearContent.CommandText = """
            UPDATE Trace SET Question = NULL, Answer = NULL, SearchQuery = NULL, EntityCandidatesJson = NULL
            WHERE RecordedAtUtc < $contentCutoff AND
              (Question IS NOT NULL OR Answer IS NOT NULL OR SearchQuery IS NOT NULL OR EntityCandidatesJson IS NOT NULL)
              AND ($environment IS NULL OR Environment = $environment);
            """;
        clearContent.Parameters.AddWithValue("$contentCutoff", FormatTimestamp(now - contentRetention));
        clearContent.Parameters.AddWithValue("$environment", (object?)environment?.ToString() ?? DBNull.Value);
        int cleared = await clearContent.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var clearReplayContent = connection.CreateCommand();
        clearReplayContent.Transaction = (SqliteTransaction)transaction;
        clearReplayContent.CommandText = """
            UPDATE ReplayRun SET ResultsJson = NULL
            WHERE CreatedAtUtc < $contentCutoff AND ResultsJson IS NOT NULL
              AND ($environment IS NULL OR Environment = $environment);
            """;
        clearReplayContent.Parameters.AddWithValue("$contentCutoff", FormatTimestamp(now - contentRetention));
        clearReplayContent.Parameters.AddWithValue("$environment", (object?)environment?.ToString() ?? DBNull.Value);
        int replayContentCleared = await clearReplayContent.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var clearEvaluationQuestions = connection.CreateCommand();
        clearEvaluationQuestions.Transaction = (SqliteTransaction)transaction;
        clearEvaluationQuestions.CommandText = """
            UPDATE EvaluationExample SET Question = NULL
            WHERE Question IS NOT NULL AND EXISTS (
                SELECT 1 FROM Trace
                WHERE Trace.Environment = EvaluationExample.Environment
                  AND Trace.RequestId = EvaluationExample.RequestId
                  AND Trace.RecordedAtUtc < $contentCutoff)
              AND ($environment IS NULL OR Environment = $environment);
            """;
        clearEvaluationQuestions.Parameters.AddWithValue("$contentCutoff", FormatTimestamp(now - contentRetention));
        clearEvaluationQuestions.Parameters.AddWithValue("$environment", (object?)environment?.ToString() ?? DBNull.Value);
        int evaluationQuestionsCleared = await clearEvaluationQuestions.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var clearFeedbackNotes = connection.CreateCommand();
        clearFeedbackNotes.Transaction = (SqliteTransaction)transaction;
        clearFeedbackNotes.CommandText = """
            UPDATE Feedback SET Note = NULL
            WHERE Note IS NOT NULL AND CreatedAtUtc < $contentCutoff
              AND ($environment IS NULL OR Environment = $environment);
            """;
        clearFeedbackNotes.Parameters.AddWithValue("$contentCutoff", FormatTimestamp(now - contentRetention));
        clearFeedbackNotes.Parameters.AddWithValue("$environment", (object?)environment?.ToString() ?? DBNull.Value);
        int feedbackNotesCleared = await clearFeedbackNotes.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        int deleted = 0;
        foreach (string statement in new[]
        {
            "DELETE FROM Feedback WHERE NOT EXISTS (SELECT 1 FROM Trace WHERE Trace.Environment = Feedback.Environment AND Trace.RequestId = Feedback.RequestId AND Trace.RecordedAtUtc >= $metadataCutoff) AND ($environment IS NULL OR Environment = $environment);",
            "DELETE FROM EvaluationExample WHERE NOT EXISTS (SELECT 1 FROM Trace WHERE Trace.Environment = EvaluationExample.Environment AND Trace.RequestId = EvaluationExample.RequestId AND Trace.RecordedAtUtc >= $metadataCutoff) AND ($environment IS NULL OR Environment = $environment);",
            "DELETE FROM ReplayRun WHERE CreatedAtUtc < $metadataCutoff AND ($environment IS NULL OR Environment = $environment);",
            "DELETE FROM Trace WHERE RecordedAtUtc < $metadataCutoff AND ($environment IS NULL OR Environment = $environment);"
        })
        {
            await using var delete = connection.CreateCommand();
            delete.Transaction = (SqliteTransaction)transaction;
            delete.CommandText = statement;
            delete.Parameters.AddWithValue("$metadataCutoff", FormatTimestamp(now - metadataRetention));
            delete.Parameters.AddWithValue("$environment", (object?)environment?.ToString() ?? DBNull.Value);
            deleted += await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new SparkyTraceRetentionResult(cleared, deleted, replayContentCleared, evaluationQuestionsCleared, feedbackNotesCleared);
    }

    private void InitializeSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using (var pragmas = connection.CreateCommand())
        {
            pragmas.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=1000; PRAGMA foreign_keys=ON;";
            pragmas.ExecuteNonQuery();
        }

        using var schema = connection.CreateCommand();
        schema.CommandText = """
            CREATE TABLE IF NOT EXISTS Trace (
                Environment TEXT NOT NULL CHECK (Environment IN ('Dev', 'Live')),
                EventId TEXT NOT NULL,
                RequestId TEXT NOT NULL,
                Source TEXT NOT NULL,
                Stage TEXT NOT NULL,
                Outcome TEXT NOT NULL,
                RecordedAtUtc TEXT NOT NULL,
                ElapsedMilliseconds INTEGER NULL,
                DetailCode TEXT NULL,
                Intent TEXT NULL,
                Focus TEXT NULL,
                SearchQuery TEXT NULL,
                EntityCandidatesJson TEXT NULL,
                SelectedSourceIdsJson TEXT NULL,
                ModelId TEXT NULL,
                ConfigRevision TEXT NULL,
                BundleVersion TEXT NULL,
                Question TEXT NULL,
                Answer TEXT NULL,
                FinishStatus TEXT NULL,
                MetadataHash TEXT NOT NULL,
                PRIMARY KEY (Environment, EventId)
            );
            CREATE INDEX IF NOT EXISTS IX_Trace_RequestId ON Trace (Environment, RequestId, RecordedAtUtc, EventId);
            CREATE INDEX IF NOT EXISTS IX_Trace_RecordedAtUtc ON Trace (Environment, RecordedAtUtc);
            CREATE INDEX IF NOT EXISTS IX_Trace_Source_Outcome ON Trace (Environment, Source, Outcome, RecordedAtUtc);

            CREATE TABLE IF NOT EXISTS Feedback (
                Environment TEXT NOT NULL CHECK (Environment IN ('Dev', 'Live')),
                FeedbackId TEXT NOT NULL,
                RequestId TEXT NOT NULL,
                Label TEXT NOT NULL,
                Note TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                PRIMARY KEY (Environment, FeedbackId)
            );
            CREATE INDEX IF NOT EXISTS IX_Feedback_RequestId ON Feedback (Environment, RequestId, CreatedAtUtc);

            CREATE TABLE IF NOT EXISTS EvaluationExample (
                Environment TEXT NOT NULL CHECK (Environment IN ('Dev', 'Live')),
                ExampleId TEXT NOT NULL,
                RequestId TEXT NOT NULL,
                Question TEXT NULL,
                ExpectedIntent TEXT NULL,
                ExpectedPropertiesJson TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                PRIMARY KEY (Environment, ExampleId)
            );

            CREATE TABLE IF NOT EXISTS ReplayRun (
                Environment TEXT NOT NULL CHECK (Environment IN ('Dev', 'Live')),
                ReplayId TEXT NOT NULL,
                ActiveRevisionId TEXT NOT NULL,
                CandidateRevisionId TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                ResultsJson TEXT NULL,
                PRIMARY KEY (Environment, ReplayId)
            );

            CREATE TABLE IF NOT EXISTS ProfileRevision (
                Environment TEXT NOT NULL CHECK (Environment IN ('Dev', 'Live')),
                RevisionId TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                ActivatedAtUtc TEXT NULL,
                ConfigurationJson TEXT NOT NULL,
                BundleVersion TEXT NULL,
                IsActive INTEGER NOT NULL DEFAULT 0,
                IsDraft INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (Environment, RevisionId)
            );

            CREATE TABLE IF NOT EXISTS Audit (
                AuditId TEXT PRIMARY KEY,
                Environment TEXT NOT NULL CHECK (Environment IN ('Dev', 'Live')),
                Actor TEXT NOT NULL,
                Action TEXT NOT NULL,
                SourceRevisionId TEXT NULL,
                Result TEXT NOT NULL,
                ChangedFieldsJson TEXT NULL,
                Reason TEXT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            """;
        schema.ExecuteNonQuery();
        EnsureColumn(connection, "Trace", "FinishStatus", "TEXT NULL");
        EnsureColumn(connection, "ProfileRevision", "ActivatedAtUtc", "TEXT NULL");
        EnsureColumn(connection, "ProfileRevision", "IsDraft", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Audit", "Reason", "TEXT NULL");
        EnsureColumn(connection, "EvaluationExample", "Question", "TEXT NULL");
    }

    private static async Task InsertAuditAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SparkyProfileAuditRecord entry,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Audit (AuditId, Environment, Actor, Action, SourceRevisionId, Result, ChangedFieldsJson, Reason, CreatedAtUtc)
            VALUES ($auditId, $environment, $actor, $action, $sourceRevisionId, $result, $changedFieldsJson, $reason, $createdAtUtc);
            """;
        command.Parameters.AddWithValue("$auditId", entry.AuditId);
        command.Parameters.AddWithValue("$environment", entry.Profile.ToString());
        command.Parameters.AddWithValue("$actor", entry.Actor);
        command.Parameters.AddWithValue("$action", entry.Action);
        command.Parameters.AddWithValue("$sourceRevisionId", (object?)entry.SourceRevisionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$result", entry.Result);
        command.Parameters.AddWithValue("$changedFieldsJson", entry.ChangedFields is null ? DBNull.Value : JsonSerializer.Serialize(entry.ChangedFields, JsonOptions));
        command.Parameters.AddWithValue("$reason", (object?)entry.Reason ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", FormatTimestamp(entry.CreatedAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string declaration)
    {
        using var inspect = connection.CreateCommand();
        inspect.CommandText = $"PRAGMA table_info({table});";
        using var reader = inspect.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        reader.Close();
        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {declaration};";
        alter.ExecuteNonQuery();
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA synchronous=FULL; PRAGMA busy_timeout=1000; PRAGMA foreign_keys=ON;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static void BindEvent(
        SqliteCommand command,
        SparkyTraceEvent traceEvent,
        SparkyApiProfile environment,
        string metadataHash)
    {
        command.Parameters.AddWithValue("$environment", environment.ToString());
        command.Parameters.AddWithValue("$eventId", traceEvent.EventId.ToString("D"));
        command.Parameters.AddWithValue("$requestId", traceEvent.RequestId.ToString("D"));
        command.Parameters.AddWithValue("$source", traceEvent.Source);
        command.Parameters.AddWithValue("$stage", traceEvent.Stage);
        command.Parameters.AddWithValue("$outcome", traceEvent.Outcome);
        command.Parameters.AddWithValue("$recordedAtUtc", FormatTimestamp(traceEvent.RecordedAtUtc));
        command.Parameters.AddWithValue("$elapsedMilliseconds", (object?)traceEvent.ElapsedMilliseconds ?? DBNull.Value);
        command.Parameters.AddWithValue("$detailCode", (object?)traceEvent.DetailCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$intent", (object?)traceEvent.Intent ?? DBNull.Value);
        command.Parameters.AddWithValue("$focus", (object?)traceEvent.Focus ?? DBNull.Value);
        command.Parameters.AddWithValue("$searchQuery", (object?)traceEvent.SearchQuery ?? DBNull.Value);
        command.Parameters.AddWithValue("$entityCandidatesJson", (object?)SerializeNullable(traceEvent.EntityCandidates) ?? DBNull.Value);
        command.Parameters.AddWithValue("$selectedSourceIdsJson", (object?)SerializeNullable(traceEvent.SelectedSourceIds) ?? DBNull.Value);
        command.Parameters.AddWithValue("$modelId", (object?)traceEvent.ModelId ?? DBNull.Value);
        command.Parameters.AddWithValue("$configRevision", (object?)traceEvent.ConfigRevision ?? DBNull.Value);
        command.Parameters.AddWithValue("$bundleVersion", (object?)traceEvent.BundleVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("$question", (object?)traceEvent.Question ?? DBNull.Value);
        command.Parameters.AddWithValue("$answer", (object?)traceEvent.Answer ?? DBNull.Value);
        command.Parameters.AddWithValue("$finishStatus", (object?)traceEvent.FinishStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("$metadataHash", metadataHash);
    }

    private static SparkyTraceEvent ReadEvent(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        Guid.Parse(reader.GetString(1)),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        reader.IsDBNull(6) ? null : reader.GetInt64(6),
        ReadNullableString(reader, 7),
        ReadNullableString(reader, 8),
        ReadNullableString(reader, 9),
        ReadNullableString(reader, 10),
        DeserializeNullableList(reader, 11),
        DeserializeNullableList(reader, 12),
        ReadNullableString(reader, 13),
        ReadNullableString(reader, 14),
        ReadNullableString(reader, 15),
        ReadNullableString(reader, 16),
        ReadNullableString(reader, 17),
        ReadNullableString(reader, 18));

    private static string ComputeMetadataHash(SparkyTraceEvent traceEvent)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            traceEvent.RequestId,
            traceEvent.Source,
            traceEvent.Stage,
            traceEvent.Outcome,
            traceEvent.RecordedAtUtc,
            traceEvent.ElapsedMilliseconds,
            traceEvent.DetailCode,
            traceEvent.Intent,
            traceEvent.Focus,
            traceEvent.SearchQuery,
            traceEvent.EntityCandidates,
            traceEvent.SelectedSourceIds,
            traceEvent.ModelId,
            traceEvent.ConfigRevision,
            traceEvent.BundleVersion,
            traceEvent.FinishStatus
        }, JsonOptions);
        try
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static string? SerializeNullable<T>(T? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    private static IReadOnlyList<string>? DeserializeNullableList(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : JsonSerializer.Deserialize<string[]>(reader.GetString(ordinal), JsonOptions);

    private static string? ReadNullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static bool IsValidReplay(SparkyReplayRun? replay)
    {
        if (replay is null || replay.ReplayId == Guid.Empty ||
            replay.Environment is not (SparkyApiProfile.Dev or SparkyApiProfile.Live) ||
            string.IsNullOrWhiteSpace(replay.ActiveRevisionId) || replay.ActiveRevisionId.Length > 80 ||
            string.IsNullOrWhiteSpace(replay.CandidateRevisionId) || replay.CandidateRevisionId.Length > 80 ||
            replay.CreatedAtUtc.Offset != TimeSpan.Zero ||
            replay.Results is not { Count: > 0 and <= SparkyReplayService.MaximumExamples })
        {
            return false;
        }

        return replay.Results.All(result =>
            result is not null && result.ExampleId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(result.Question) &&
            SparkyTraceEventValidator.IsContentTextValid(result.Question, 2000) &&
            result.ExpectedProperties is { Count: <= 16 } &&
            result.ExpectedProperties.All(property => !string.IsNullOrWhiteSpace(property) &&
                SparkyTraceEventValidator.IsContentTextValid(property, 80)) &&
            (result.ExpectedIntent is null || result.ExpectedIntent.Length <= 64) &&
            IsValidReplayOutput(result.Active) && IsValidReplayOutput(result.Candidate));
    }

    private static bool IsValidReplayOutput(SparkyReplayOutput? output) =>
        output is not null && !string.IsNullOrWhiteSpace(output.Status) &&
        SparkyTraceEventValidator.IsContentTextValid(output.Status, 32) &&
        SparkyTraceEventValidator.IsContentTextValid(output.Intent, 64) &&
        SparkyTraceEventValidator.IsContentTextValid(output.Answer, 2000) &&
        IsValidReplayStringList(output.Entities, maxCount: 8, maxCharacters: 64) &&
        IsValidReplayStringList(output.SelectedSourceIds, maxCount: 32, maxCharacters: 128);

    private static bool IsValidReplayStringList(IReadOnlyList<string>? values, int maxCount, int maxCharacters) =>
        values is null || values.Count <= maxCount && values.All(value =>
            !string.IsNullOrWhiteSpace(value) && SparkyTraceEventValidator.IsContentTextValid(value, maxCharacters));
}
