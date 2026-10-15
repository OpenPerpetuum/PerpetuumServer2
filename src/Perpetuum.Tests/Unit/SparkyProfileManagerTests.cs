using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyProfileManagerTests
{
    [Fact]
    public async Task Activating_Dev_keeps_Live_snapshot_unchanged_and_existing_requests_hold_their_revision()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        using var certificate = CreateCertificate();
        var bundle = Bundle("bundle-1234567890abcdef");
        var catalog = new FakeCatalog(bundle);
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        using var options = Options(temp.Path, certificate, liveToken: "sparky-live-token-that-is-distinct-and-long-enough");
        var manager = new SparkyProfileManager(store, catalog, options, bundle);

        await manager.InitializeAsync(cancellationToken);
        var devBefore = Assert.IsType<SparkyProfileSnapshot>(GetSnapshot(manager, SparkyApiProfile.Dev));
        var initialLiveDraft = await manager.SaveDraftAsync(
            SparkyApiProfile.Dev,
            devBefore.Configuration with { ModelId = "live-model" },
            "prepare Live promotion",
            cancellationToken);
        Assert.True(initialLiveDraft.Success);
        var promotion = await manager.PromoteDevToLiveAsync(initialLiveDraft.RevisionId!, "enable Live", cancellationToken);
        Assert.True(promotion.Success);
        var liveBefore = Assert.IsType<SparkyProfileSnapshot>(GetSnapshot(manager, SparkyApiProfile.Live));

        var devDraft = await manager.SaveDraftAsync(SparkyApiProfile.Dev, devBefore.Configuration with { ModelId = "dev-model-v2" }, "change Dev model", cancellationToken);
        Assert.True(devDraft.Success);
        Assert.True((await manager.ActivateAsync(SparkyApiProfile.Dev, devDraft.RevisionId!, "activate Dev model", cancellationToken)).Success);

        Assert.Equal("dev-model-v2", GetSnapshot(manager, SparkyApiProfile.Dev)!.Configuration.ModelId);
        Assert.Equal("live-model", GetSnapshot(manager, SparkyApiProfile.Live)!.Configuration.ModelId);
        Assert.Equal("qwen3-4b-instruct", devBefore.Configuration.ModelId);
        Assert.Equal("live-model", liveBefore.Configuration.ModelId);
    }

    [Fact]
    public async Task Live_drafts_and_direct_activation_are_rejected_in_favor_of_explicit_promotion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        using var certificate = CreateCertificate();
        var bundle = Bundle("bundle-1234567890abcdef");
        var manager = new SparkyProfileManager(
            new SparkyTraceStore(Path.Combine(temp.Path, "traces.db")),
            new FakeCatalog(bundle),
            Options(temp.Path, certificate, liveToken: "sparky-live-token-that-is-distinct-and-long-enough"),
            bundle);
        await manager.InitializeAsync(cancellationToken);
        var dev = Assert.IsType<SparkyProfileSnapshot>(GetSnapshot(manager, SparkyApiProfile.Dev));

        var liveDraft = await manager.SaveDraftAsync(
            SparkyApiProfile.Live, dev.Configuration, "bypass promotion", cancellationToken);
        var activation = await manager.ActivateAsync(SparkyApiProfile.Live, Guid.NewGuid().ToString("N"), null, cancellationToken);

        Assert.False(liveDraft.Success);
        Assert.Equal("live_requires_promotion", liveDraft.ErrorCode);
        Assert.False(activation.Success);
        Assert.Equal("live_requires_promotion", activation.ErrorCode);
        Assert.False(manager.TryGetSnapshot(SparkyApiProfile.Live, out _));
    }

    [Fact]
    public async Task Invalid_draft_does_not_replace_the_active_snapshot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        using var certificate = CreateCertificate();
        var bundle = Bundle("bundle-1234567890abcdef");
        var manager = new SparkyProfileManager(
            new SparkyTraceStore(Path.Combine(temp.Path, "traces.db")),
            new FakeCatalog(bundle),
            Options(temp.Path, certificate, liveToken: null),
            bundle);
        await manager.InitializeAsync(cancellationToken);
        var activeBefore = GetSnapshot(manager, SparkyApiProfile.Dev)!;

        var invalid = await manager.SaveDraftAsync(SparkyApiProfile.Dev, activeBefore.Configuration with { MaxAnswerCharacters = 9000 }, "invalid bounds", cancellationToken);
        var activation = await manager.ActivateAsync(SparkyApiProfile.Dev, invalid.RevisionId!, null, cancellationToken);

        Assert.False(activation.Success);
        Assert.Contains(activation.Errors!, error => error.Contains("MaxAnswerCharacters", StringComparison.Ordinal));
        Assert.Same(activeBefore, GetSnapshot(manager, SparkyApiProfile.Dev));
    }

    [Fact]
    public async Task Restart_loads_active_revision_and_rollback_creates_a_new_revision()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        using var certificate = CreateCertificate();
        var bundle = Bundle("bundle-1234567890abcdef");
        var catalog = new FakeCatalog(bundle);
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        using var options = Options(temp.Path, certificate, liveToken: null);
        var first = new SparkyProfileManager(store, catalog, options, bundle);
        await first.InitializeAsync(cancellationToken);
        string initialRevision = GetSnapshot(first, SparkyApiProfile.Dev)!.RevisionId;
        var edit = await first.SaveDraftAsync(SparkyApiProfile.Dev, GetSnapshot(first, SparkyApiProfile.Dev)!.Configuration with { PersonaStyle = "Short and formal." }, "edit style", cancellationToken);
        Assert.True((await first.ActivateAsync(SparkyApiProfile.Dev, edit.RevisionId!, "apply edit", cancellationToken)).Success);

        var restarted = new SparkyProfileManager(store, catalog, options, bundle);
        await restarted.InitializeAsync(cancellationToken);
        Assert.Equal(edit.RevisionId, GetSnapshot(restarted, SparkyApiProfile.Dev)!.RevisionId);
        var rollback = await restarted.RollbackAsync(SparkyApiProfile.Dev, "restore prior behavior", cancellationToken);

        Assert.True(rollback.Success);
        Assert.NotEqual(initialRevision, rollback.RevisionId);
        Assert.Equal("Clear, calm, practical, and friendly. Use dry humor only when it fits.",
            GetSnapshot(restarted, SparkyApiProfile.Dev)!.Configuration.PersonaStyle);
    }

    [Fact]
    public async Task Disabled_Live_retention_uses_its_last_active_policy_or_defaults()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        using var certificate = CreateCertificate();
        var bundle = Bundle("bundle-1234567890abcdef");
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var catalog = new FakeCatalog(bundle);
        using var enabledOptions = Options(temp.Path, certificate, liveToken: "sparky-live-token-that-is-distinct-and-long-enough");
        var enabledManager = new SparkyProfileManager(store, catalog, enabledOptions, bundle);
        await enabledManager.InitializeAsync(cancellationToken);
        var dev = Assert.IsType<SparkyProfileSnapshot>(GetSnapshot(enabledManager, SparkyApiProfile.Dev));
        var draft = await enabledManager.SaveDraftAsync(
            SparkyApiProfile.Dev,
            dev.Configuration with { ContentRetentionDays = 11, MetadataRetentionDays = 55 },
            "prepare retention policy",
            cancellationToken);
        Assert.True(draft.Success);
        Assert.True((await enabledManager.ActivateAsync(SparkyApiProfile.Dev, draft.RevisionId!, null, cancellationToken)).Success);
        Assert.True((await enabledManager.PromoteDevToLiveAsync(draft.RevisionId!, "promote retention policy", cancellationToken)).Success);

        using var disabledOptions = Options(temp.Path, certificate, liveToken: null);
        var disabledManager = new SparkyProfileManager(store, catalog, disabledOptions, bundle);
        await disabledManager.InitializeAsync(cancellationToken);
        var disabledPolicy = await disabledManager.GetTraceRetentionPolicyAsync(SparkyApiProfile.Live, cancellationToken);
        var devPolicy = await disabledManager.GetTraceRetentionPolicyAsync(SparkyApiProfile.Dev, cancellationToken);

        Assert.Equal(11, disabledPolicy.ContentRetentionDays);
        Assert.Equal(55, disabledPolicy.MetadataRetentionDays);
        Assert.Equal(11, devPolicy.ContentRetentionDays);
        Assert.Equal(55, devPolicy.MetadataRetentionDays);

        var defaultsStore = new SparkyTraceStore(Path.Combine(temp.Path, "defaults-traces.db"));
        var defaultsManager = new SparkyProfileManager(defaultsStore, catalog, disabledOptions, bundle);
        await defaultsManager.InitializeAsync(cancellationToken);
        var defaultPolicy = await defaultsManager.GetTraceRetentionPolicyAsync(SparkyApiProfile.Live, cancellationToken);
        Assert.Equal(30, defaultPolicy.ContentRetentionDays);
        Assert.Equal(90, defaultPolicy.MetadataRetentionDays);
    }

    [Fact]
    public async Task Activation_audit_lists_only_configuration_groups_that_changed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        using var certificate = CreateCertificate();
        var bundle = Bundle("bundle-1234567890abcdef");
        var manager = new SparkyProfileManager(
            new SparkyTraceStore(Path.Combine(temp.Path, "traces.db")),
            new FakeCatalog(bundle),
            Options(temp.Path, certificate, liveToken: null),
            bundle);
        await manager.InitializeAsync(cancellationToken);
        var active = Assert.IsType<SparkyProfileSnapshot>(GetSnapshot(manager, SparkyApiProfile.Dev));
        var draft = await manager.SaveDraftAsync(
            SparkyApiProfile.Dev,
            active.Configuration with
            {
                ModelId = "new-model",
                CaptureContent = false,
                ConversationHistoryTurns = 6,
                ConversationHistoryCharacters = 3200,
                AnswerTemperature = 0.35,
                AnswerTopP = 0.85
            },
            "change two settings",
            cancellationToken);

        Assert.True((await manager.ActivateAsync(SparkyApiProfile.Dev, draft.RevisionId!, null, cancellationToken)).Success);

        var audit = (await manager.GetDetailsAsync(SparkyApiProfile.Dev, cancellationToken)).Audit
            .First(entry => entry.Action == "activate");
        Assert.Equal(["modelId", "conversationContext", "answerModelTuning", "contentCapture"], audit.ChangedFields);
    }

    [Fact]
    public async Task Restart_migrates_active_schema_v1_profile_with_conservative_tuning_defaults()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        using var certificate = CreateCertificate();
        var bundle = Bundle("bundle-1234567890abcdef");
        var catalog = new FakeCatalog(bundle);
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "legacy-profile.db"));
        const string legacyJson = """
            {"schemaVersion":1,"modelId":"legacy-model","maxAnswerCharacters":2000,"maxCompletionTokens":700,"inferenceTimeoutSeconds":120,"maxConcurrentRequests":1,"captureContent":true,"contentRetentionDays":30,"metadataRetentionDays":90,"personaStyle":"Practical and concise.","retrieval":{"maxPassages":5,"maxCharacters":4200,"minimumScore":0.25},"routeAliases":[],"bundleVersion":"bundle-1234567890abcdef"}
            """;
        await store.SaveProfileRevisionAsync(new SparkyProfileRevisionRecord(
            SparkyApiProfile.Dev, "rev-legacy-v1", DateTimeOffset.UtcNow, null, legacyJson,
            bundle.Manifest.BundleVersion, false, true), "test", "seed old revision", cancellationToken);
        Assert.True(await store.ActivateProfileRevisionAsync(
            SparkyApiProfile.Dev, "rev-legacy-v1", "test", "activate", null, null, [], cancellationToken));

        using var options = Options(temp.Path, certificate, liveToken: null);
        var manager = new SparkyProfileManager(store, catalog, options, bundle);
        await manager.InitializeAsync(cancellationToken);

        var migrated = Assert.IsType<SparkyProfileSnapshot>(GetSnapshot(manager, SparkyApiProfile.Dev));
        Assert.Equal(2, migrated.Configuration.SchemaVersion);
        Assert.Equal(8, migrated.Configuration.ConversationHistoryTurns);
        Assert.Equal(4000, migrated.Configuration.ConversationHistoryCharacters);
        Assert.Equal(0.2, migrated.Configuration.AnswerTemperature);
        Assert.Equal(0.9, migrated.Configuration.AnswerTopP);
        Assert.Equal("legacy-model", migrated.Configuration.ModelId);
        SparkyServiceOptions applied = migrated.ApplyTo(options);
        Assert.Equal(8, applied.ConversationHistoryTurns);
        Assert.Equal(4000, applied.ConversationHistoryCharacters);
        Assert.Equal(0.2, applied.AnswerTemperature);
        Assert.Equal(0.9, applied.AnswerTopP);
    }

    [Fact]
    public void Rejects_unknown_profile_schema_instead_of_treating_it_as_a_new_default()
    {
        Assert.Throws<System.IO.InvalidDataException>(() =>
            SparkyProfileManager.DeserializeDraft("{\"schemaVersion\":99}"));
    }

    [Fact]
    public async Task Retention_pass_applies_persisted_policy_to_disabled_Live_profile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        using var certificate = CreateCertificate();
        var bundle = Bundle("bundle-1234567890abcdef");
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var catalog = new FakeCatalog(bundle);
        using var enabledOptions = Options(temp.Path, certificate, liveToken: "sparky-live-token-that-is-distinct-and-long-enough");
        var enabledManager = new SparkyProfileManager(store, catalog, enabledOptions, bundle);
        await enabledManager.InitializeAsync(cancellationToken);
        var dev = Assert.IsType<SparkyProfileSnapshot>(GetSnapshot(enabledManager, SparkyApiProfile.Dev));
        var draft = await enabledManager.SaveDraftAsync(
            SparkyApiProfile.Dev,
            dev.Configuration with { ContentRetentionDays = 11, MetadataRetentionDays = 55 },
            "prepare Live retention policy",
            cancellationToken);
        Assert.True(draft.Success);
        Assert.True((await enabledManager.ActivateAsync(SparkyApiProfile.Dev, draft.RevisionId!, null, cancellationToken)).Success);
        Assert.True((await enabledManager.PromoteDevToLiveAsync(draft.RevisionId!, "promote retention policy", cancellationToken)).Success);

        using var disabledOptions = Options(temp.Path, certificate, liveToken: null);
        var disabledManager = new SparkyProfileManager(store, catalog, disabledOptions, bundle);
        await disabledManager.InitializeAsync(cancellationToken);
        var oldLiveEvent = new SparkyTraceEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Discord", "answer", "answered",
            DateTimeOffset.UtcNow.AddDays(-20), ElapsedMilliseconds: 100,
            DetailCode: "ok", Question: "old question", Answer: "old answer");
        await store.AppendAsync(oldLiveEvent, SparkyApiProfile.Live, captureContent: true, cancellationToken);

        var retentionService = new SparkyTraceRetentionService(
            store, disabledManager, Microsoft.Extensions.Logging.Abstractions.NullLogger<SparkyTraceRetentionService>.Instance);
        await retentionService.ApplyRetentionPassAsync(cancellationToken);
        var retained = await store.GetTraceAsync(oldLiveEvent.RequestId, SparkyApiProfile.Live, cancellationToken);

        var retainedEvent = Assert.Single(retained!.Events);
        Assert.Null(retainedEvent.Question);
        Assert.Null(retainedEvent.Answer);
    }

    private static SparkyProfileSnapshot? GetSnapshot(SparkyProfileManager manager, SparkyApiProfile profile) =>
        manager.TryGetSnapshot(profile, out var snapshot) ? snapshot : null;

    private static SparkyProfileDraft Draft(string bundleVersion, string modelId) => new(
        2, modelId, 2000, 700, 120, 1, false, 30, 90,
        "Practical and concise.", new SparkyRetrievalSettings(5, 4200, 0.25), [], bundleVersion,
        8, 4000, 0.2, 0.9);

    private static SparkyKnowledgeBundle Bundle(string version) => new(
        3,
        new SparkyBundleManifest(version, "revision", DateTimeOffset.UtcNow,
            new Dictionary<int, string>(), new Dictionary<string, string>()),
        [], [], [], [], new SparkySearchIndex(new Dictionary<string, IReadOnlyList<int>>(), []));

    private static SparkyServiceOptions Options(string root, X509Certificate2 certificate, string? liveToken) => new(
        Path.Combine(root, "legacy.json"), new Uri("https://127.0.0.1:5080"), certificate,
        "sparky-dev-token-that-is-long-enough-for-test-use", "http://localhost:1234/v1", "qwen3-4b-instruct", null,
        8192, 2000, 2000, 700, TimeSpan.FromSeconds(120), 2, null, liveToken,
        Path.Combine(root, "traces.db"), true, false, 30, 90);

    private static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Sparky Test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private sealed class FakeCatalog(SparkyKnowledgeBundle bundle) : ISparkyBundleCatalog
    {
        public void RegisterExisting(string version, string path, SparkyKnowledgeBundle existing)
        {
            if (version != bundle.Manifest.BundleVersion)
                throw new InvalidDataException();
        }

        public IReadOnlyList<SparkyBundleInfo> List() => [];
        public SparkyKnowledgeBundle GetRequired(string version) => version == bundle.Manifest.BundleVersion ? bundle : throw new InvalidDataException();
        public Task<SparkyBundleInfo> StageAsync(Stream input, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sparky-profiles-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            File.WriteAllText(System.IO.Path.Combine(Path, "legacy.json"), "{}");
        }

        public string Path { get; }
        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
