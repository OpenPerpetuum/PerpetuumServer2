using System.Diagnostics;
using System.Threading.Channels;
using Perpetuum.Accounting.Characters;
using Perpetuum.Log;
using Perpetuum.Services.EventServices;
using Perpetuum.Services.EventServices.EventMessages;
using Perpetuum.Threading.Process;
using Process = Perpetuum.Threading.Process.Process;

namespace Perpetuum.Services.Sparky;

public sealed class SparkyRequestCoordinator : Process
{
    private const string BusyText = "I'm handling several requests at the moment. Please try again shortly.";
    private const string QuestionTooLongText = "Please shorten your question and ask again.";

    private readonly GlobalConfiguration _configuration;
    private readonly Func<EventListenerService> _eventListenerFactory;
    private readonly SparkyContextStore _contextStore;
    private readonly object _pendingLock = new();
    private readonly Dictionary<SparkyRequestIdentity, int> _pendingByCaller = new();

    private Channel<SparkyWorkItem>? _queue;
    private CancellationTokenSource? _shutdown;
    private SparkyApiClient? _apiClient;
    private SparkyClientOptions? _options;
    private Task[] _workers = [];
    private Character? _senderCharacter;
    private int _started;
    private int _enabled;

    public SparkyRequestCoordinator(
        GlobalConfiguration configuration,
        Func<EventListenerService> eventListenerFactory)
    {
        _configuration = configuration;
        _eventListenerFactory = eventListenerFactory;
        _contextStore = new SparkyContextStore(
            Math.Clamp(configuration.SparkyContextCapacity, 1, 100000),
            TimeSpan.FromMinutes(Math.Clamp(configuration.SparkyContextLifetimeMinutes, 1, 1440)));
    }

    public bool IsEnabled => Volatile.Read(ref _enabled) == 1;

    public Character? SenderCharacter => _senderCharacter;

    public int MaxAnswerCharacters => _options?.MaxAnswerCharacters ?? 2000;

