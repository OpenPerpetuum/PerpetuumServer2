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
        public int Calls { get; private set; }
        public Task<SparkyReplayOutput> AnswerAsync(string question, SparkyProfileSnapshot snapshot, CancellationToken cancellationToken)
        {
            Calls++;
            string status = snapshot.Configuration.PersonaStyle.Contains("concise", StringComparison.Ordinal) ? "draft" : "active";
            return Task.FromResult(new SparkyReplayOutput("answered", "MechanicsLore", ["Theseus"], ["source-1"], status + " answer"));
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
