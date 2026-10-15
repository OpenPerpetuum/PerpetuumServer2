using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Perpetuum.Services.Sparky;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyLiveKnowledgeClientTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Current_and_finalization_use_existing_authority_bearer_and_matching_acknowledgements()
    {
        var current = Current();
        var final = Finalization();
        var requests = new List<(string Method, string Uri, AuthenticationHeaderValue? Authorization, string Body)>();
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            requests.Add((request.Method.Method, request.RequestUri!.ToString(), request.Headers.Authorization,
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            string operation = JsonDocument.Parse(requests[^1].Body).RootElement.GetProperty("operation").GetString()!;
            string hash = JsonDocument.Parse(requests[^1].Body).RootElement.GetProperty("contentHash").GetString()!;
            return Ack(operation, hash);
        });

        var currentResult = await client.PublishCurrentAsync(current, TestContext.Current.CancellationToken);
        var finalResult = await client.PublishFinalizationAsync(final, TestContext.Current.CancellationToken);

        Assert.True(currentResult.IsAcknowledged);
        Assert.True(finalResult.IsAcknowledged);
        Assert.Equal("https://127.0.0.1:5080/v1/live-knowledge/current", requests[0].Uri);
        Assert.Equal("https://127.0.0.1:5080/v1/live-knowledge/seasons/7/finalize", requests[1].Uri);
        Assert.Equal("Bearer", requests[0].Authorization!.Scheme);
        Assert.Equal("test-api-token", requests[0].Authorization.Parameter);
        Assert.Equal("test-api-token", requests[1].Authorization!.Parameter);
        Assert.Contains("Autumn Circuit", requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_uses_metadata_route_and_recovers_when_ai_reports_missing_current()
    {
        var current = Current();
        var refresh = current with
        {
            Operation = "refresh-current",
            Current = null,
            ContentHash = current.ContentHash,
            CapturedAtUtc = current.CapturedAtUtc.AddMinutes(5)
        };
        string? requestUri = null;
        using var client = CreateClient((request, _) =>
        {
            requestUri = request.RequestUri!.ToString();
            return Task.FromResult(Ack("refresh-current", current.ContentHash, HttpStatusCode.Conflict, "current_missing"));
        });

        var result = await client.RefreshCurrentAsync(refresh, TestContext.Current.CancellationToken);

        Assert.False(result.IsAcknowledged);
        Assert.Equal("current_missing", result.Status);
        Assert.Equal("https://127.0.0.1:5080/v1/live-knowledge/current/refresh", requestUri);
    }

    [Fact]
    public async Task Heartbeat_missing_current_resends_the_full_snapshot_and_accepts_matching_ack()
    {
        var current = Current();
        var operations = new List<string>();
        using var client = CreateClient(async (request, cancellationToken) =>
        {
            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            string operation = JsonDocument.Parse(body).RootElement.GetProperty("operation").GetString()!;
            operations.Add(operation);
            return operation == "refresh-current"
                ? Ack(operation, current.ContentHash, HttpStatusCode.Conflict, "current_missing")
                : Ack(operation, current.ContentHash);
        });

        var result = await client.PublishCurrentWithHeartbeatAsync(
            current, contentHashWasPreviouslyAcknowledged: true, TestContext.Current.CancellationToken);

        Assert.True(result.IsAcknowledged);
        Assert.True(result.FullSnapshotSent);
        Assert.Equal(["refresh-current", "replace-current"], operations);
    }

    [Fact]
    public async Task Wrong_acknowledgement_hash_or_operation_is_not_accepted()
    {
        var update = Current();
        using var client = CreateClient((_, _) => Task.FromResult(Ack("finalize-season", new string('b', 64))));

        var result = await client.PublishCurrentAsync(update, TestContext.Current.CancellationToken);

        Assert.False(result.IsAcknowledged);
        Assert.Equal("invalid_acknowledgement", result.Status);
    }

    [Fact]
    public async Task Oversized_payload_is_rejected_before_http_and_cancellation_is_propagated()
    {
        bool requestSent = false;
        using var client = CreateClient((_, _) =>
        {
            requestSent = true;
            return Task.FromResult(Ack("replace-current", new string('a', 64)));
        });
        string longDescription = new('x', 2000);
        var oversized = Current() with
        {
            Current = new SparkySeasonCurrentState(
                new SparkySeasonFacts(1, "Season", longDescription, CapturedAt, CapturedAt.AddDays(1), "ActivityAndGlobal",
                    [], Enumerable.Range(0, 128).Select(index => new SparkySeasonObjective(
                        $"Objective {index}", longDescription, "NpcKill", 1, 1, false)).ToArray(), []), []),
            ContentHash = new string('a', 64)
        };
        oversized = oversized with { ContentHash = SparkyLiveKnowledgeHash.ComputeCurrent(oversized) };

        var oversizedResult = await client.PublishCurrentAsync(oversized, TestContext.Current.CancellationToken);
        Assert.False(oversizedResult.IsAcknowledged);
        Assert.Equal("payload_too_large", oversizedResult.Status);
        Assert.False(requestSent);

        using var cancelClient = CreateClient((_, cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith<HttpResponseMessage>(
            _ => new HttpResponseMessage(HttpStatusCode.OK), cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelClient.PublishCurrentAsync(Current(), cancellation.Token));
    }

    private static SparkyApiClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    {
        var options = new SparkyClientOptions(
            new Uri("https://127.0.0.1:5080/v1/answer"), "test-api-token", "dev", false,
            TimeSpan.FromSeconds(5), 2000, 2000, 8, 1, 2, TimeSpan.FromMinutes(15), 8, 4000, 100,
            "[OPP] Sparky - The Syndicate Welcome Agent");
        return new SparkyApiClient(options, new HttpClient(new Handler(send)));
    }

    private static HttpResponseMessage Ack(
        string operation,
        string hash,
        HttpStatusCode status = HttpStatusCode.OK,
        string? acknowledgementStatus = null) =>
        new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                status = acknowledgementStatus ?? (status == HttpStatusCode.Conflict
                    ? "current_missing"
                    : operation switch
                    {
                        "replace-current" => "applied",
                        "refresh-current" => "refreshed",
                        "finalize-season" => "finalized",
                        _ => "unknown"
                    }),
                contentHash = hash,
                operation,
                revision = 1
            }), Encoding.UTF8, "application/json")
        };

    private static SparkyLiveKnowledgeUpdate Current()
    {
        var update = new SparkyLiveKnowledgeUpdate(1, "seasons", "dev", "replace-current", CapturedAt,
            new string('a', 64), new SparkySeasonCurrentState(
                new SparkySeasonFacts(8, "Autumn Circuit", "Public season", CapturedAt.AddDays(-1), CapturedAt.AddDays(5),
                    "ActivityAndGlobal", [], [], []), []), null);
        return update with { ContentHash = SparkyLiveKnowledgeHash.ComputeCurrent(update) };
    }

    private static SparkyLiveKnowledgeUpdate Finalization()
    {
        var update = new SparkyLiveKnowledgeUpdate(1, "seasons", "dev", "finalize-season", CapturedAt,
            new string('a', 64), null,
            new SparkySeasonFinalization(7, "Winter Circuit", CapturedAt.AddDays(-1),
                [new SparkySeasonStanding(1, "Agent One", 100)]));
        return update with { ContentHash = SparkyLiveKnowledgeHash.ComputeFinalization(update) };
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
