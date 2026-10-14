using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Perpetuum.Log;

namespace Perpetuum.Services.Sparky;

public sealed class SparkyTraceUploader : IDisposable
{
    private const int MaximumBatchCount = 100;
    private const int MaximumBodyBytes = 64 * 1024;
    private const int InputQueueCapacity = 2048;
    private const int MaximumResponseBytes = 64 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 4,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly SparkyTraceSpool _spool;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private Channel<SparkyTraceEvent>? _input;
    private CancellationTokenSource? _shutdown;
    private Task? _runTask;
    private Uri? _endpoint;
    private string? _token;
    private int _accepting;
    private int _droppedSourceMask;
    private int _overflowWarningWritten;

    public SparkyTraceUploader(SparkyTraceSpool spool)
        : this(spool, CreateHttpClient(), ownsHttpClient: true)
    {
    }

    internal SparkyTraceUploader(SparkyTraceSpool spool, Uri endpoint, string token, HttpClient httpClient)
        : this(spool, httpClient, ownsHttpClient: false)
    {
        ConfigureEndpoint(endpoint, token);
        _input = CreateInputChannel();
        Volatile.Write(ref _accepting, 1);
    }

    private SparkyTraceUploader(SparkyTraceSpool spool, HttpClient httpClient, bool ownsHttpClient)
    {
        _spool = spool ?? throw new ArgumentNullException(nameof(spool));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = ownsHttpClient;
    }

    public Task Completion => _runTask ?? Task.CompletedTask;

    internal void Start(SparkyClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_runTask is { IsCompleted: false })
        {
            throw new InvalidOperationException("The Sparky trace uploader is already running.");
        }

