using Perpetuum.ItemStatsExporter;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyTraceStoreTests
{
    [Fact]
    public async Task Repeated_event_upload_is_acknowledged_once_in_the_timeline()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var traceEvent = Event("10000000-0000-0000-0000-000000000001", "20000000-0000-0000-0000-000000000001", "request", "accepted") with
        {
            FinishStatus = "stop"
        };

        var first = await store.AppendBatchAsync([traceEvent], SparkyApiProfile.Dev, captureContent: true, cancellationToken);
        var retry = await store.AppendBatchAsync([traceEvent], SparkyApiProfile.Dev, captureContent: true, cancellationToken);
        var timeline = await store.GetTraceAsync(traceEvent.RequestId, SparkyApiProfile.Dev, cancellationToken);

        Assert.Equal([traceEvent.EventId], first.AcknowledgedEventIds);
        Assert.Equal([traceEvent.EventId], retry.AcknowledgedEventIds);
        Assert.Empty(retry.ConflictingEventIds);
        Assert.NotNull(timeline);
        Assert.Single(timeline.Events);
        Assert.Equal("stop", timeline.Events[0].FinishStatus);
    }

    [Fact]
    public async Task Timeline_is_ordered_by_recorded_time_and_isolated_by_profile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var requestId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var later = Event("10000000-0000-0000-0000-000000000003", requestId.ToString(), "answer", "answered", 12);
        var earlier = Event("10000000-0000-0000-0000-000000000002", requestId.ToString(), "request", "accepted", 10);

        await store.AppendBatchAsync([later], SparkyApiProfile.Dev, captureContent: false, cancellationToken);
        await store.AppendBatchAsync([earlier], SparkyApiProfile.Dev, captureContent: false, cancellationToken);
        await store.AppendBatchAsync([earlier], SparkyApiProfile.Live, captureContent: false, cancellationToken);

        var devTimeline = await store.GetTraceAsync(requestId, SparkyApiProfile.Dev, cancellationToken);
        var liveTimeline = await store.GetTraceAsync(requestId, SparkyApiProfile.Live, cancellationToken);

        Assert.NotNull(devTimeline);
        Assert.Equal(new[] { earlier.EventId, later.EventId }, devTimeline.Events.Select(item => item.EventId));
        Assert.NotNull(liveTimeline);
        Assert.Single(liveTimeline.Events);
        Assert.Equal(SparkyApiProfile.Live, liveTimeline.Environment);
    }

    [Fact]
    public async Task Content_capture_policy_and_expiration_are_applied_without_deleting_live_metadata_early()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var requestId = Guid.Parse("20000000-0000-0000-0000-000000000003");
        var contentEvent = Event("10000000-0000-0000-0000-000000000004", requestId.ToString(), "request", "accepted", 1) with
        {
            Question = "private question text",
            Answer = "private answer text",
            SearchQuery = "derived private search text",
            EntityCandidates = ["private entity name"]
        };
        await store.AppendBatchAsync([contentEvent], SparkyApiProfile.Dev, captureContent: false, cancellationToken);

        var noCapture = await store.GetTraceAsync(requestId, SparkyApiProfile.Dev, cancellationToken);
        Assert.Null(noCapture!.Events[0].Question);
        Assert.Null(noCapture.Events[0].Answer);
        Assert.Null(noCapture.Events[0].SearchQuery);
        Assert.Null(noCapture.Events[0].EntityCandidates);

        var capturedEvent = contentEvent with
        {
            EventId = Guid.Parse("10000000-0000-0000-0000-000000000005"),
            RecordedAtUtc = DateTimeOffset.UtcNow.AddDays(-31),
            Answer = "private answer text\nwith a second line"
        };
        await store.AppendBatchAsync([capturedEvent], SparkyApiProfile.Dev, captureContent: true, cancellationToken);
        var capturedTimeline = await store.GetTraceAsync(requestId, SparkyApiProfile.Dev, cancellationToken);
        Assert.Equal(
            "private answer text\nwith a second line",
            capturedTimeline!.Events.Single(item => item.EventId == capturedEvent.EventId).Answer);

        var removed = await store.ApplyRetentionAsync(
            contentRetention: TimeSpan.FromDays(30),
            metadataRetention: TimeSpan.FromDays(90),
            cancellationToken: cancellationToken);
        var afterRetention = await store.GetTraceAsync(requestId, SparkyApiProfile.Dev, cancellationToken);

        Assert.Equal(1, removed.ContentRowsCleared);
        Assert.Equal(0, removed.MetadataRowsDeleted);
        Assert.NotNull(afterRetention);
        Assert.Equal(2, afterRetention.Events.Count);
        Assert.All(afterRetention.Events, item =>
        {
            Assert.Null(item.Question);
            Assert.Null(item.Answer);
        });
    }

    [Fact]
    public async Task Reusing_an_event_id_for_different_data_is_reported_as_a_conflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var traceEvent = Event("10000000-0000-0000-0000-000000000006", "20000000-0000-0000-0000-000000000004", "request", "accepted");
        var conflicting = traceEvent with { Outcome = "rejected" };

        await store.AppendBatchAsync([traceEvent], SparkyApiProfile.Dev, captureContent: false, cancellationToken);
        var result = await store.AppendBatchAsync([conflicting], SparkyApiProfile.Dev, captureContent: false, cancellationToken);

        Assert.Empty(result.AcknowledgedEventIds);
        Assert.Equal([traceEvent.EventId], result.ConflictingEventIds);
        Assert.Single((await store.GetTraceAsync(traceEvent.RequestId, SparkyApiProfile.Dev, cancellationToken))!.Events);
    }

    [Fact]
    public async Task Existing_trace_database_reads_legacy_evaluation_question_as_one_turn()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "legacy-evaluation.db");
        Directory.CreateDirectory(temp.Path);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE EvaluationExample (
                    Environment TEXT NOT NULL,
                    ExampleId TEXT NOT NULL,
                    RequestId TEXT NOT NULL,
                    Question TEXT NULL,
                    ExpectedIntent TEXT NULL,
                    ExpectedPropertiesJson TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    PRIMARY KEY (Environment, ExampleId));
                INSERT INTO EvaluationExample (Environment, ExampleId, RequestId, Question, ExpectedIntent, ExpectedPropertiesJson, CreatedAtUtc)
                VALUES ('Dev', '30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', 'Who is Theseus?', 'MechanicsLore', '["entity:Theseus"]', '2026-10-14T12:00:00.0000000+00:00');
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var store = new SparkyTraceStore(path);
        var example = Assert.Single(await store.GetEvaluationExamplesAsync(SparkyApiProfile.Dev, cancellationToken: cancellationToken));

        Assert.Equal("Who is Theseus?", example.Question);
        var turn = Assert.Single(example.Turns!);
        Assert.Equal("Who is Theseus?", turn.Question);
        Assert.Equal("MechanicsLore", turn.ExpectedIntent);
        Assert.Equal(["entity:Theseus"], turn.ExpectedProperties);
        await using var verifyConnection = new SqliteConnection(connectionString);
        await verifyConnection.OpenAsync(cancellationToken);
        await using var verifyCommand = verifyConnection.CreateCommand();
        verifyCommand.CommandText = "SELECT TurnsJson FROM EvaluationExample WHERE ExampleId = '30000000-0000-0000-0000-000000000001';";
        Assert.Equal(DBNull.Value, await verifyCommand.ExecuteScalarAsync(cancellationToken));
    }

    [Fact]
    public async Task Trace_persists_non_sensitive_effective_profile_tuning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "tuning-trace.db"));
        var traceEvent = Event("10000000-0000-0000-0000-000000000020", "20000000-0000-0000-0000-000000000020", "answer", "answered") with
        {
            AnswerTemperature = 0.55,
            AnswerTopP = 0.8,
            ConversationHistoryTurns = 6,
            ConversationHistoryCharacters = 3000
        };

        await store.AppendAsync(traceEvent, SparkyApiProfile.Dev, captureContent: false, cancellationToken);
        var result = Assert.Single((await store.GetTraceAsync(traceEvent.RequestId, SparkyApiProfile.Dev, cancellationToken))!.Events);

        Assert.Equal(0.55, result.AnswerTemperature);
        Assert.Equal(0.8, result.AnswerTopP);
        Assert.Equal(6, result.ConversationHistoryTurns);
        Assert.Equal(3000, result.ConversationHistoryCharacters);
        Assert.Null(result.Question);
    }

    [Fact]
    public async Task Context_and_comparison_content_are_gated_while_diagnostic_metadata_is_retained()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "comparison-trace.db"));
        Guid requestId = Guid.Parse("20000000-0000-0000-0000-000000000021");
        var content = Event("10000000-0000-0000-0000-000000000021", requestId.ToString(), "answer", "answered") with
        {
            ContextMessageCount = 2,
            ContextCharacterCount = 154,
            HasContextSummary = true,
            HasPendingClarification = false,
            ContextBudgetTrimmed = true,
            Intent = "StructuredRobot",
            EffectiveIntent = "StructuredRobot",
            EffectiveFocus = "CompareStats",
            ResolverCategory = "comparison_facts",
            ResolverOutcome = "resolved",
            ComparisonSubjectCount = 2,
            ComparisonAttributeCategory = "speed",
            ComparisonResultCategory = "comparison_resolved",
            ContextTurns = [new("user", "What are stats for Kain?"), new("assistant", "Kain tops out at 103.32 kph.")],
            ContextSummary = "The player is comparing robot speed.",
            ComparisonDiagnostics = new SparkyComparisonDiagnostics(
                "Robot",
                [new("Kain", "conversation_context"), new("Tyrannos", "conversation_context")],
                "maximum speed",
                "comparison_resolved",
                [new("Maximum speed", ["103.32 kph", "93.6 kph"], "kph", "9.72", "Kain")])
        };

        await store.AppendAsync(content, SparkyApiProfile.Dev, captureContent: false, cancellationToken);
        var metadataOnly = Assert.Single((await store.GetTraceAsync(requestId, SparkyApiProfile.Dev, cancellationToken))!.Events);
        Assert.Equal(2, metadataOnly.ContextMessageCount);
        Assert.Equal(154, metadataOnly.ContextCharacterCount);
        Assert.True(metadataOnly.HasContextSummary);
        Assert.True(metadataOnly.ContextBudgetTrimmed);
        Assert.Equal("StructuredRobot", metadataOnly.Intent);
        Assert.Equal("CompareStats", metadataOnly.EffectiveFocus);
        Assert.Equal("resolved", metadataOnly.ResolverOutcome);
        Assert.Null(metadataOnly.ContextTurns);
        Assert.Null(metadataOnly.ContextSummary);
        Assert.Null(metadataOnly.ComparisonDiagnostics);

        var captured = content with { EventId = Guid.Parse("10000000-0000-0000-0000-000000000022") };
        await store.AppendAsync(captured, SparkyApiProfile.Dev, captureContent: true, cancellationToken);
        var timeline = await store.GetTraceAsync(requestId, SparkyApiProfile.Dev, cancellationToken);
        var capturedEvent = timeline!.Events.Single(item => item.EventId == captured.EventId);
        Assert.Equal("What are stats for Kain?", capturedEvent.ContextTurns![0].Content);
        Assert.Equal("maximum speed", capturedEvent.ComparisonDiagnostics!.MetricPhrase);
        Assert.Equal("9.72", Assert.Single(capturedEvent.ComparisonDiagnostics.Facts).Difference);
    }

    [Fact]
    public async Task Existing_trace_table_is_additively_migrated_and_legacy_rows_remain_readable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "legacy-trace.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE Trace (
                    Environment TEXT NOT NULL, EventId TEXT NOT NULL, RequestId TEXT NOT NULL,
                    Source TEXT NOT NULL, Stage TEXT NOT NULL, Outcome TEXT NOT NULL, RecordedAtUtc TEXT NOT NULL,
                    ElapsedMilliseconds INTEGER NULL, DetailCode TEXT NULL, Intent TEXT NULL, Focus TEXT NULL,
                    SearchQuery TEXT NULL, EntityCandidatesJson TEXT NULL, SelectedSourceIdsJson TEXT NULL,
                    ModelId TEXT NULL, ConfigRevision TEXT NULL, BundleVersion TEXT NULL, Question TEXT NULL,
                    Answer TEXT NULL, FinishStatus TEXT NULL, MetadataHash TEXT NOT NULL,
                    PRIMARY KEY (Environment, EventId));
                INSERT INTO Trace (Environment, EventId, RequestId, Source, Stage, Outcome, RecordedAtUtc, MetadataHash)
                VALUES ('Dev', '10000000-0000-0000-0000-000000000023', '20000000-0000-0000-0000-000000000023',
                        'Game', 'answer', 'answered', '2026-10-15T12:00:00.0000000+00:00', 'legacy');
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var store = new SparkyTraceStore(path);
        var trace = Assert.Single((await store.GetTraceAsync(
            Guid.Parse("20000000-0000-0000-0000-000000000023"), SparkyApiProfile.Dev, cancellationToken))!.Events);
        Assert.Equal("answered", trace.Outcome);
        Assert.Null(trace.ContextMessageCount);
        Assert.Null(trace.ComparisonDiagnostics);
    }

    [Fact]
    public async Task Query_joins_stage_filters_across_the_timeline_and_feedback_is_idempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var requestId = Guid.Parse("20000000-0000-0000-0000-000000000005");
        var sourceEvent = Event("10000000-0000-0000-0000-000000000007", requestId.ToString(), "trigger", "recognized", 1) with
        {
            Source = "Discord"
        };
        var answerEvent = Event("10000000-0000-0000-0000-000000000008", requestId.ToString(), "answer", "answered", 2) with
        {
            Source = "AI",
            Question = "What is Nia?",
            Answer = "Nia is described in the local records."
        };
        await store.AppendBatchAsync([sourceEvent, answerEvent], SparkyApiProfile.Dev, captureContent: true, cancellationToken);

        var results = await store.QueryTracesAsync(
            new SparkyTraceQuery(SparkyApiProfile.Dev, Source: "Discord", Outcome: "answered", Limit: 500),
            cancellationToken);
        var feedback = new SparkyTraceFeedback(
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            requestId,
            "retrieval_miss",
            "Check entity retrieval",
            DateTimeOffset.UtcNow);

        bool firstFeedback = await store.AddFeedbackAsync(feedback, SparkyApiProfile.Dev, cancellationToken);
        bool duplicateFeedback = await store.AddFeedbackAsync(feedback, SparkyApiProfile.Dev, cancellationToken);
        bool otherProfileFeedback = await store.AddFeedbackAsync(feedback, SparkyApiProfile.Live, cancellationToken);
        var labeledResults = await store.QueryTracesAsync(
            new SparkyTraceQuery(SparkyApiProfile.Dev, Label: "retrieval_miss"),
            cancellationToken);

        var summary = Assert.Single(results);
        var labeledSummary = Assert.Single(labeledResults);
        Assert.Equal(requestId, summary.RequestId);
        Assert.Equal("Discord", summary.Source);
        Assert.Equal("answered", summary.Outcome);
        Assert.Equal("What is Nia?", summary.Question);
        Assert.Equal("Nia is described in the local records.", summary.Answer);
        Assert.Equal(["retrieval_miss"], labeledSummary.Labels);
        Assert.True(firstFeedback);
        Assert.False(duplicateFeedback);
        Assert.False(otherProfileFeedback);
    }

    private static SparkyTraceEvent Event(
        string eventId,
        string requestId,
        string stage,
        string outcome,
        int? second = null) => new(
        Guid.Parse(eventId),
        Guid.Parse(requestId),
        "Game",
        stage,
        outcome,
        new DateTimeOffset(2026, 10, 14, 12, 0, second ?? 0, TimeSpan.Zero),
        ElapsedMilliseconds: 15,
        DetailCode: "ok");

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "sparky-trace-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
