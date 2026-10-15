using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Perpetuum.Services.Sparky;

internal sealed class SparkyApiClient : IDisposable
{
    private const int MaxResponseBytes = 65536;
    private const int MaxLiveKnowledgeRequestBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly HttpClient _httpClient;
    private readonly SparkyClientOptions _options;

    public SparkyApiClient(SparkyClientOptions options)
        : this(options, new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        })
    {
    }

    internal SparkyApiClient(SparkyClientOptions options, HttpClient httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<SparkyApiResult> AnswerAsync(
        Guid traceId,
        string question,
        string? contextItem,
        int? contextLanguageId,
        CancellationToken cancellationToken,
        SparkyContextEntry? context = null,
        SparkyCallerRank callerRank = SparkyCallerRank.Agent)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.RequestTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUri)
        {
            Content = JsonContent.Create(new SparkyApiRequest(
                traceId,
                question,
                contextItem,
                contextLanguageId,
                context?.Turns,
                context?.Summary,
                context?.PendingClarification,
                callerRank), options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token).ConfigureAwait(false);

        var responseBytes = await ReadBoundedAsync(response.Content, timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return SparkyApiResult.Unavailable;
        }

        var parsed = JsonSerializer.Deserialize<SparkyApiResponse>(responseBytes, JsonOptions);
        if (parsed is null)
        {
            return SparkyApiResult.Unavailable;
        }

        var contextUpdate = ValidateContextUpdate(parsed.Status, parsed.Answer, parsed.Candidates, parsed.ContextUpdate);

        return parsed.Status switch
        {
            "answered" when !string.IsNullOrWhiteSpace(parsed.Answer) => new SparkyApiResult(
                SparkyResultKind.Answered,
                parsed.Answer,
                Limit(parsed.Item, 256),
                [],
                parsed.LanguageId,
                contextUpdate),
            "not_found" => new SparkyApiResult(
                SparkyResultKind.NotFound,
                Limit(parsed.Answer, 2000),
                null,
                [],
                parsed.LanguageId,
                contextUpdate),
            "ambiguous" => new SparkyApiResult(
                SparkyResultKind.Ambiguous,
                Limit(parsed.Answer, 2000),
                null,
                (parsed.Candidates ?? []).Take(10).Select(candidate => Limit(candidate, 256)!).ToArray(),
                parsed.LanguageId,
                contextUpdate),
            "busy" => new SparkyApiResult(
                SparkyResultKind.Busy,
                Limit(parsed.Answer, 2000),
                null,
                [],
                parsed.LanguageId,
                contextUpdate),
            _ => SparkyApiResult.Unavailable
        };
    }

    public async Task<SparkyLiveKnowledgePublishResult> PublishCurrentAsync(
        SparkyLiveKnowledgeUpdate update,
        CancellationToken cancellationToken) =>
        (await PublishLiveKnowledgeAsync(update, "replace-current", ["applied", "refreshed", "unchanged"], cancellationToken)
            .ConfigureAwait(false)) with { FullSnapshotSent = true };

    public Task<SparkyLiveKnowledgePublishResult> RefreshCurrentAsync(
        SparkyLiveKnowledgeUpdate update,
        CancellationToken cancellationToken) =>
        PublishLiveKnowledgeAsync(update, "refresh-current", ["refreshed", "unchanged"], cancellationToken);

    public async Task<SparkyLiveKnowledgePublishResult> PublishCurrentWithHeartbeatAsync(
        SparkyLiveKnowledgeUpdate update,
        bool contentHashWasPreviouslyAcknowledged,
        CancellationToken cancellationToken)
    {
        if (!contentHashWasPreviouslyAcknowledged)
            return await PublishCurrentAsync(update, cancellationToken).ConfigureAwait(false);

        var heartbeat = await RefreshCurrentAsync(update with
        {
            Operation = "refresh-current",
            Current = null
        }, cancellationToken).ConfigureAwait(false);
        if (heartbeat.Status is "current_missing" or "hash_mismatch")
            return await PublishCurrentAsync(update, cancellationToken).ConfigureAwait(false);
        return heartbeat with { FullSnapshotSent = false };
    }

    public Task<SparkyLiveKnowledgePublishResult> PublishFinalizationAsync(
        SparkyLiveKnowledgeUpdate update,
        CancellationToken cancellationToken) =>
        PublishLiveKnowledgeAsync(update, "finalize-season", ["finalized", "duplicate"], cancellationToken);

    private async Task<SparkyLiveKnowledgePublishResult> PublishLiveKnowledgeAsync(
        SparkyLiveKnowledgeUpdate update,
        string expectedOperation,
        string[] acceptedStatuses,
        CancellationToken cancellationToken)
    {
        if (!IsValidLiveUpdate(update, expectedOperation))
            return new SparkyLiveKnowledgePublishResult(false, "invalid_update", 0);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(update, JsonOptions);
        if (body.Length > MaxLiveKnowledgeRequestBytes)
            return new SparkyLiveKnowledgePublishResult(false, "payload_too_large", 0);

        string route = expectedOperation switch
        {
            "replace-current" => "/v1/live-knowledge/current",
            "refresh-current" => "/v1/live-knowledge/current/refresh",
            "finalize-season" => $"/v1/live-knowledge/seasons/{update.Finalization!.SeasonId}/finalize",
            _ => throw new InvalidOperationException("Unsupported Sparky live-knowledge operation.")
        };
        var authority = new Uri(_options.ApiUri.GetLeftPart(UriPartial.Authority));
        var uri = new Uri(authority, route);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.RequestTimeout);
        using var request = new HttpRequestMessage(
            expectedOperation == "replace-current" ? HttpMethod.Put : HttpMethod.Post,
            uri)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token).ConfigureAwait(false);
        byte[] responseBytes = await ReadBoundedAsync(response.Content, timeout.Token).ConfigureAwait(false);
        SparkyLiveKnowledgeAcknowledgement? acknowledgement;
        try
        {
            acknowledgement = JsonSerializer.Deserialize<SparkyLiveKnowledgeAcknowledgement>(responseBytes, JsonOptions);
        }
        catch (JsonException)
        {
            acknowledgement = null;
        }

        if (acknowledgement is null ||
            !string.Equals(acknowledgement.Operation, expectedOperation, StringComparison.Ordinal) ||
            !string.Equals(acknowledgement.ContentHash, update.ContentHash, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(acknowledgement.Status))
        {
            return new SparkyLiveKnowledgePublishResult(false, "invalid_acknowledgement", (int)response.StatusCode);
        }

        bool accepted = response.IsSuccessStatusCode && acceptedStatuses.Contains(acknowledgement.Status, StringComparer.Ordinal);
        return new SparkyLiveKnowledgePublishResult(accepted, acknowledgement.Status, (int)response.StatusCode);
    }

    private bool IsValidLiveUpdate(SparkyLiveKnowledgeUpdate? update, string expectedOperation)
    {
        if (update is null || update.SchemaVersion != 1 || update.Provider != "seasons" ||
            update.Profile != _options.Profile || update.Operation != expectedOperation ||
            update.CapturedAtUtc.Offset != TimeSpan.Zero || string.IsNullOrEmpty(update.ContentHash) || update.ContentHash.Length != 64 ||
            !update.ContentHash.All(Uri.IsHexDigit))
        {
            return false;
        }

        return expectedOperation switch
        {
            "replace-current" => update.Current is not null && update.Finalization is null &&
                string.Equals(update.ContentHash, SparkyLiveKnowledgeHash.ComputeCurrent(update), StringComparison.OrdinalIgnoreCase),
            "refresh-current" => update.Current is null && update.Finalization is null,
            "finalize-season" => update.Current is null && update.Finalization is { SeasonId: > 0 } finalization &&
                string.Equals(update.ContentHash, SparkyLiveKnowledgeHash.ComputeFinalization(update), StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    internal static SparkyConversationUpdate? ValidateContextUpdate(
        string? status,
        string? answer,
        IReadOnlyList<string>? responseCandidates,
        SparkyConversationUpdate? update)
    {
        if (update?.PendingClarification is not { } pending)
        {
            return update;
        }

        if (status is not ("answered" or "not_found" or "ambiguous") ||
            string.IsNullOrWhiteSpace(answer) ||
            responseCandidates is null ||
            pending.CandidateNames is null ||
            pending.CandidateNames.Count is < 1 or > SparkyContextStore.MaximumCandidates)
        {
            return update with { PendingClarification = null };
        }

        var offered = responseCandidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => Limit(candidate.Trim(), SparkyContextStore.MaximumCandidateCharacters)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pendingNames = pending.CandidateNames
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => Limit(candidate.Trim(), SparkyContextStore.MaximumCandidateCharacters)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        bool choicesWereOffered = pendingNames.Length == pending.CandidateNames.Count &&
                                  offered.Count == pendingNames.Length &&
                                  pendingNames.All(candidate =>
                                      offered.Contains(candidate) &&
                                      answer.Contains(candidate, StringComparison.OrdinalIgnoreCase));
        return choicesWereOffered ? update : update with { PendingClarification = null };
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaxResponseBytes)
        {
            throw new InvalidDataException("Sparky response exceeded the configured response bound.");
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var count = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + count > MaxResponseBytes)
            {
                throw new InvalidDataException("Sparky response exceeded the configured response bound.");
            }

            buffer.Write(chunk, 0, count);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private static string? Limit(string? value, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed class SparkyApiResponse
    {
        public string? Status { get; init; }
        public string? Item { get; init; }
        public string? Answer { get; init; }
        public string[]? Candidates { get; init; }
        public int? LanguageId { get; init; }
        public SparkyConversationUpdate? ContextUpdate { get; init; }
    }

    private sealed class SparkyLiveKnowledgeAcknowledgement
    {
        public string? Status { get; init; }
        public string? ContentHash { get; init; }
        public string? Operation { get; init; }
        public int? Revision { get; init; }
    }
}

internal sealed record SparkyLiveKnowledgePublishResult(
    bool IsAcknowledged,
    string Status,
    int HttpStatusCode,
    bool FullSnapshotSent = false);

internal sealed record SparkyApiRequest(
    Guid RequestId,
    string Question,
    string? ContextItem = null,
    int? ContextLanguageId = null,
    IReadOnlyList<SparkyConversationTurn>? ContextTurns = null,
    string? ContextSummary = null,
    SparkyPendingClarification? PendingClarification = null,
    SparkyCallerRank CallerRank = SparkyCallerRank.Agent);

internal enum SparkyResultKind
{
    Answered,
    NotFound,
    Ambiguous,
    Busy,
    Unavailable,
    Usage,
    QuestionTooLong
}

internal sealed record SparkyApiResult(
    SparkyResultKind Kind,
    string? Answer,
    string? ItemDisplayName,
    IReadOnlyList<string> Candidates,
    int? LanguageId = null,
    SparkyConversationUpdate? ContextUpdate = null)
{
    public static SparkyApiResult Unavailable { get; } = new(SparkyResultKind.Unavailable, null, null, []);
}
