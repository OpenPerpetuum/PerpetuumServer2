using System.Text;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyTraceEventValidationTests
{
    [Fact]
    public void Valid_bounded_game_and_discord_events_are_accepted()
    {
        var events = new[]
        {
            Event("10000000-0000-0000-0000-000000000001", "Game", "queue", "queued"),
            Event("10000000-0000-0000-0000-000000000002", "Discord", "delivery", "sent")
        };

        Assert.True(SparkyTraceEventValidator.TryValidateBatch(events, out _));
    }

    [Theory]
    [InlineData("Webhook", "queue", "queued")]
    [InlineData("Game", "execute_sql", "queued")]
    [InlineData("Game", "queue", "arbitrary user text")]
    public void Unknown_source_stage_or_outcome_is_rejected(string source, string stage, string outcome)
    {
        var traceEvent = Event("10000000-0000-0000-0000-000000000003", source, stage, outcome);

        Assert.False(SparkyTraceEventValidator.TryValidateBatch([traceEvent], out _));
    }

    [Fact]
    public void Oversized_batches_and_detail_fields_are_rejected()
    {
        var repeated = Enumerable.Range(0, 101)
            .Select(index => Event($"10000000-0000-0000-0000-{index + 1:000000000000}", "Game", "queue", "queued"))
            .ToArray();
        var oversizedDetail = Event("10000000-0000-0000-0000-000000000004", "Game", "queue", "queued") with
        {
            DetailCode = new string('a', SparkyTraceEventValidator.MaxDetailCodeCharacters + 1)
        };

        Assert.False(SparkyTraceEventValidator.TryValidateBatch(repeated, out _));
        Assert.False(SparkyTraceEventValidator.TryValidateBatch([oversizedDetail], out _));
    }

    [Fact]
    public void Empty_ids_negative_durations_and_caller_identity_fields_are_rejected()
    {
        var emptyId = Event("10000000-0000-0000-0000-000000000005", "Game", "queue", "queued") with { EventId = Guid.Empty };
        var negativeElapsed = Event("10000000-0000-0000-0000-000000000006", "Game", "queue", "queued") with { ElapsedMilliseconds = -1 };

        Assert.False(SparkyTraceEventValidator.TryValidateBatch([emptyId], out _));
        Assert.False(SparkyTraceEventValidator.TryValidateBatch([negativeElapsed], out _));
        Assert.False(SparkyTraceEventValidator.TryDeserializeBatch(
            Encoding.UTF8.GetBytes("[{\"eventId\":\"10000000-0000-0000-0000-000000000007\",\"requestId\":\"20000000-0000-0000-0000-000000000001\",\"source\":\"Game\",\"stage\":\"queue\",\"outcome\":\"queued\",\"recordedAtUtc\":\"2026-10-14T12:00:00Z\",\"playerId\":\"not-allowed\"}]"), out _));
    }

    [Fact]
    public void Unknown_json_fields_and_oversized_json_are_rejected()
    {
        var unknownField = Encoding.UTF8.GetBytes("[{\"eventId\":\"10000000-0000-0000-0000-000000000008\",\"requestId\":\"20000000-0000-0000-0000-000000000001\",\"source\":\"Game\",\"stage\":\"queue\",\"outcome\":\"queued\",\"recordedAtUtc\":\"2026-10-14T12:00:00Z\",\"environment\":\"Live\"}]");
        var oversized = new byte[SparkyTraceEventValidator.MaxBodyBytes + 1];

        Assert.False(SparkyTraceEventValidator.TryDeserializeBatch(unknownField, out _));
        Assert.False(SparkyTraceEventValidator.TryDeserializeBatch(oversized, out _));
    }

    private static SparkyTraceEventRequest Event(string eventId, string source, string stage, string outcome) => new(
        Guid.Parse(eventId),
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        source,
        stage,
        outcome,
        new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero),
        ElapsedMilliseconds: 10,
        DetailCode: "ok");
}
