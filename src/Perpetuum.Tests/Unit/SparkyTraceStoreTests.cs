using Perpetuum.ItemStatsExporter;
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
