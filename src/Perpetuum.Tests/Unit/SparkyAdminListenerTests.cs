using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyAdminListenerTests
{
    private const string AdminToken = "sparky-admin-token-that-is-at-least-32-characters";
    private const string DevToken = "sparky-dev-token-that-is-at-least-32-characters-long";
    private const string LiveToken = "sparky-live-token-that-is-at-least-32-characters-long";

    [Fact]
    public async Task AdminApiRequiresItsOwnTokenAndDoesNotExposeGameApiRoutes()
    {
        int port = GetAvailablePort();
        string traceDirectory = Path.Combine(Path.GetTempPath(), $"sparky-admin-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(traceDirectory);
        var options = new SparkyServiceOptions(
            "unused.json",
            new Uri("https://127.0.0.1:5080/"),
            null!,
            DevToken,
            "http://localhost:1234/v1",
            "test-model",
            null,
            8192,
            2000,
            2000,
            700,
            TimeSpan.FromSeconds(10),
            1,
            null,
            LiveToken,
            Path.Combine(traceDirectory, "traces.db"),
            AdminServiceUri: new Uri("https://127.0.0.1:5081/"),
            AdminApiToken: AdminToken);
        var bundle = CreateEmptyBundle();
        var traceStore = new SparkyTraceStore(options.TraceDatabasePath!);
        var traceRequestId = Guid.NewGuid();
        await traceStore.AppendAsync(new SparkyTraceEvent(
            Guid.NewGuid(), traceRequestId, "AI", "answer", "answered", DateTimeOffset.UtcNow,
            Intent: "MechanicsLore", Question: "Who is Theseus?", Answer: "A local record identifies Theseus.",
            EffectiveIntent: "StructuredRobot", EffectiveFocus: "CompareStats", ResolverCategory: "comparison_facts",
            ResolverOutcome: "resolved", ContextMessageCount: 2, ContextCharacterCount: 80,
            ConversationHistoryTurns: 8, ConversationHistoryCharacters: 4000,
            ContextTurns: [new("user", "Kain top speed"), new("assistant", "Verified local data.")],
            ComparisonSubjectCount: 2, ComparisonAttributeCategory: "MaximumSpeed", ComparisonResultCategory: "resolved",
            ComparisonDiagnostics: new("Robot", [new("Kain", "conversation_context"), new("Tyrannos", "current_question")],
                "maximum speed", "resolved", [new("Maximum speed", ["103.32", "93.6"], "kph", "9.72", "Kain")])),
            SparkyApiProfile.Dev, captureContent: true, TestContext.Current.CancellationToken);
        var profileManager = new SparkyProfileManager(traceStore, new FakeBundleCatalog(bundle), options, bundle);
        await profileManager.InitializeAsync(TestContext.Current.CancellationToken);
        var tunnelStatuses = new SparkyTunnelStatusRegistry();
        tunnelStatuses.Register("game-api", enabled: false);
        tunnelStatuses.Register("admin-panel", enabled: false);
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, port));
        await using var app = builder.Build();
        using var authentication = new SparkyAdminAuthentication(AdminToken, DevToken, LiveToken);
        SparkyAdminEndpoints.Map(
            app, authentication, traceStore, options, bundle, tunnelStatuses, profileManager,
            new SparkyReplayService(traceStore, profileManager, new SparkyConcurrencyLimiter(), options));
        await app.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") };

            using var noToken = await client.GetAsync("admin/api/health", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);

            using var gameToken = await GetWithBearerAsync(client, "admin/api/health", DevToken);
            Assert.Equal(HttpStatusCode.Unauthorized, gameToken.StatusCode);

            using var liveToken = await GetWithBearerAsync(client, "admin/api/health", LiveToken);
            Assert.Equal(HttpStatusCode.Unauthorized, liveToken.StatusCode);

            using var adminToken = await GetWithBearerAsync(client, "admin/api/health", AdminToken);
            Assert.Equal(HttpStatusCode.OK, adminToken.StatusCode);
            string health = await adminToken.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("test-model", health, StringComparison.Ordinal);
            Assert.DoesNotContain(AdminToken, health, StringComparison.Ordinal);

            using var panel = await client.GetAsync("sparky-admin/", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, panel.StatusCode);
            string panelHtml = await panel.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("Sparky Admin", panelHtml, StringComparison.Ordinal);

            using var gameHealth = await GetWithBearerAsync(client, "health", AdminToken);
            Assert.Equal(HttpStatusCode.NotFound, gameHealth.StatusCode);

            using var gameAnswer = await GetWithBearerAsync(client, "v1/answer", AdminToken);
            Assert.Equal(HttpStatusCode.NotFound, gameAnswer.StatusCode);

            using var invalidFilter = await GetWithBearerAsync(client, "admin/api/traces?limit=1000", AdminToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);

            var activeConfiguration = (await profileManager.GetDetailsAsync(SparkyApiProfile.Dev, TestContext.Current.CancellationToken)).ActiveConfiguration!;
            using var directLiveDraft = await SendJsonWithBearerAsync(client, HttpMethod.Put, "admin/api/profiles/Live/draft", AdminToken,
                JsonSerializer.Serialize(new { configuration = activeConfiguration, reason = "bypass promotion" }));
            Assert.Equal(HttpStatusCode.Conflict, directLiveDraft.StatusCode);
            Assert.Contains("live_requires_promotion", await directLiveDraft.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
            using var directLiveActivation = await PostWithBearerAsync(client, "admin/api/profiles/Live/activate", AdminToken,
                JsonSerializer.Serialize(new { revisionId = Guid.NewGuid().ToString("N"), reason = "bypass promotion" }));
            Assert.Equal(HttpStatusCode.Conflict, directLiveActivation.StatusCode);

            var draftConfiguration = activeConfiguration with
            {
                PersonaStyle = "A concise, practical technician.",
                ModelId = "test-model-v2"
            };
            using var savedDraft = await SendJsonWithBearerAsync(client, HttpMethod.Put, "admin/api/profiles/Dev/draft", AdminToken,
                JsonSerializer.Serialize(new { configuration = draftConfiguration, reason = "panel test" }));
            Assert.Equal(HttpStatusCode.OK, savedDraft.StatusCode);
            using var savedDraftJson = JsonDocument.Parse(await savedDraft.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            string revisionId = savedDraftJson.RootElement.GetProperty("revisionId").GetString()!;

            using var validatedDraft = await PostWithBearerAsync(client, "admin/api/profiles/Dev/validate", AdminToken,
                JsonSerializer.Serialize(draftConfiguration));
            Assert.Equal(HttpStatusCode.OK, validatedDraft.StatusCode);
            using var validationJson = JsonDocument.Parse(await validatedDraft.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.True(validationJson.RootElement.GetProperty("valid").GetBoolean());

            using var activatedDraft = await PostWithBearerAsync(client, "admin/api/profiles/Dev/activate", AdminToken,
                JsonSerializer.Serialize(new { revisionId, reason = "panel test" }));
            Assert.Equal(HttpStatusCode.OK, activatedDraft.StatusCode);
            using var profileSummaries = await GetWithBearerAsync(client, "admin/api/profiles", AdminToken);
            Assert.Contains(revisionId, await profileSummaries.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
            using var updatedHealth = await GetWithBearerAsync(client, "admin/api/health", AdminToken);
            Assert.Contains("test-model-v2", await updatedHealth.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

            using var feedback = await PostWithBearerAsync(client,
                $"admin/api/traces/{traceRequestId}/feedback?environment=Dev", AdminToken,
                "{\"label\":\"wrong_route\",\"note\":\"Review the selected route\"}");
            Assert.Equal(HttpStatusCode.OK, feedback.StatusCode);

            using var evaluation = await PostWithBearerAsync(client,
                "admin/api/evaluations?environment=Dev", AdminToken,
                "{\"requestId\":\"" + traceRequestId + "\",\"expectedIntent\":\"MechanicsLore\",\"expectedProperties\":[\"entity:Theseus\"]}");
            Assert.Equal(HttpStatusCode.Created, evaluation.StatusCode);

            using var evaluations = await GetWithBearerAsync(client, "admin/api/evaluations?environment=Dev", AdminToken);
            Assert.Equal(HttpStatusCode.OK, evaluations.StatusCode);
            Assert.Contains("Who is Theseus?", await evaluations.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

            using var redactedExport = await GetWithBearerAsync(client,
                $"admin/api/traces/{traceRequestId}/export?environment=Dev", AdminToken);
            string redactedJson = await redactedExport.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, redactedExport.StatusCode);
            Assert.DoesNotContain("Who is Theseus?", redactedJson, StringComparison.Ordinal);
            Assert.DoesNotContain("maximum speed", redactedJson, StringComparison.Ordinal);
            Assert.Contains("contextMessageCount", redactedJson, StringComparison.Ordinal);
            Assert.Contains("effectiveIntent", redactedJson, StringComparison.Ordinal);
            Assert.Contains("\"contextMessageCount\":2", redactedJson, StringComparison.Ordinal);
            Assert.Contains("\"conversationHistoryTurns\":8", redactedJson, StringComparison.Ordinal);
            Assert.Contains("schemaVersion", redactedJson, StringComparison.Ordinal);
            using var contentExport = await GetWithBearerAsync(client,
                $"admin/api/traces/{traceRequestId}/export?environment=Dev&includeContent=true", AdminToken);
            string contentJson = await contentExport.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("Who is Theseus?", contentJson, StringComparison.Ordinal);
            Assert.Contains("maximum speed", contentJson, StringComparison.Ordinal);
            Assert.Contains("103.32", contentJson, StringComparison.Ordinal);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
            if (Directory.Exists(traceDirectory)) Directory.Delete(traceDirectory, recursive: true);
        }
    }

    private static async Task<HttpResponseMessage> GetWithBearerAsync(HttpClient client, string path, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> PostWithBearerAsync(HttpClient client, string path, string token, string json)
        => await SendJsonWithBearerAsync(client, HttpMethod.Post, path, token, json);

    private static async Task<HttpResponseMessage> SendJsonWithBearerAsync(HttpClient client, HttpMethod method, string path, string token, string json)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static SparkyKnowledgeBundle CreateEmptyBundle() => new(
        SparkyKnowledgeBundle.CurrentSchemaVersion,
        new SparkyBundleManifest(
            "test-bundle-version",
            "test-revision",
            DateTimeOffset.UtcNow,
            new Dictionary<int, string>(),
            new Dictionary<string, string> { ["test"] = "test" }),
        [],
        [],
        [],
        [],
        new SparkySearchIndex(new Dictionary<string, IReadOnlyList<int>>(), []));

    private sealed class FakeBundleCatalog(SparkyKnowledgeBundle bundle) : ISparkyBundleCatalog
    {
        public void RegisterExisting(string version, string path, SparkyKnowledgeBundle existing) { }
        public IReadOnlyList<SparkyBundleInfo> List() => [];
        public SparkyKnowledgeBundle GetRequired(string version) => bundle;
        public Task<SparkyBundleInfo> StageAsync(Stream input, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
