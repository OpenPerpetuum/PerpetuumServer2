using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Perpetuum.Services.Sparky;

internal sealed class SparkyApiClient : IDisposable
{
    private const int MaxResponseBytes = 65536;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly SparkyClientOptions _options;

    public SparkyApiClient(SparkyClientOptions options)
    {
        _options = options;
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async Task<SparkyApiResult> AnswerAsync(
        string question,
        string? contextItem,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.RequestTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUri)
        {
            Content = JsonContent.Create(new SparkyApiRequest(question, contextItem), options: JsonOptions)
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

        return parsed.Status switch
        {
            "answered" when !string.IsNullOrWhiteSpace(parsed.Answer) => new SparkyApiResult(
                SparkyResultKind.Answered,
                parsed.Answer,
                Limit(parsed.Item, 256),
                []),
            "not_found" => new SparkyApiResult(SparkyResultKind.NotFound, null, null, []),
            "ambiguous" => new SparkyApiResult(
                SparkyResultKind.Ambiguous,
                null,
                null,
                (parsed.Candidates ?? []).Take(10).Select(candidate => Limit(candidate, 256)!).ToArray()),
            _ => SparkyApiResult.Unavailable
        };
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

    private static string? Limit(string? value, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed record SparkyApiRequest(string Question, string? ContextItem);
    private sealed class SparkyApiResponse
    {
        public string? Status { get; init; }
        public string? Item { get; init; }
        public string? Answer { get; init; }
        public string[]? Candidates { get; init; }
    }
}

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
    IReadOnlyList<string> Candidates)
{
    public static SparkyApiResult Unavailable { get; } = new(SparkyResultKind.Unavailable, null, null, []);
}
