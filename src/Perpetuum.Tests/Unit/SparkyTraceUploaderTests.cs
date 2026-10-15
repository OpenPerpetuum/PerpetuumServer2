using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Perpetuum.Services.Sparky;
using Xunit;
using SparkyApiTraceEventRequest = Perpetuum.ItemStatsExporter.SparkyTraceEventRequest;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyTraceUploaderTests
{
    [Fact]
    public async Task Uploader_sends_bounded_authenticated_batches_and_removes_only_acknowledged_ids()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "spool.jsonl");
        await using var spool = new SparkyTraceSpool(path, 64 * 1024);
        await spool.InitializeAsync(cancellationToken);
        var first = Event(Guid.NewGuid(), Guid.NewGuid());
        var second = Event(Guid.NewGuid(), Guid.NewGuid());
        await spool.AppendAsync(first, cancellationToken);
        await spool.AppendAsync(second, cancellationToken);

        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var events = JsonSerializer.Deserialize<SparkyApiTraceEventRequest[]>(
                capturedBody,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(events);
            Assert.InRange(events!.Length, 1, 100);
            byte[] response = JsonSerializer.SerializeToUtf8Bytes(new
            {
                error = "event_id_conflict",
                acknowledgedEventIds = new[] { first.EventId },
                conflictingEventIds = new[] { second.EventId }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return JsonResponse(HttpStatusCode.Conflict, response);
        });
        using var client = new HttpClient(handler);
        var uploader = new SparkyTraceUploader(
            spool,
            new Uri("https://127.0.0.1:5080/v1/trace-events"),
            "test-token-for-sparky-trace-upload-0123456789",
            client);

        bool uploadSucceeded = await uploader.UploadOnceAsync(cancellationToken);

        Assert.False(uploadSucceeded); // HTTP 409 acknowledged only the first event.
        Assert.NotNull(capturedRequest);
        Assert.DoesNotContain("isValid", capturedBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("https://127.0.0.1:5080/v1/trace-events", capturedRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-token-for-sparky-trace-upload-0123456789", capturedRequest.Headers.Authorization.Parameter);
        Assert.Equal([second], await spool.ReadPendingAsync(10, cancellationToken));
    }

    [Fact]
    public async Task Failed_upload_keeps_events_for_retry_after_uploader_restart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        string path = Path.Combine(temp.Path, "spool.jsonl");
        var traceEvent = Event(Guid.NewGuid(), Guid.NewGuid());

        await using (var spool = new SparkyTraceSpool(path, 64 * 1024))
        {
            await spool.InitializeAsync(cancellationToken);
            await spool.AppendAsync(traceEvent, cancellationToken);
            using var failedClient = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
            var failedUploader = new SparkyTraceUploader(
                spool,
                new Uri("https://127.0.0.1:5080/v1/trace-events"),
                "test-token-for-sparky-trace-upload-0123456789",
                failedClient);

            Assert.False(await failedUploader.UploadOnceAsync(cancellationToken));
            Assert.Equal([traceEvent], await spool.ReadPendingAsync(10, cancellationToken));
        }

        await using var reopened = new SparkyTraceSpool(path, 64 * 1024);
        await reopened.InitializeAsync(cancellationToken);
        using var successClient = new HttpClient(new StubHandler(async request =>
        {
            var events = await request.Content!.ReadFromJsonAsync<SparkyTraceEvent[]>(cancellationToken: cancellationToken);
            byte[] response = JsonSerializer.SerializeToUtf8Bytes(new
            {
                acknowledgedEventIds = events!.Select(trace => trace.EventId).ToArray()
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return JsonResponse(HttpStatusCode.OK, response);
        }));
        var retryingUploader = new SparkyTraceUploader(
            reopened,
            new Uri("https://127.0.0.1:5080/v1/trace-events"),
            "test-token-for-sparky-trace-upload-0123456789",
            successClient);

        Assert.True(await retryingUploader.UploadOnceAsync(cancellationToken));
        Assert.Empty(await reopened.ReadPendingAsync(10, cancellationToken));
    }

    [Fact]
    public async Task Uploader_does_not_delete_an_event_that_was_not_in_its_acknowledgement()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        await using var spool = new SparkyTraceSpool(Path.Combine(temp.Path, "spool.jsonl"), 64 * 1024);
        await spool.InitializeAsync(cancellationToken);
        var traceEvent = Event(Guid.NewGuid(), Guid.NewGuid());
        await spool.AppendAsync(traceEvent, cancellationToken);
        using var client = new HttpClient(new StubHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            JsonSerializer.SerializeToUtf8Bytes(new { acknowledgedEventIds = Array.Empty<Guid>() }, new JsonSerializerOptions(JsonSerializerDefaults.Web))))));
        var uploader = new SparkyTraceUploader(
            spool,
            new Uri("https://127.0.0.1:5080/v1/trace-events"),
            "test-token-for-sparky-trace-upload-0123456789",
            client);

        Assert.False(await uploader.UploadOnceAsync(cancellationToken));
        Assert.Equal([traceEvent], await spool.ReadPendingAsync(10, cancellationToken));
    }

    [Fact]
    public async Task Background_worker_spools_and_uploads_without_blocking_the_caller()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        await using var spool = new SparkyTraceSpool(Path.Combine(temp.Path, "spool.jsonl"), 64 * 1024);
        var traceEvent = Event(Guid.NewGuid(), Guid.NewGuid());
        var uploadSeen = new TaskCompletionSource<Guid[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new StubHandler(async request =>
        {
            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var events = JsonSerializer.Deserialize<SparkyApiTraceEventRequest[]>(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            uploadSeen.TrySetResult(events!.Select(trace => trace.EventId).ToArray());
            byte[] response = JsonSerializer.SerializeToUtf8Bytes(new
            {
                acknowledgedEventIds = events.Select(trace => trace.EventId).ToArray()
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return JsonResponse(HttpStatusCode.OK, response);
        }));
        using var uploader = new SparkyTraceUploader(
            spool,
            new Uri("https://127.0.0.1:5080/v1/trace-events"),
            "test-token-for-sparky-trace-upload-0123456789",
            client);
        var options = new SparkyClientOptions(
            new Uri("https://127.0.0.1:5080/v1/answer"),
            "test-token-for-sparky-trace-upload-0123456789",
            "dev",
            UsesLegacySettings: false,
            RequestTimeout: TimeSpan.FromSeconds(90),
            MaxQuestionCharacters: 1000,
            MaxAnswerCharacters: 2000,
            QueueCapacity: 64,
            WorkerCount: 1,
            PendingPerCallerLimit: 1,
            ContextLifetime: TimeSpan.FromMinutes(15),
            ContextMaxTurns: 8,
            ContextMaxCharacters: 6000,
            ContextCapacity: 100,
            SenderCharacterNick: "Sparky");
        uploader.Start(options);

        Assert.True(uploader.TryRecord(traceEvent));
        Assert.Equal([traceEvent.EventId], await uploadSeen.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken));

        for (int attempt = 0; attempt < 100; attempt++)
        {
            if ((await spool.ReadPendingAsync(10, cancellationToken)).Count == 0)
            {
                break;
            }

            await Task.Delay(20, cancellationToken);
        }

        await uploader.StopAsync().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.Empty(await spool.ReadPendingAsync(10, cancellationToken));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, byte[] body) => new(status)
    {
        Content = new ByteArrayContent(body) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") } }
    };

    private static SparkyTraceEvent Event(Guid eventId, Guid requestId) => new(
        eventId,
        requestId,
        "Discord",
        "delivery",
        "sent",
        DateTimeOffset.UtcNow,
        DetailCode: "reply_sent");

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "sparky-game-trace-uploader-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
