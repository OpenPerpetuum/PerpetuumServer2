using Perpetuum.ItemStatsExporter;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyLiveKnowledgeRoutingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Current_season_question_selects_fresh_current_source()
    {
        var view = CreateView(new SparkyLiveKnowledgeProfileState(
            Current("Autumn Circuit", [new SparkySeasonStanding(1, "Agent One", 250)]), []), "fresh");

        var result = Search("Who leads the Autumn Circuit this season?", view);

        Assert.Contains(result, passage => passage.SourceId == "live-season-current");
        Assert.Contains(result, passage => passage.Text.Contains("Agent One", StringComparison.Ordinal));
    }

    [Fact]
    public void Exact_current_leader_wording_retrieves_the_rank_one_standing()
    {
        var view = SparkyLiveKnowledgeFixtures.CreateReadView("season-sample-v1", Now);

        var result = SearchAtDefaultThreshold("Who is the leader of the current season in the game?", view);

        Assert.Contains(result, passage => passage.SourceId == "live-season-current" &&
            passage.Text.Contains("Fixture Leader", StringComparison.Ordinal));
    }

    [Fact]
    public void Past_season_leader_wording_retrieves_the_latest_final_standing()
    {
        var view = SparkyLiveKnowledgeFixtures.CreateReadView("season-sample-v1", Now);

        var result = SearchAtDefaultThreshold("Who was the leader of the past season in the game?", view);

        Assert.Equal("live-season-final-1002", result[0].SourceId);
        Assert.Contains(result, passage => passage.SourceId == "live-season-final-1002" &&
            passage.Text.Contains("Previous Fixture Champion", StringComparison.Ordinal));
    }

    [Fact]
    public void Current_season_leader_question_resolves_to_the_fresh_rank_one_fact()
    {
        var view = SparkyLiveKnowledgeFixtures.CreateReadView("season-sample-v1", Now);

        bool resolved = SparkySeasonStandingResolver.TryResolve(
            "Who is the leader of the current season in the game?",
            "who is the leader of the current season in the game",
            view,
            out var facts);

        Assert.True(resolved);
        Assert.Equal(SparkySeasonStandingPeriod.Current, facts.Period);
        Assert.Equal(SparkySeasonStandingStatus.LeaderFound, facts.Status);
        Assert.Equal("Synthetic Helix Season", facts.SeasonName);
        Assert.Equal("Fixture Leader", facts.LeaderName);
        Assert.Equal("live-season-current", facts.SourceId);
    }

    [Fact]
    public void Natural_this_season_leading_wording_resolves_to_the_current_rank_one_fact()
    {
        var view = SparkyLiveKnowledgeFixtures.CreateReadView("season-sample-v1", Now);

        bool resolved = SparkySeasonStandingResolver.TryResolve(
            "Who's leading this season?",
            "who is leading the current season",
            view,
            out var facts);

        Assert.True(resolved);
        Assert.Equal("Fixture Leader", facts.LeaderName);
    }

    [Fact]
    public void Past_season_leader_question_resolves_to_the_most_recent_finalized_season()
    {
        var view = SparkyLiveKnowledgeFixtures.CreateReadView("season-sample-v1", Now);

        bool resolved = SparkySeasonStandingResolver.TryResolve(
            "Who was the leader of the past season in the game?",
            "who was the leader of the past season in the game",
            view,
            out var facts);

        Assert.True(resolved);
        Assert.Equal(SparkySeasonStandingPeriod.MostRecentFinal, facts.Period);
        Assert.Equal(SparkySeasonStandingStatus.LeaderFound, facts.Status);
        Assert.Equal("Jade Circuit", facts.SeasonName);
        Assert.Equal("Previous Fixture Champion", facts.LeaderName);
        Assert.Equal("live-season-final-1002", facts.SourceId);
    }

    [Fact]
    public void Past_season_leader_resolution_uses_end_time_instead_of_larger_season_id()
    {
        var older = FinalRecord(1002, "Older Circuit", "Older Leader", Now.AddDays(-60));
        var newer = FinalRecord(1001, "Newer Circuit", "Newer Leader", Now.AddDays(-30));
        var view = CreateView(new SparkyLiveKnowledgeProfileState(null, [older, newer]), "missing");

        bool resolved = SparkySeasonStandingResolver.TryResolve(
            "Who led the last season?",
            "who led the last season",
            view,
            out var facts);

        Assert.True(resolved);
        Assert.Equal("Newer Circuit", facts.SeasonName);
        Assert.Equal("Newer Leader", facts.LeaderName);
        Assert.Equal("live-season-final-1001", facts.SourceId);
    }

    [Fact]
    public void Current_season_leader_question_resolves_to_no_active_season_when_off_season()
    {
        var state = new SparkyLiveKnowledgeProfileState(
            Current(string.Empty, []) with
            {
                Current = new SparkySeasonCurrentState(null, []),
                ContentHash = new string('b', 64)
            },
            []);
        var view = CreateView(state, "fresh");

        bool resolved = SparkySeasonStandingResolver.TryResolve(
            "Who is the leader of the current season?",
            "who is the leader of the current season",
            view,
            out var facts);

        Assert.True(resolved);
        Assert.Equal(SparkySeasonStandingStatus.NoActiveSeason, facts.Status);
        Assert.Null(facts.LeaderName);
    }

    [Fact]
    public void Season_mechanics_question_is_not_misclassified_as_a_leader_lookup()
    {
        var view = SparkyLiveKnowledgeFixtures.CreateReadView("season-sample-v1", Now);

        bool resolved = SparkySeasonStandingResolver.TryResolve(
            "How does the current season scoring work?",
            "how does the current season scoring work",
            view,
            out _);

        Assert.False(resolved);
    }

    [Fact]
    public void Champion_question_selects_matching_finalized_season_source()
    {
        var record = new SparkySeasonFinalRecord(
            6, 1, new string('a', 64), Now,
            new SparkySeasonFinalization(6, "Winter Circuit", Now.AddDays(-5),
                [new SparkySeasonStanding(1, "Champion One", 900)]));
        var view = CreateView(new SparkyLiveKnowledgeProfileState(null, [record]), "missing");

        var result = Search("Who was the champion of Winter Circuit?", view);

        Assert.Contains(result, passage => passage.SourceId == "live-season-final-6");
    }

    [Fact]
    public void Stale_current_source_is_excluded_but_archived_final_stays_searchable()
    {
        var record = new SparkySeasonFinalRecord(
            6, 1, new string('a', 64), Now,
            new SparkySeasonFinalization(6, "Winter Circuit", Now.AddDays(-5),
                [new SparkySeasonStanding(1, "Champion One", 900)]));
        var state = new SparkyLiveKnowledgeProfileState(Current("Autumn Circuit", []), [record]);
        var view = CreateView(state, "stale");

        var currentResult = Search("Who leads Autumn Circuit this season?", view);
        var archivedResult = Search("Who was champion of Winter Circuit?", view);

        Assert.DoesNotContain(currentResult, passage => passage.SourceId == "live-season-current");
        Assert.Contains(archivedResult, passage => passage.SourceId == "live-season-final-6");
    }

    [Fact]
    public async Task Catalog_expires_current_snapshot_without_rebuilding_its_cached_index()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        string databasePath = Path.Combine(temp.Path, "live-knowledge.db");
        int buildCount = 0;
        var store = new SparkyLiveKnowledgeStore(databasePath);
        await store.InitializeAsync(cancellationToken);
        var catalog = new SparkyLiveKnowledgeCatalog(store, state =>
        {
            buildCount++;
            return SparkyLiveKnowledgePassageBuilder.BuildSupplement(state);
        });
        await catalog.InitializeAsync(cancellationToken);
        var current = Current("Autumn Circuit", [new SparkySeasonStanding(1, "Agent One", 250)]) with
        {
            CapturedAtUtc = Now.AddMinutes(-16)
        };

        await catalog.ApplyAsync(SparkyApiProfile.Dev, current, cancellationToken);
        int afterContentUpdate = buildCount;
        var staleView = catalog.GetReadView(SparkyApiProfile.Dev, Now, TimeSpan.FromMinutes(15));
        var staleResult = Search("Who leads Autumn Circuit?", staleView);
        var freshView = catalog.GetReadView(SparkyApiProfile.Dev, Now.AddMinutes(-1), TimeSpan.FromMinutes(30));

        Assert.Equal("stale", staleView.Diagnostics.Freshness);
        Assert.Contains("live-season-current", staleView.ExcludedSourceIds);
        Assert.DoesNotContain(staleResult, passage => passage.SourceId == "live-season-current");
        Assert.Equal("fresh", freshView.Diagnostics.Freshness);
        Assert.Equal(afterContentUpdate, buildCount);

        foreach (DateTimeOffset heartbeatTime in new[]
                 {
                     Now.AddMinutes(-11),
                     Now.AddMinutes(-6),
                     Now.AddMinutes(-1)
                 })
        {
            var heartbeat = current with
            {
                Operation = "refresh-current",
                Current = null,
                Finalization = null,
                CapturedAtUtc = heartbeatTime,
                ContentHash = current.ContentHash
            };
            await catalog.RefreshCurrentAsync(SparkyApiProfile.Dev, heartbeat, cancellationToken);
            Assert.Equal("fresh", catalog.GetReadView(
                SparkyApiProfile.Dev, heartbeatTime, TimeSpan.FromMinutes(15)).Diagnostics.Freshness);
        }

        Assert.Equal(afterContentUpdate, buildCount);
        Assert.Equal("fresh", catalog.GetReadView(SparkyApiProfile.Dev, Now, TimeSpan.FromMinutes(15)).Diagnostics.Freshness);
    }

    private static IReadOnlyList<SparkyKnowledgePassage> Search(string query, SparkyLiveKnowledgeReadView view)
    {
        var bundle = new SparkyKnowledgeBundle(
            SparkyKnowledgeBundle.CurrentSchemaVersion,
            new SparkyBundleManifest("bundle", "revision", Now, new Dictionary<int, string>(), new Dictionary<string, string>()),
            [], [], [], [], new SparkySearchIndex(new Dictionary<string, IReadOnlyList<int>>(), []));

        return new SparkyKnowledgeRetriever(bundle).Search(
            query, query, liveKnowledge: view, minimumRelevanceScore: 0.1);
    }

    private static IReadOnlyList<SparkyKnowledgePassage> SearchAtDefaultThreshold(string query, SparkyLiveKnowledgeReadView view)
    {
        var bundle = new SparkyKnowledgeBundle(
            SparkyKnowledgeBundle.CurrentSchemaVersion,
            new SparkyBundleManifest("bundle", "revision", Now, new Dictionary<int, string>(), new Dictionary<string, string>()),
            [], [], [], [], new SparkySearchIndex(new Dictionary<string, IReadOnlyList<int>>(), []));

        return new SparkyKnowledgeRetriever(bundle).Search(query, query, liveKnowledge: view);
    }

    private static SparkyLiveKnowledgeReadView CreateView(SparkyLiveKnowledgeProfileState state, string freshness)
    {
        var supplement = SparkyLiveKnowledgePassageBuilder.BuildSupplement(state);
        var excluded = freshness == "stale"
            ? new HashSet<string>(["live-season-current"], StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        return new SparkyLiveKnowledgeReadView(
            supplement,
            excluded,
            new SparkyLiveKnowledgeDiagnostics("seasons", 1, state.Current?.ContentHash, 0, freshness, state.FinalizedSeasons.Count),
            state);
    }

    private static SparkyLiveKnowledgeUpdate Current(string seasonName, IReadOnlyList<SparkySeasonStanding> standings)
    {
        var facts = new SparkySeasonFacts(
            8, seasonName, "A public scoring season", Now.AddDays(-1), Now.AddDays(10), "ActivityAndGlobal", [], [], []);
        var update = new SparkyLiveKnowledgeUpdate(1, "seasons", "dev", "replace-current", Now, new string('a', 64),
            new SparkySeasonCurrentState(facts, standings), null);
        return update with { ContentHash = SparkyLiveKnowledgeValidator.ComputeCurrentHash(update) };
    }

    private static SparkySeasonFinalRecord FinalRecord(
        int seasonId,
        string seasonName,
        string leaderName,
        DateTimeOffset endedAtUtc)
    {
        var finalization = new SparkySeasonFinalization(
            seasonId,
            seasonName,
            endedAtUtc,
            [new SparkySeasonStanding(1, leaderName, 100)]);
        var update = new SparkyLiveKnowledgeUpdate(
            1,
            "seasons",
            "dev",
            "finalize-season",
            endedAtUtc,
            new string('c', 64),
            null,
            finalization);
        return new SparkySeasonFinalRecord(
            seasonId,
            1,
            SparkyLiveKnowledgeValidator.ComputeFinalizationHash(update),
            endedAtUtc,
            finalization);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sparky-live-routing-" + Guid.NewGuid().ToString("N"));
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