    public override void Start()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return;
        }

        if (!_configuration.EnableSparky)
        {
            return;
        }

        if (!SparkyClientOptions.TryCreate(_configuration, out _options) || _options is null)
        {
            Logger.Error("[Sparky] Disabled because the Sparky-only settings or protected API credential are invalid.");
            return;
        }

        try
        {
            _senderCharacter = Character.GetByNick(_options.SenderCharacterNick);
            if (_senderCharacter == Character.None)
            {
                Logger.Error("[Sparky] Disabled because the configured sender character could not be resolved.");
                _senderCharacter = null;
                return;
            }

            _apiClient = new SparkyApiClient(_options);
            _shutdown = new CancellationTokenSource();
            _queue = Channel.CreateBounded<SparkyWorkItem>(new BoundedChannelOptions(_options.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = _options.WorkerCount == 1,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            _workers = Enumerable.Range(0, _options.WorkerCount)
                .Select(_ => WorkerLoopAsync(_queue.Reader, _shutdown.Token))
                .ToArray();
            Volatile.Write(ref _enabled, 1);
            Logger.Info("[Sparky] Game and Discord request adapter enabled.");
        }
        catch (Exception)
        {
            _apiClient?.Dispose();
            _apiClient = null;
            _senderCharacter = null;
            Logger.Error("[Sparky] Disabled because initialization failed; normal game startup will continue.");
        }
    }

    public override void Stop()
    {
        Volatile.Write(ref _enabled, 0);
        _queue?.Writer.TryComplete();
        _shutdown?.Cancel();
        if (_workers.Length > 0)
        {
            try
            {
                if (!Task.WaitAll(_workers, TimeSpan.FromSeconds(5)))
                {
                    Logger.Error("[Sparky] Background workers did not stop within the shutdown grace period.");
                }
            }
            catch (AggregateException)
            {
                Logger.Error("[Sparky] A background worker failed during shutdown.");
            }
        }

        _contextStore.Clear();
        lock (_pendingLock)
        {
            _pendingByCaller.Clear();
        }

        if (_workers.All(worker => worker.IsCompleted))
        {
            _shutdown?.Dispose();
            _apiClient?.Dispose();
            _shutdown = null;
            _apiClient = null;
            _workers = [];
            _queue = null;
            _senderCharacter = null;
            Interlocked.Exchange(ref _started, 0);
        }
    }

    public override void Update(TimeSpan time)
    {
        // Inference runs only on the tracked worker tasks started above.
    }

    public bool TrySubmitGameQuestion(string channelName, Character caller, string question)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var identity = new SparkyRequestIdentity(SparkySource.Game, (ulong)caller.Id);
        Submit(identity, caller, channelName, 0, question);
        return true;
    }

    public bool TrySubmitDiscordQuestion(ulong channelId, ulong callerId, string question)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var identity = new SparkyRequestIdentity(SparkySource.Discord, callerId);
        Submit(identity, null, null, channelId, question);
        return true;
    }

    private void Submit(
        SparkyRequestIdentity identity,
        Character? caller,
        string? channelName,
        ulong discordChannelId,
        string question)
    {
        question = question.Trim();
        if (question.Length == 0)
        {
            PublishImmediate(identity.Source, caller, channelName, discordChannelId,
                new SparkyApiResult(SparkyResultKind.Usage, null, null, []));
            return;
        }

        if (question.Length > _options!.MaxQuestionCharacters)
        {
            PublishImmediate(identity.Source, caller, channelName, discordChannelId,
                new SparkyApiResult(SparkyResultKind.QuestionTooLong, null, null, []));
            return;
        }

        if (!TryAddPending(identity, _options.PendingPerCallerLimit))
        {
            PublishImmediate(identity.Source, caller, channelName, discordChannelId,
                new SparkyApiResult(SparkyResultKind.Busy, BusyText, null, []));
            return;
        }

        var contextItem = _contextStore.GetAndTouch(identity, DateTimeOffset.UtcNow);
        var work = new SparkyWorkItem(identity, caller, channelName, discordChannelId, question, contextItem);
        if (_queue?.Writer.TryWrite(work) != true)
        {
            RemovePending(identity);
            PublishImmediate(identity.Source, caller, channelName, discordChannelId,
                new SparkyApiResult(SparkyResultKind.Busy, BusyText, null, []));
        }
    }

    private async Task WorkerLoopAsync(ChannelReader<SparkyWorkItem> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var work in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var timer = Stopwatch.StartNew();
                SparkyApiResult result;
                try
                {
                    result = await _apiClient!.AnswerAsync(work.Question, work.ContextItem, cancellationToken)
                        .ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        RemovePending(work.Identity);
                        return;
                    }

                    if (result.Kind == SparkyResultKind.Answered && !string.IsNullOrWhiteSpace(result.ItemDisplayName))
                    {
                        _contextStore.Set(work.Identity, result.ItemDisplayName, DateTimeOffset.UtcNow);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    RemovePending(work.Identity);
                    return;
                }
                catch (Exception exception)
                {
                    result = SparkyApiResult.Unavailable;
                    Logger.Error($"[Sparky] Upstream request failed ({exception.GetType().Name}).");
                }

                timer.Stop();
                RemovePending(work.Identity);
                Logger.Info($"[Sparky] Request completed with {result.Kind} in {timer.ElapsedMilliseconds} ms.");
                PublishImmediate(work.Identity.Source, work.Caller, work.ChannelName, work.DiscordChannelId, result);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during server shutdown.
        }
        catch (Exception exception)
        {
            Logger.Error($"[Sparky] Background worker stopped ({exception.GetType().Name}).");
        }
    }

    private bool TryAddPending(SparkyRequestIdentity identity, int maximum)
    {
        lock (_pendingLock)
        {
            var count = _pendingByCaller.GetValueOrDefault(identity);
            if (count >= maximum)
            {
                return false;
            }

            _pendingByCaller[identity] = count + 1;
            return true;
        }
    }

    private void RemovePending(SparkyRequestIdentity identity)
    {
        lock (_pendingLock)
        {
            if (!_pendingByCaller.TryGetValue(identity, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _pendingByCaller.Remove(identity);
            }
            else
            {
                _pendingByCaller[identity] = count - 1;
            }
        }
    }

    private void PublishImmediate(
        SparkySource source,
        Character? caller,
        string? channelName,
        ulong discordChannelId,
        SparkyApiResult result)
    {
        try
        {
            _eventListenerFactory().PublishMessage(
                new SparkyResponseMessage(source, caller, channelName, discordChannelId, result));
        }
        catch (Exception exception)
        {
            Logger.Error($"[Sparky] Could not queue a response ({exception.GetType().Name}).");
        }
    }

    private sealed record SparkyWorkItem(
        SparkyRequestIdentity Identity,
        Character? Caller,
        string? ChannelName,
        ulong DiscordChannelId,
        string Question,
        string? ContextItem);
}
