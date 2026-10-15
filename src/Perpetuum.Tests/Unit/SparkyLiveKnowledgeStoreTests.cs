using Perpetuum.ItemStatsExporter;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyLiveKnowledgeStoreTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Current_snapshot_isolated_by_profile_and_survives_store_reopen()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        string databasePath = Path.Combine(temp.Path, "live-knowledge.db");
        var devUpdate = Current("dev", CapturedAt, "Autumn Circuit");

        var store = new SparkyLiveKnowledgeStore(databasePath);
        await store.InitializeAsync(cancellationToken);
        Assert.Equal("applied", (await store.ReplaceCurrentAsync(SparkyApiProfile.Dev, devUpdate, cancellationToken)).Status);
        Assert.Null((await store.LoadProfileAsync(SparkyApiProfile.Live, cancellationToken)).Current);

        var reopened = new SparkyLiveKnowledgeStore(databasePath);
        await reopened.InitializeAsync(cancellationToken);
        var loaded = await reopened.LoadProfileAsync(SparkyApiProfile.Dev, cancellationToken);

        Assert.Equal(devUpdate.ContentHash, loaded.Current!.ContentHash);
        Assert.Equal("Autumn Circuit", loaded.Current.Current!.ActiveSeason!.Name);
    }

    [Fact]
    public async Task Matching_heartbeat_advances_capture_time_without_changing_payload_or_hash()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = NewStore(temp.Path);
        var update = Current("dev", CapturedAt, "Autumn Circuit");
        await store.ReplaceCurrentAsync(SparkyApiProfile.Dev, update, cancellationToken);
        var heartbeat = new SparkyLiveKnowledgeUpdate(1, "seasons", "dev", "refresh-current", CapturedAt.AddMinutes(5), update.ContentHash, null, null);

        var result = await store.RefreshCurrentAsync(SparkyApiProfile.Dev, heartbeat, cancellationToken);
        var loaded = await store.LoadProfileAsync(SparkyApiProfile.Dev, cancellationToken);

        Assert.Equal("refreshed", result.Status);
        Assert.Equal(update.ContentHash, loaded.Current!.ContentHash);
        Assert.Equal(CapturedAt.AddMinutes(5), loaded.Current.CapturedAtUtc);
        Assert.Equal("Autumn Circuit", loaded.Current.Current!.ActiveSeason!.Name);
    }

    [Fact]
    public async Task Missing_or_mismatched_heartbeat_does_not_change_current_freshness()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = NewStore(temp.Path);
        var current = Current("dev", CapturedAt, "Autumn Circuit");
        await store.ReplaceCurrentAsync(SparkyApiProfile.Dev, current, cancellationToken);

        var mismatch = new SparkyLiveKnowledgeUpdate(1, "seasons", "dev", "refresh-current", CapturedAt.AddMinutes(5), new string('b', 64), null, null);
        var mismatchResult = await store.RefreshCurrentAsync(SparkyApiProfile.Dev, mismatch, cancellationToken);
        var afterMismatch = await store.LoadProfileAsync(SparkyApiProfile.Dev, cancellationToken);

        Assert.Equal("hash_mismatch", mismatchResult.Status);
        Assert.Equal(CapturedAt, afterMismatch.Current!.CapturedAtUtc);

        var missing = mismatch with { Profile = "live", ContentHash = current.ContentHash };
        var missingResult = await store.RefreshCurrentAsync(SparkyApiProfile.Live, missing, cancellationToken);

        Assert.Equal("current_missing", missingResult.Status);
    }

    [Fact]
    public async Task Older_current_snapshot_cannot_roll_back_newer_snapshot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = NewStore(temp.Path);
        var newer = Current("dev", CapturedAt.AddMinutes(10), "Autumn Circuit");
        var older = Current("dev", CapturedAt, "Winter Circuit");
        await store.ReplaceCurrentAsync(SparkyApiProfile.Dev, newer, cancellationToken);

        var result = await store.ReplaceCurrentAsync(SparkyApiProfile.Dev, older, cancellationToken);
        var loaded = await store.LoadProfileAsync(SparkyApiProfile.Dev, cancellationToken);

        Assert.Equal("out_of_order", result.Status);
        Assert.Equal(newer.ContentHash, loaded.Current!.ContentHash);
        Assert.Equal("Autumn Circuit", loaded.Current.Current!.ActiveSeason!.Name);
    }

    [Fact]
    public async Task Finalization_is_idempotent_and_newer_correction_keeps_revision_history()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        string databasePath = Path.Combine(temp.Path, "live-knowledge.db");
        var store = new SparkyLiveKnowledgeStore(databasePath);
        await store.InitializeAsync(cancellationToken);
        var first = Finalization("dev", CapturedAt, "Agent One", 100);
        var duplicate = Finalization("dev", CapturedAt.AddMinutes(1), "Agent One", 100);
        var correction = Finalization("dev", CapturedAt.AddMinutes(2), "Agent Two", 120);

        var result1 = await store.FinalizeSeasonAsync(SparkyApiProfile.Dev, 7, first, cancellationToken);
        var result2 = await store.FinalizeSeasonAsync(SparkyApiProfile.Dev, 7, duplicate, cancellationToken);
        var result3 = await store.FinalizeSeasonAsync(SparkyApiProfile.Dev, 7, correction, cancellationToken);

        Assert.Equal(1, result1.Revision);
        Assert.Equal(1, result2.Revision);
        Assert.Equal(2, result3.Revision);

        var reopened = new SparkyLiveKnowledgeStore(databasePath);
        await reopened.InitializeAsync(cancellationToken);
        var history = (await reopened.LoadProfileAsync(SparkyApiProfile.Dev, cancellationToken)).FinalizedSeasons;

        Assert.Single(history);
        Assert.Equal(2, history[0].Revision);
        Assert.Equal("Agent Two", history[0].Finalization.TopTen[0].DisplayName);
    }

    [Fact]
    public async Task Finalization_route_id_must_match_payload_season_id()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = NewStore(temp.Path);
        var finalization = Finalization("dev", CapturedAt, "Agent One", 100);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.FinalizeSeasonAsync(SparkyApiProfile.Dev, 8, finalization, cancellationToken));
    }

    private static SparkyLiveKnowledgeStore NewStore(string directory)
    {
        var store = new SparkyLiveKnowledgeStore(Path.Combine(directory, "live-knowledge.db"));
        store.InitializeAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        return store;
    }

    private static SparkyLiveKnowledgeUpdate Current(string profile, DateTimeOffset capturedAt, string seasonName)
    {
        var update = new SparkyLiveKnowledgeUpdate(1, "seasons", profile, "replace-current", capturedAt, new string('a', 64),
            new SparkySeasonCurrentState(
                new SparkySeasonFacts(7, seasonName, "Public season", CapturedAt.AddDays(-1), CapturedAt.AddDays(10),
                    "ActivityAndGlobal", [new SparkySeasonActivityRate("NpcKill", 1.5, 2)], [], []),
                [new SparkySeasonStanding(1, "Agent One", 250)]), null);
        return update with { ContentHash = SparkyLiveKnowledgeValidator.ComputeCurrentHash(update) };
    }

    private static SparkyLiveKnowledgeUpdate Finalization(string profile, DateTimeOffset capturedAt, string champion, double points)
    {
        var update = new SparkyLiveKnowledgeUpdate(1, "seasons", profile, "finalize-season", capturedAt, new string('a', 64), null,
            new SparkySeasonFinalization(7, "Autumn Circuit", CapturedAt.AddDays(-1), [new SparkySeasonStanding(1, champion, points)]));
        return update with { ContentHash = SparkyLiveKnowledgeValidator.ComputeFinalizationHash(update) };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sparky-live-store-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
