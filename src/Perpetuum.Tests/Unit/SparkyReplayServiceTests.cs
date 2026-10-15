using System.Net;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyReplayServiceTests
{
    [Fact]
    public async Task Replay_compares_active_and_draft_without_changing_active_profile_or_persisting_a_trace()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var bundle = CreateBundle();
        var options = CreateOptions(temp.Path);
        var profiles = new SparkyProfileManager(store, new FakeBundleCatalog(bundle), options, bundle);
        await profiles.InitializeAsync(cancellationToken);
        var original = profiles.GetSummaries().Single(summary => summary.Profile == "Dev").ActiveRevisionId!;
        var activeConfiguration = (await profiles.GetDetailsAsync(SparkyApiProfile.Dev, cancellationToken)).ActiveConfiguration!;
        var draft = await profiles.SaveDraftAsync(SparkyApiProfile.Dev, activeConfiguration with { PersonaStyle = "Use concise field notes." }, "replay draft", cancellationToken);
        Assert.True(draft.Success);

        var trace = new SparkyTraceEvent(Guid.NewGuid(), Guid.NewGuid(), "AI", "answer", "answered", DateTimeOffset.UtcNow,
            Intent: "MechanicsLore", Question: "Who is Theseus?", Answer: "Existing answer.");
        await store.AppendAsync(trace, SparkyApiProfile.Dev, captureContent: true, cancellationToken);
        var example = await store.CreateEvaluationExampleAsync(trace.RequestId, SparkyApiProfile.Dev, "MechanicsLore", ["entity:Theseus"], cancellationToken);
        var answerer = new FakeAnswerer();
        var service = new SparkyReplayService(store, profiles, new SparkyConcurrencyLimiter(), options, answerer);

        var replay = await service.RunAsync(
            new SparkyReplayRequest(SparkyApiProfile.Dev, draft.RevisionId!, [example!.ExampleId]), cancellationToken);
        var stored = await store.GetReplayAsync(replay.ReplayId, SparkyApiProfile.Dev, cancellationToken);
        var activeAfter = profiles.GetSummaries().Single(summary => summary.Profile == "Dev").ActiveRevisionId;

        Assert.Equal(original, replay.ActiveRevisionId);
        Assert.Equal(draft.RevisionId, replay.CandidateRevisionId);
        Assert.Equal(original, activeAfter);
        Assert.Equal(2, answerer.Calls);
        Assert.Equal("active answer", replay.Results[0].Active.Answer);
        Assert.Equal("draft answer", replay.Results[0].Candidate.Answer);
        Assert.NotNull(stored);
        Assert.Equal(replay.ReplayId, stored.ReplayId);
        Assert.Null(await store.GetTraceAsync(replay.ReplayId, SparkyApiProfile.Dev, cancellationToken));
    }

    [Fact]
    public async Task Dev_season_fixture_is_shared_by_active_and_draft_without_changing_durable_live_state()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var bundle = CreateBundle();
        var options = CreateOptions(temp.Path);
        var profiles = new SparkyProfileManager(store, new FakeBundleCatalog(bundle), options, bundle);
        await profiles.InitializeAsync(cancellationToken);
        var active = (await profiles.GetDetailsAsync(SparkyApiProfile.Dev, cancellationToken)).ActiveConfiguration!;
        var draft = await profiles.SaveDraftAsync(SparkyApiProfile.Dev, active with { PersonaStyle = "Use concise field notes." }, "fixture draft", cancellationToken);
        Assert.True(draft.Success);

        var trace = new SparkyTraceEvent(Guid.NewGuid(), Guid.NewGuid(), "AI", "answer", "answered", DateTimeOffset.UtcNow,
            Intent: "MechanicsLore", Question: "Who won the previous season?", Answer: "Fixture Champion.");
        await store.AppendAsync(trace, SparkyApiProfile.Dev, captureContent: true, cancellationToken);
        var example = await store.CreateEvaluationExampleAsync(trace.RequestId, SparkyApiProfile.Dev, "MechanicsLore", ["entity:FixtureChampion"], cancellationToken);
        var liveStore = new SparkyLiveKnowledgeStore(Path.Combine(temp.Path, "live-knowledge.db"));
        await liveStore.InitializeAsync(cancellationToken);
        var liveCatalog = new SparkyLiveKnowledgeCatalog(liveStore, SparkyLiveKnowledgePassageBuilder.BuildSupplement);
        await liveCatalog.InitializeAsync(cancellationToken);
        var answerer = new FakeAnswerer();
        var service = new SparkyReplayService(
            store, profiles, new SparkyConcurrencyLimiter(), options, answerer, liveCatalog);

        await service.RunAsync(
            new SparkyReplayRequest(SparkyApiProfile.Dev, draft.RevisionId!, [example!.ExampleId], "season-sample-v1"),
            cancellationToken);

        Assert.Equal(2, answerer.Calls);
        Assert.NotNull(answerer.LiveKnowledgeViews[0]);
        Assert.Same(answerer.LiveKnowledgeViews[0], answerer.LiveKnowledgeViews[1]);
        Assert.Equal("fresh", answerer.LiveKnowledgeViews[0]!.Diagnostics.Freshness);
        Assert.Equal(2, answerer.LiveKnowledgeViews[0]!.Diagnostics.CompletedSeasonCount);
        Assert.Contains(answerer.LiveKnowledgeViews[0]!.Supplement.Passages,
            passage => passage.SourceId == "live-season-final-1002" && passage.Text.Contains("Previous Fixture Champion", StringComparison.Ordinal));

        var durableState = await liveStore.LoadProfileAsync(SparkyApiProfile.Dev, cancellationToken);
        Assert.Null(durableState.Current);
        Assert.Empty(durableState.FinalizedSeasons);
    }

    [Fact]
    public async Task Synthetic_live_fixture_is_rejected_for_live_replay()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var bundle = CreateBundle();
        var options = CreateOptions(temp.Path);
        var profiles = new SparkyProfileManager(store, new FakeBundleCatalog(bundle), options, bundle);
        await profiles.InitializeAsync(cancellationToken);
        var answerer = new FakeAnswerer();
        var service = new SparkyReplayService(store, profiles, new SparkyConcurrencyLimiter(), options, answerer);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.RunAsync(
            new SparkyReplayRequest(SparkyApiProfile.Live, "draft", [Guid.NewGuid()], "season-sample-v1"), cancellationToken));
        Assert.Equal(0, answerer.Calls);
    }

    [Fact]
    public async Task Replay_rejects_more_than_one_hundred_examples_before_inference()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var bundle = CreateBundle();
        var options = CreateOptions(temp.Path);
        var profiles = new SparkyProfileManager(store, new FakeBundleCatalog(bundle), options, bundle);
        await profiles.InitializeAsync(cancellationToken);
        var revision = profiles.GetSummaries().Single(summary => summary.Profile == "Dev").DraftRevisionId!;
        var answerer = new FakeAnswerer();
        var service = new SparkyReplayService(store, profiles, new SparkyConcurrencyLimiter(), options, answerer);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.RunAsync(
            new SparkyReplayRequest(SparkyApiProfile.Dev, revision, Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray()), cancellationToken));

        Assert.Equal(0, answerer.Calls);
    }

    [Fact]
    public async Task Replay_runs_ordered_followups_with_isolated_active_and_draft_contexts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "sequence-traces.db"));
        var bundle = CreateBundle();
        var options = CreateOptions(temp.Path);
        var profiles = new SparkyProfileManager(store, new FakeBundleCatalog(bundle), options, bundle);
        await profiles.InitializeAsync(cancellationToken);
        var active = (await profiles.GetDetailsAsync(SparkyApiProfile.Dev, cancellationToken)).ActiveConfiguration!;
        var draft = await profiles.SaveDraftAsync(SparkyApiProfile.Dev, active with { PersonaStyle = "Use concise field notes." }, "sequence draft", cancellationToken);
        Assert.True(draft.Success);

        var trace = new SparkyTraceEvent(Guid.NewGuid(), Guid.NewGuid(), "AI", "answer", "answered", DateTimeOffset.UtcNow,
            Intent: "StructuredItem", Question: "What are T3 sensor amplifier stats?", Answer: "Which one?");
        await store.AppendAsync(trace, SparkyApiProfile.Dev, captureContent: true, cancellationToken);
        var turns = new[]
        {
            new SparkyEvaluationTurn("What are T3 sensor amplifier stats?", "Game", "StructuredItem", "T3 sensor amplifier"),
            new SparkyEvaluationTurn("Da Gama", "Game", "StructuredItem", "Da Gama"),
            new SparkyEvaluationTurn("And its full recipe?", "Game", "StructuredItem", "Da Gama", RequiredFacts: ["Axicoline"])
        };
        var example = await store.CreateEvaluationExampleAsync(
            trace.RequestId, SparkyApiProfile.Dev, turns[0].ExpectedIntent, [], cancellationToken, turns, "clarify then answer");
        var secondTrace = new SparkyTraceEvent(Guid.NewGuid(), Guid.NewGuid(), "AI", "answer", "answered", DateTimeOffset.UtcNow,
            Intent: "MechanicsLore", Question: "Who is Theseus?", Answer: "A figure in the lore records.");
        await store.AppendAsync(secondTrace, SparkyApiProfile.Dev, captureContent: true, cancellationToken);
        var secondExample = await store.CreateEvaluationExampleAsync(
            secondTrace.RequestId, SparkyApiProfile.Dev, "MechanicsLore", ["entity:Theseus"], cancellationToken);
        var storedExample = (await store.GetEvaluationExamplesAsync(SparkyApiProfile.Dev, cancellationToken: cancellationToken))
            .Single(candidate => candidate.RequestId == trace.RequestId);
        Assert.Equal(turns.Select(turn => turn.Question), storedExample.Turns!.Select(turn => turn.Question));
        Assert.Equal("clarify then answer", storedExample.Note);

        var answerer = new FakeAnswerer();
        var service = new SparkyReplayService(store, profiles, new SparkyConcurrencyLimiter(), options, answerer);
        var replay = await service.RunAsync(
            new SparkyReplayRequest(SparkyApiProfile.Dev, draft.RevisionId!, [example!.ExampleId, secondExample!.ExampleId]), cancellationToken);

        Assert.Equal(8, answerer.Calls);
        Assert.Empty(answerer.Contexts[0]);
        Assert.Equal(["What are T3 sensor amplifier stats?", "active answer"], answerer.Contexts[1].Select(turn => turn.Content));
        Assert.Equal(4, answerer.Contexts[2].Count);
        Assert.Empty(answerer.Contexts[3]);
        Assert.Equal(["What are T3 sensor amplifier stats?", "draft answer"], answerer.Contexts[4].Select(turn => turn.Content));
        Assert.Equal(4, answerer.Contexts[5].Count);
        Assert.Empty(answerer.Contexts[6]);
        Assert.Empty(answerer.Contexts[7]);
        Assert.Equal(turns.Select(turn => turn.Question), replay.Results[0].Turns!.Select(turn => turn.Question));
        Assert.Equal("Game", replay.Results[0].Turns![2].Active.Scope);
        Assert.Equal("And its full recipe?", replay.Results[0].Turns![2].Question);
        Assert.Equal("theseus lore query", replay.Results[1].Turns![0].Active.SearchQuery);
        Assert.Equal(["source-1"], replay.Results[1].Turns![0].Active.SelectedSourceIds);
        Assert.Equal("StructuredRobot", replay.Results[0].Turns![2].Candidate.EffectiveIntent);
        Assert.Equal("CompareStats", replay.Results[0].Turns![2].Candidate.EffectiveFocus);
        Assert.Equal("resolved", replay.Results[0].Turns![2].Candidate.ResolverOutcome);
        Assert.Equal("resolved: Kain vs Tyrannos; requested maximum speed; Maximum speed: 103.32 vs 93.6 kph (difference 9.72 kph; direction favors Kain)",
            replay.Results[0].Turns![2].Candidate.ComparisonSummary);
        Assert.Equal("model", replay.Results[0].Turns![2].Candidate.Metadata!.ModelId);
        Assert.Equal("draft answer", replay.Results[0].Candidate.Answer);
    }

    [Fact]
    public async Task Replay_rejects_an_oversized_or_empty_sequence_before_persisting_it()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "invalid-sequence.db"));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.CreateEvaluationExampleAsync(
            Guid.NewGuid(), SparkyApiProfile.Dev, null, [], cancellationToken, [], null));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.CreateEvaluationExampleAsync(
            Guid.NewGuid(), SparkyApiProfile.Dev, null, [], cancellationToken,
            [new SparkyEvaluationTurn(new string('x', 2001))], null));
    }

    private static SparkyServiceOptions CreateOptions(string root) => new(
        Path.Combine(root, "legacy.json"), new Uri("https://127.0.0.1:5080/"), null!,
        "dev-token-that-is-long-enough-for-this-test", "http://localhost:1234/v1", "model", null,
        8192, 2000, 2000, 700, TimeSpan.FromSeconds(20), 2, null, null,
        Path.Combine(root, "traces.db"));

    private static SparkyKnowledgeBundle CreateBundle() => new(
        SparkyKnowledgeBundle.CurrentSchemaVersion,
        new SparkyBundleManifest("bundle-test", "content-test", DateTimeOffset.UtcNow, new Dictionary<int, string>(), new Dictionary<string, string> { ["test"] = "test" }),
        [], [], [], [], new SparkySearchIndex(new Dictionary<string, IReadOnlyList<int>>(), []));

    private sealed class FakeAnswerer : ISparkyReplayAnswerer
    {
        public List<IReadOnlyList<SparkyConversationTurn>> Contexts { get; } = [];
        public List<SparkyLiveKnowledgeReadView?> LiveKnowledgeViews { get; } = [];
        public int Calls => Contexts.Count;
        public Task<SparkyReplayStep> AnswerAsync(
            string question,
            SparkyProfileSnapshot snapshot,
            IReadOnlyList<SparkyConversationTurn> contextTurns,
            string? contextSummary,
            SparkyPendingClarification? pendingClarification,
            CancellationToken cancellationToken,
            SparkyLiveKnowledgeReadView? liveKnowledge)
        {
            Contexts.Add(contextTurns.ToArray());
            LiveKnowledgeViews.Add(liveKnowledge);
            string status = snapshot.Configuration.PersonaStyle.Contains("concise", StringComparison.Ordinal) ? "draft" : "active";
            string answer = status + " answer";
            var output = new SparkyReplayOutput("answered", "MechanicsLore", ["Theseus"], ["source-1"], answer,
                "Game", "theseus lore query", "Theseus", new SparkyReplayProfileMetadata(
                    snapshot.Configuration.ModelId, snapshot.RevisionId, snapshot.Bundle.Manifest.BundleVersion,
                    snapshot.Configuration.AnswerTemperature, snapshot.Configuration.AnswerTopP,
                    snapshot.Configuration.ConversationHistoryTurns, snapshot.Configuration.ConversationHistoryCharacters),
                "StructuredRobot", "CompareStats", "resolved",
                "resolved: Kain vs Tyrannos; requested maximum speed; Maximum speed: 103.32 vs 93.6 kph (difference 9.72 kph; direction favors Kain)");
            return Task.FromResult(new SparkyReplayStep(output, new SparkyConversationUpdate(null, null, null)));
        }
    }

    private sealed class FakeBundleCatalog(SparkyKnowledgeBundle bundle) : ISparkyBundleCatalog
    {
        public void RegisterExisting(string version, string path, SparkyKnowledgeBundle existing) { }
        public IReadOnlyList<SparkyBundleInfo> List() => [];
        public SparkyKnowledgeBundle GetRequired(string version) => bundle;
        public Task<SparkyBundleInfo> StageAsync(Stream input, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sparky-replay-{Guid.NewGuid():N}");
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
