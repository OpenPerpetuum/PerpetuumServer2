using System.Text;
using Perpetuum.Services.Sparky;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyTraceSpoolTests
{
    [Fact]
    public async Task Appended_events_survive_reopen_and_identical_event_ids_are_idempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "spool.jsonl");
        var traceEvent = Event(Guid.NewGuid(), Guid.NewGuid());

        await using (var spool = new SparkyTraceSpool(path, 16 * 1024))
        {
            await spool.InitializeAsync(cancellationToken);
            Assert.Equal(SparkyTraceAppendDisposition.Added,
                await spool.AppendAsync(traceEvent, cancellationToken));
            Assert.Equal(SparkyTraceAppendDisposition.Duplicate,
                await spool.AppendAsync(traceEvent, cancellationToken));
        }

        await using var reopened = new SparkyTraceSpool(path, 16 * 1024);
        await reopened.InitializeAsync(cancellationToken);
        var pending = await reopened.ReadPendingAsync(10, cancellationToken);

        Assert.Equal([traceEvent], pending);
    }

    [Fact]
    public async Task Reopen_truncates_only_an_incomplete_final_line()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "spool.jsonl");
        var traceEvent = Event(Guid.NewGuid(), Guid.NewGuid());

        long completeLength;
        await using (var spool = new SparkyTraceSpool(path, 16 * 1024))
        {
            await spool.InitializeAsync(cancellationToken);
            await spool.AppendAsync(traceEvent, cancellationToken);
            completeLength = new FileInfo(path).Length;
        }

        await File.AppendAllTextAsync(path, "{\"EventId\":", Encoding.UTF8, cancellationToken);

        await using var reopened = new SparkyTraceSpool(path, 16 * 1024);
        await reopened.InitializeAsync(cancellationToken);

        Assert.Equal(completeLength, new FileInfo(path).Length);
        Assert.Equal([traceEvent], await reopened.ReadPendingAsync(10, cancellationToken));
    }

    [Fact]
    public async Task Invalid_complete_line_is_preserved_and_disables_spool_recovery()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "spool.jsonl");
        Directory.CreateDirectory(temp.Path);
        await File.WriteAllTextAsync(path, "{\"notAnEvent\":true}\n", Encoding.UTF8, cancellationToken);
        byte[] original = await File.ReadAllBytesAsync(path, cancellationToken);

        await using var spool = new SparkyTraceSpool(path, 16 * 1024);

        await Assert.ThrowsAsync<InvalidDataException>(() => spool.InitializeAsync(cancellationToken));
        Assert.Equal(original, await File.ReadAllBytesAsync(path, cancellationToken));
    }

    [Fact]
    public async Task Conflicting_duplicate_id_is_rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        await using var spool = new SparkyTraceSpool(Path.Combine(temp.Path, "spool.jsonl"), 16 * 1024);
        await spool.InitializeAsync(cancellationToken);
        var original = Event(Guid.NewGuid(), Guid.NewGuid());

        await spool.AppendAsync(original, cancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            spool.AppendAsync(original with { Outcome = "failed" }, cancellationToken));
    }

    [Fact]
    public async Task Full_spool_keeps_existing_events_and_writes_gap_marker_after_acknowledgement()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        await using var spool = new SparkyTraceSpool(Path.Combine(temp.Path, "spool.jsonl"), 1024, 512);
        await spool.InitializeAsync(cancellationToken);
        var accepted = new List<SparkyTraceEvent>();
        bool capacityReached = false;

        for (int index = 0; index < 50; index++)
        {
            var traceEvent = Event(Guid.NewGuid(), Guid.NewGuid());
            var result = await spool.AppendAsync(traceEvent, cancellationToken);
            if (result == SparkyTraceAppendDisposition.CapacityExceeded)
            {
                capacityReached = true;
                break;
            }

            accepted.Add(traceEvent);
        }

        Assert.True(capacityReached);
        Assert.NotEmpty(accepted);
        var beforeAcknowledgement = await spool.ReadPendingAsync(100, cancellationToken);
        Assert.Contains(beforeAcknowledgement, traceEvent => traceEvent.Stage == "trace_gap");
        Assert.All(accepted, traceEvent => Assert.Contains(beforeAcknowledgement, pending => pending.EventId == traceEvent.EventId));

        await spool.AcknowledgeAsync(accepted.Select(traceEvent => traceEvent.EventId).ToArray(), cancellationToken);
        var afterAcknowledgement = await spool.ReadPendingAsync(100, cancellationToken);

        var gap = Assert.Single(afterAcknowledgement);
        Assert.Equal("trace_gap", gap.Stage);
        Assert.Equal("trace_gap", gap.Outcome);
        Assert.Equal("spool_overflow", gap.DetailCode);
        Assert.NotEqual(Guid.Empty, gap.EventId);
        Assert.NotEqual(Guid.Empty, gap.RequestId);
        Assert.True(new FileInfo(spool.Path).Length <= 1024);
    }

    private static SparkyTraceEvent Event(Guid eventId, Guid requestId) => new(
        eventId,
        requestId,
        "Game",
        "trigger",
        "recognized",
        DateTimeOffset.UtcNow,
        DetailCode: "mention");

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "sparky-game-trace-spool-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