        _shutdown?.Dispose();
        _shutdown = new CancellationTokenSource();
        _endpoint = new UriBuilder(options.ApiUri) { Path = "/v1/trace-events", Query = string.Empty, Fragment = string.Empty }.Uri;
        _token = options.ApiToken;
        _input = CreateInputChannel();
        Volatile.Write(ref _droppedSourceMask, 0);
        Volatile.Write(ref _overflowWarningWritten, 0);
        Volatile.Write(ref _accepting, 1);
        _runTask = RunAsync(_input.Reader, _shutdown.Token);
    }

    internal bool TryRecord(SparkyTraceEvent traceEvent)
    {
        if (Volatile.Read(ref _accepting) != 1 || traceEvent is null || !traceEvent.IsValid || _input is null)
        {
            return false;
        }

        if (_input.Writer.TryWrite(traceEvent))
        {
            return true;
        }

        int sourceBit = traceEvent.Source switch
        {
            "Game" => 1,
            "Discord" => 2,
            _ => 0
        };
        if (sourceBit != 0)
        {
            Interlocked.Or(ref _droppedSourceMask, sourceBit);
        }

        if (Interlocked.Exchange(ref _overflowWarningWritten, 1) == 0)
        {
            Logger.Warning("[Sparky trace] The bounded metadata queue is full; some trace events were dropped.");
        }

        return false;
    }

    public Task StopAsync()
    {
        Volatile.Write(ref _accepting, 0);
        _input?.Writer.TryComplete();
        _shutdown?.Cancel();
        return _runTask ?? Task.CompletedTask;
    }

    internal async Task<bool> UploadOnceAsync(CancellationToken cancellationToken)
    {
        if (_endpoint is null || string.IsNullOrWhiteSpace(_token))
        {
            return false;
        }

        var events = await _spool.ReadPendingAsync(MaximumBatchCount, cancellationToken).ConfigureAwait(false);
        if (events.Count == 0)
        {
            return true;
        }

        byte[] body;
        int count = events.Count;
        while (true)
        {
            body = JsonSerializer.SerializeToUtf8Bytes(events.Take(count).ToArray(), JsonOptions);
            if (body.Length <= MaximumBodyBytes)
            {
                break;
            }

            if (count == 1)
            {
                throw new InvalidDataException("A Sparky trace event exceeds the upload body limit.");
            }

            count--;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            if (response.StatusCode is not HttpStatusCode.OK and not HttpStatusCode.Conflict)
            {
                return false;
            }

            byte[] responseBody = await ReadBoundedResponseAsync(response, timeout.Token).ConfigureAwait(false);
            UploadAcknowledgement? acknowledgement;
            try
            {
                acknowledgement = JsonSerializer.Deserialize<UploadAcknowledgement>(responseBody, JsonOptions);
            }
            catch (JsonException)
            {
                return false;
            }

            if (acknowledgement?.AcknowledgedEventIds is null)
            {
                return false;
            }

            var batchIds = events.Take(count).Select(traceEvent => traceEvent.EventId).ToHashSet();
            Guid[] acknowledgedIds = acknowledgement.AcknowledgedEventIds
                .Where(batchIds.Contains)
                .Distinct()
                .ToArray();
            await _spool.AcknowledgeAsync(acknowledgedIds, cancellationToken).ConfigureAwait(false);
            return acknowledgedIds.Length == batchIds.Count;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task RunAsync(ChannelReader<SparkyTraceEvent> reader, CancellationToken cancellationToken)
    {
        try
        {
            await _spool.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidDataException or System.Security.SecurityException or ArgumentException)
        {
            Volatile.Write(ref _accepting, 0);
            _input?.Writer.TryComplete();
            Logger.Error($"[Sparky trace] Local spool initialization failed ({exception.GetType().Name}); trace capture is disabled.");
            return;
        }

        Task writer = PersistEventsAsync(reader);
        Task sender = UploadLoopAsync(cancellationToken);
        await Task.WhenAll(writer, sender).ConfigureAwait(false);
    }

    private async Task PersistEventsAsync(ChannelReader<SparkyTraceEvent> reader)
    {
        try
        {
            await foreach (var traceEvent in reader.ReadAllAsync().ConfigureAwait(false))
            {
                await _spool.AppendAsync(traceEvent, CancellationToken.None).ConfigureAwait(false);
                await WriteQueueOverflowMarkersAsync().ConfigureAwait(false);
            }

            await WriteQueueOverflowMarkersAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidDataException or System.Security.SecurityException or ArgumentException)
        {
            Volatile.Write(ref _accepting, 0);
            _input?.Writer.TryComplete();
            _shutdown?.Cancel();
            Logger.Error($"[Sparky trace] Local spool write failed ({exception.GetType().Name}); trace capture is disabled.");
        }
        catch (Exception exception)
        {
            Volatile.Write(ref _accepting, 0);
            _input?.Writer.TryComplete();
            _shutdown?.Cancel();
            Logger.Error($"[Sparky trace] Trace writer failed ({exception.GetType().Name}); trace capture is disabled.");
        }
    }

    private async Task WriteQueueOverflowMarkersAsync()
    {
        int droppedSources = Interlocked.Exchange(ref _droppedSourceMask, 0);
        foreach ((int mask, string source) in new[] { (1, "Game"), (2, "Discord") })
        {
            if ((droppedSources & mask) == 0)
            {
                continue;
            }

            var marker = SparkyTraceEvent.CreateGap(source, "queue_overflow");
            await _spool.AppendAsync(marker, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task UploadLoopAsync(CancellationToken cancellationToken)
    {
        TimeSpan retryDelay = TimeSpan.FromSeconds(1);
        bool outageLogged = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var pending = await _spool.ReadPendingAsync(1, cancellationToken).ConfigureAwait(false);
                if (pending.Count == 0)
                {
                    await Task.Delay(IdlePollInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                bool uploaded = await UploadOnceAsync(cancellationToken).ConfigureAwait(false);
                if (uploaded)
                {
                    retryDelay = TimeSpan.FromSeconds(1);
                    outageLogged = false;
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!outageLogged)
                {
                    Logger.Warning("[Sparky trace] Some trace events remain unacknowledged; metadata stays in the local spool for retry.");
                    outageLogged = true;
                }

                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                retryDelay = TimeSpan.FromSeconds(Math.Min(MaximumRetryDelay.TotalSeconds, retryDelay.TotalSeconds * 2));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException)
            {
                if (!outageLogged)
                {
                    Logger.Warning($"[Sparky trace] Upload failed ({exception.GetType().Name}); metadata remains in the local spool for retry.");
                    outageLogged = true;
                }

                try
                {
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                retryDelay = TimeSpan.FromSeconds(Math.Min(MaximumRetryDelay.TotalSeconds, retryDelay.TotalSeconds * 2));
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _accepting, 0);
                _input?.Writer.TryComplete();
                Logger.Error($"[Sparky trace] Trace worker failed ({exception.GetType().Name}); trace capture is disabled.");
                return;
            }
        }
    }

    private static async Task<byte[]> ReadBoundedResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
        {
            throw new InvalidDataException("The Sparky trace acknowledgement exceeded its configured size limit.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            int count = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + count > MaximumResponseBytes)
            {
                throw new InvalidDataException("The Sparky trace acknowledgement exceeded its configured size limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
    }

    private void ConfigureEndpoint(Uri endpoint, string token)
    {
        if (endpoint.Scheme != Uri.UriSchemeHttps ||
            !IPAddress.TryParse(endpoint.Host, out var address) ||
            !IPAddress.IsLoopback(address) ||
            endpoint.AbsolutePath != "/v1/trace-events" ||
            endpoint.UserInfo.Length != 0 ||
            endpoint.Query.Length != 0 ||
            endpoint.Fragment.Length != 0 ||
            token.Length is < 16 or > 4096 ||
            token.Any(char.IsControl))
        {
            throw new ArgumentException("The Sparky trace uploader requires a loopback HTTPS endpoint and a bounded bearer token.");
        }

        _endpoint = endpoint;
        _token = token;
    }

    private static Channel<SparkyTraceEvent> CreateInputChannel() =>
        Channel.CreateBounded<SparkyTraceEvent>(new BoundedChannelOptions(InputQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    private static HttpClient CreateHttpClient() => new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false
    })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public void Dispose()
    {
        StopAsync();
        _shutdown?.Dispose();
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed record UploadAcknowledgement(
        [property: JsonPropertyName("acknowledgedEventIds")] IReadOnlyList<Guid>? AcknowledgedEventIds,
        [property: JsonPropertyName("conflictingEventIds")] IReadOnlyList<Guid>? ConflictingEventIds = null,
        [property: JsonPropertyName("error")] string? Error = null);
}
