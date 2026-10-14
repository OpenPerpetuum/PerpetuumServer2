using System.Text;
using System.Text.Json;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyTraceIngestionTests
{
    private const string DevToken = "dev-token-for-sparky-tests-0123456789";
    private const string LiveToken = "live-token-for-sparky-tests-0123456789";

    [Fact]
    public async Task Unauthorized_or_malformed_uploads_do_not_create_trace_rows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var resolver = new SparkyApiCredentialResolver(DevToken, LiveToken);
        var traceEvent = Event("10000000-0000-0000-0000-000000000009", "20000000-0000-0000-0000-000000000009");
        byte[] validBody = Serialize([traceEvent]);

        var unauthorized = await HandleAsync("Bearer wrong-token-long-enough-to-test-000", validBody, resolver, store, cancellationToken);
        var unknownEnvironment = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(validBody).Replace(
            "\"source\":\"Discord\"",
            "\"source\":\"Discord\",\"environment\":\"Live\"",
            StringComparison.Ordinal));
        var invalid = await HandleAsync($"Bearer {DevToken}", unknownEnvironment, resolver, store, cancellationToken);

        Assert.Equal(401, unauthorized.StatusCode);
        Assert.Equal(400, invalid.StatusCode);
        Assert.Null(await store.GetTraceAsync(traceEvent.RequestId, SparkyApiProfile.Dev, cancellationToken));
        Assert.Null(await store.GetTraceAsync(traceEvent.RequestId, SparkyApiProfile.Live, cancellationToken));
    }

    [Fact]
    public async Task Authenticated_profile_selects_storage_environment_and_retries_are_idempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var resolver = new SparkyApiCredentialResolver(DevToken, LiveToken);
        var traceEvent = Event("10000000-0000-0000-0000-000000000010", "20000000-0000-0000-0000-000000000010");
        byte[] body = Serialize([traceEvent]);

        var dev = await HandleAsync($"Bearer {DevToken}", body, resolver, store, cancellationToken);
        var live = await HandleAsync($"Bearer {LiveToken}", body, resolver, store, cancellationToken);
        var retry = await HandleAsync($"Bearer {DevToken}", body, resolver, store, cancellationToken);

        Assert.Equal(200, dev.StatusCode);
        Assert.Equal(new[] { traceEvent.EventId }, dev.AcknowledgedEventIds);
        Assert.Equal(200, live.StatusCode);
        Assert.Equal(200, retry.StatusCode);
        Assert.Equal(new[] { traceEvent.EventId }, retry.AcknowledgedEventIds);
        Assert.Single((await store.GetTraceAsync(traceEvent.RequestId, SparkyApiProfile.Dev, cancellationToken))!.Events);
        Assert.Single((await store.GetTraceAsync(traceEvent.RequestId, SparkyApiProfile.Live, cancellationToken))!.Events);
    }

    [Fact]
    public async Task Oversized_upload_is_rejected_before_store_write()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var resolver = new SparkyApiCredentialResolver(DevToken, LiveToken);
        using var body = new MemoryStream();

        var result = await SparkyTraceIngestion.HandleAsync(
            $"Bearer {DevToken}",
            "application/json",
            SparkyTraceEventValidator.MaxBodyBytes + 1,
            body,
            resolver,
            store,
            cancellationToken);

        Assert.Equal(413, result.StatusCode);
        Assert.Null(result.AcknowledgedEventIds);
    }

    [Fact]
    public async Task Missing_trace_store_returns_a_redacted_retryable_error()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var resolver = new SparkyApiCredentialResolver(DevToken, LiveToken);
        byte[] body = Serialize([Event("10000000-0000-0000-0000-000000000011", "20000000-0000-0000-0000-000000000011")]);

        var result = await HandleAsync($"Bearer {DevToken}", body, resolver, store: null, cancellationToken);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("trace_storage_unavailable", result.Error);
        Assert.DoesNotContain("SQLite", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static Task<SparkyTraceIngestionResult> HandleAsync(
        string authorization,
        byte[] body,
        SparkyApiCredentialResolver resolver,
        SparkyTraceStore? store,
        CancellationToken cancellationToken) =>
        SparkyTraceIngestion.HandleAsync(
            authorization,
            "application/json; charset=utf-8",
            body.Length,
            new MemoryStream(body),
            resolver,
            store,
            cancellationToken);

    private static byte[] Serialize(SparkyTraceEventRequest[] events) =>
        JsonSerializer.SerializeToUtf8Bytes(events, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static SparkyTraceEventRequest Event(string eventId, string requestId) => new(
        Guid.Parse(eventId),
        Guid.Parse(requestId),
        "Discord",
        "trigger",
        "recognized",
        DateTimeOffset.UtcNow,
        DetailCode: "mention");

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "sparky-trace-ingestion-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
