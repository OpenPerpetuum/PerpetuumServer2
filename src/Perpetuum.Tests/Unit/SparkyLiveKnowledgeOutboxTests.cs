using Perpetuum.Services.Sparky;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyLiveKnowledgeOutboxTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Pending_finalizations_survive_reload_and_acknowledgement_rewrites_atomically()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "sparky-live-knowledge-outbox.jsonl");
        var first = Finalization(7, "Agent One", 900);
        var second = Finalization(8, "Agent Two", 700);
        var outbox = new SparkyLiveKnowledgeOutbox(path);
        await outbox.InitializeAsync(cancellationToken);

        Assert.Equal(SparkyLiveKnowledgeOutboxAppendDisposition.Added,
            await outbox.AppendAsync(first, cancellationToken));
        Assert.Equal(SparkyLiveKnowledgeOutboxAppendDisposition.Added,
            await outbox.AppendAsync(second, cancellationToken));
        Assert.Equal(SparkyLiveKnowledgeOutboxAppendDisposition.Duplicate,
            await outbox.AppendAsync(first, cancellationToken));

        var reloaded = new SparkyLiveKnowledgeOutbox(path);
        await reloaded.InitializeAsync(cancellationToken);
        Assert.Equal([first.ContentHash, second.ContentHash],
            (await reloaded.ReadPendingAsync(cancellationToken)).Select(update => update.ContentHash));

        Assert.True(await reloaded.AcknowledgeAsync(first, cancellationToken));
        Assert.Equal([second.ContentHash],
            (await reloaded.ReadPendingAsync(cancellationToken)).Select(update => update.ContentHash));
        Assert.False(Directory.EnumerateFiles(temp.Path, "*.tmp-*").Any());

        var afterRestart = new SparkyLiveKnowledgeOutbox(path);
        await afterRestart.InitializeAsync(cancellationToken);
        Assert.Equal([second.ContentHash],
            (await afterRestart.ReadPendingAsync(cancellationToken)).Select(update => update.ContentHash));
    }

    [Fact]
    public async Task Full_outbox_rejects_new_finalization_and_preserves_existing_records()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var outbox = new SparkyLiveKnowledgeOutbox(Path.Combine(temp.Path, "full.jsonl"), maxRecords: 2);
        await outbox.InitializeAsync(cancellationToken);
        var first = Finalization(7, "Agent One", 900);
        var second = Finalization(8, "Agent Two", 700);

        Assert.Equal(SparkyLiveKnowledgeOutboxAppendDisposition.Added, await outbox.AppendAsync(first, cancellationToken));
        Assert.Equal(SparkyLiveKnowledgeOutboxAppendDisposition.Added, await outbox.AppendAsync(second, cancellationToken));
        Assert.Equal(SparkyLiveKnowledgeOutboxAppendDisposition.CapacityExceeded,
            await outbox.AppendAsync(Finalization(9, "Agent Three", 500), cancellationToken));

        var pending = await outbox.ReadPendingAsync(cancellationToken);
        Assert.Equal([first.ContentHash, second.ContentHash], pending.Select(update => update.ContentHash));
    }

    [Fact]
    public async Task Duplicate_finalization_key_is_season_profile_and_hash_scoped()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var outbox = new SparkyLiveKnowledgeOutbox(Path.Combine(temp.Path, "dedupe.jsonl"));
        await outbox.InitializeAsync(cancellationToken);
        var initial = Finalization(7, "Agent One", 900);
        var correction = Finalization(7, "Agent Two", 950);

        Assert.Equal(SparkyLiveKnowledgeOutboxAppendDisposition.Added, await outbox.AppendAsync(initial, cancellationToken));
        Assert.Equal(SparkyLiveKnowledgeOutboxAppendDisposition.Added, await outbox.AppendAsync(correction, cancellationToken));
        Assert.Equal(2, (await outbox.ReadPendingAsync(cancellationToken)).Count);
    }

    private static SparkyLiveKnowledgeUpdate Finalization(int seasonId, string champion, double points)
    {
        var update = new SparkyLiveKnowledgeUpdate(1, "seasons", "dev", "finalize-season", CapturedAt,
            new string('a', 64), null,
            new SparkySeasonFinalization(seasonId, $"Season {seasonId}", CapturedAt.AddDays(-1),
                [new SparkySeasonStanding(1, champion, points)]));
        return update with { ContentHash = SparkyLiveKnowledgeHash.ComputeFinalization(update) };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sparky-live-outbox-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
