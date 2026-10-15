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
    private readonly GlobalConfiguration _configuration;
    private readonly Func<EventListenerService> _eventListenerFactory;
    private readonly Func<SparkyTraceUploader> _traceUploaderFactory;
    private readonly SparkyContextStore _contextStore;
    private readonly SparkyCallerLaneScheduler _callerLaneScheduler = new();
    private readonly object _pendingLock = new();
    private readonly Dictionary<SparkyRequestIdentity, int> _pendingByCaller = new();
    private int _pendingTotal;

    private Channel<SparkyWorkItem>? _queue;
    private Channel<SparkyLiveKnowledgeUpdate>? _currentSnapshotQueue;
    private Channel<SparkyLiveKnowledgeUpdate>? _finalizationQueue;
    private Channel<byte>? _outboxChanged;
    private CancellationTokenSource? _shutdown;
    private CancellationTokenSource? _publisherShutdown;
    private SparkyApiClient? _apiClient;
    private SparkyClientOptions? _options;
    private SemaphoreSlim? _workConcurrency;
    private SparkyTraceUploader? _traceUploader;
    private Task[] _workers = [];
    private Task? _finalizationSpoolWorker;
    private SparkyLiveKnowledgeOutbox? _liveKnowledgeOutbox;
    private Character? _senderCharacter;
    private int _started;
    private int _enabled;
    private int _traceErrorLogged;
    private long _requestSequence;

    public SparkyRequestCoordinator(
        GlobalConfiguration configuration,
        Func<EventListenerService> eventListenerFactory,
        Func<SparkyTraceUploader> traceUploaderFactory)
    {
        _configuration = configuration;
        _eventListenerFactory = eventListenerFactory;
        _traceUploaderFactory = traceUploaderFactory;
        _contextStore = new SparkyContextStore(
            Math.Clamp(configuration.SparkyContextCapacity, 1, 100000),
            TimeSpan.FromMinutes(Math.Clamp(configuration.SparkyContextLifetimeMinutes, 1, 1440)),
            Math.Clamp(configuration.SparkyContextMaxTurns, 2, SparkyContextStore.HardMaximumTurns),
            Math.Clamp(configuration.SparkyContextMaxCharacters,
                SparkyContextStore.MinimumMaximumCharacters,
                SparkyContextStore.HardMaximumCharacters));
    }

    public bool IsEnabled => Volatile.Read(ref _enabled) == 1;

    public Character? SenderCharacter => _senderCharacter;

    public int MaxAnswerCharacters => _options?.MaxAnswerCharacters ?? 2000;

    public string? Profile => _options?.Profile;

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

        if (_options.UsesLegacySettings)
        {
            Logger.Warning("[Sparky] Protected client settings v1 are treated as Dev; migrate them to version 2 with Configure-SparkyVdmClientSettings.ps1.");
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
            try
            {
                _traceUploader = _traceUploaderFactory();
                _traceUploader.Start(_options);
                Interlocked.Exchange(ref _traceErrorLogged, 0);
            }
            catch (Exception exception)
            {
                _traceUploader = null;
                Logger.Warning($"[Sparky trace] Uploader setup failed ({exception.GetType().Name}); Sparky answers remain enabled.");
            }

            _queue = Channel.CreateBounded<SparkyWorkItem>(new BoundedChannelOptions(_options.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            _currentSnapshotQueue = Channel.CreateBounded<SparkyLiveKnowledgeUpdate>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            _finalizationQueue = Channel.CreateBounded<SparkyLiveKnowledgeUpdate>(new BoundedChannelOptions(SparkyLiveKnowledgeOutbox.MaximumFinalizations)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            _outboxChanged = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });
            _publisherShutdown = new CancellationTokenSource();
            _liveKnowledgeOutbox = new SparkyLiveKnowledgeOutbox(Path.Combine(
                _configuration.GameRoot,
                "logs",
                "sparky-live-knowledge-outbox.jsonl"));
            _workConcurrency = new SemaphoreSlim(_options.WorkerCount, _options.WorkerCount);
            _finalizationSpoolWorker = SpoolFinalizationsAsync(_finalizationQueue.Reader, _publisherShutdown.Token);
            _workers =
            [
                WorkerLoopAsync(_queue.Reader, _shutdown.Token),
                PublishCurrentSnapshotsAsync(_currentSnapshotQueue.Reader, _publisherShutdown.Token),
                _finalizationSpoolWorker,
                DeliverFinalizationsAsync(_publisherShutdown.Token)
            ];
            Volatile.Write(ref _enabled, 1);
            Logger.Info("[Sparky] Game and Discord request adapter enabled.");
        }
        catch (Exception)
        {
            _workConcurrency?.Dispose();
            _workConcurrency = null;
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
        _currentSnapshotQueue?.Writer.TryComplete();
        _finalizationQueue?.Writer.TryComplete();
        Task traceShutdown = _traceUploader?.StopAsync() ?? Task.CompletedTask;
        _shutdown?.Cancel();
        var shutdownTimer = Stopwatch.StartNew();
        Task[] shutdownTasks = _workers.Append(traceShutdown).ToArray();
        bool dispatchAndTraceStopped = true;
        if (shutdownTasks.Length > 0)
        {
            try
            {
                if (!Task.WaitAll(shutdownTasks, TimeSpan.FromSeconds(5)))
                {
                    _publisherShutdown?.Cancel();
                    TimeSpan remaining = TimeSpan.FromSeconds(5) - shutdownTimer.Elapsed;
                    if (remaining <= TimeSpan.Zero || !Task.WaitAll(shutdownTasks, remaining))
                    {
                        Logger.Error("[Sparky] Background workers did not stop within the shutdown grace period.");
                        dispatchAndTraceStopped = false;
                    }
                }
            }
            catch (AggregateException)
            {
                Logger.Error("[Sparky] A background worker failed during shutdown.");
            }
        }

        if (!dispatchAndTraceStopped)
        {
            return;
        }

        Task laneShutdown = _callerLaneScheduler.WaitForIdleAsync();
        try
        {
            if (!laneShutdown.Wait(TimeSpan.FromSeconds(5)))
            {
                Logger.Error("[Sparky] Caller conversation lanes did not stop within the shutdown grace period.");
                return;
            }
        }
        catch (AggregateException)
        {
            Logger.Error("[Sparky] A caller conversation lane failed during shutdown.");
            return;
        }

        _contextStore.Clear();
        lock (_pendingLock)
        {
            _pendingByCaller.Clear();
            _pendingTotal = 0;
        }

        if (_workers.All(worker => worker.IsCompleted) && traceShutdown.IsCompleted && laneShutdown.IsCompleted)
        {
            _shutdown?.Dispose();
            _publisherShutdown?.Dispose();
            _apiClient?.Dispose();
            _traceUploader?.Dispose();
            _workConcurrency?.Dispose();
            _shutdown = null;
            _publisherShutdown = null;
            _apiClient = null;
            _workConcurrency = null;
            _traceUploader = null;
            _workers = [];
            _queue = null;
            _currentSnapshotQueue = null;
            _finalizationQueue = null;
            _outboxChanged = null;
            _finalizationSpoolWorker = null;
            _liveKnowledgeOutbox = null;
            _senderCharacter = null;
            Interlocked.Exchange(ref _started, 0);
        }
    }

    public override void Update(TimeSpan time)
    {
        // Inference runs only on the tracked worker tasks started above.
    }

    internal bool TryPublishCurrentSnapshot(SparkyLiveKnowledgeUpdate update)
    {
        if (!IsEnabled || !IsPublishable(update, "replace-current") ||
            _currentSnapshotQueue?.Writer.TryWrite(update) != true)
        {
            return false;
        }

        return true;
    }

    internal bool TryPublishSeasonFinalization(SparkyLiveKnowledgeUpdate update)
    {
        if (!IsEnabled || !IsPublishable(update, "finalize-season") ||
            _finalizationQueue?.Writer.TryWrite(update) != true)
        {
            if (IsEnabled && update.Operation == "finalize-season")
                Logger.Warning("[Sparky live knowledge] Season finalization was not queued because the bounded queue is full or the update did not match the protected profile.");
            return false;
        }

        return true;
    }

    private bool IsPublishable(SparkyLiveKnowledgeUpdate update, string operation) =>
        update is not null && update.SchemaVersion == 1 && update.Provider == "seasons" &&
        update.Profile == _options?.Profile && update.Operation == operation;

    private async Task PublishCurrentSnapshotsAsync(
        ChannelReader<SparkyLiveKnowledgeUpdate> reader,
        CancellationToken cancellationToken)
    {
        string? acknowledgedHash = null;
        bool unavailableLogged = false;
        var permanentlyRejected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.TryRead(out var update))
                    continue;
                while (reader.TryRead(out var latest))
                    update = latest;

                string hash = update.ContentHash;
                if (permanentlyRejected.Contains(hash))
                    continue;

                int retryAttempt = 0;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (reader.TryRead(out var newer))
                    {
                        while (reader.TryRead(out var latest))
                            newer = latest;
                        if (!string.Equals(hash, newer.ContentHash, StringComparison.OrdinalIgnoreCase))
                            retryAttempt = 0;
                        update = newer;
                        hash = update.ContentHash;
                        if (permanentlyRejected.Contains(hash))
                            break;
                    }

                    try
                    {
                        bool previouslyAcknowledged = string.Equals(acknowledgedHash, hash, StringComparison.OrdinalIgnoreCase);
                        var attempt = await _apiClient!.PublishCurrentWithHeartbeatAsync(
                            update,
                            previouslyAcknowledged,
                            cancellationToken).ConfigureAwait(false);

                        if (attempt.FullSnapshotSent && !attempt.IsAcknowledged)
                            acknowledgedHash = null;

                        if (attempt.IsAcknowledged)
                        {
                            acknowledgedHash = hash;
                            if (unavailableLogged)
                            {
                                Logger.Info("[Sparky live knowledge] Current season snapshot publishing recovered.");
                                unavailableLogged = false;
                            }
                            break;
                        }

                        if (ShouldRetryLiveKnowledgePublish(attempt.HttpStatusCode))
                        {
                            if (!unavailableLogged)
                            {
                                Logger.Warning("[Sparky live knowledge] Current season snapshot publishing is unavailable; retrying in the background.");
                                unavailableLogged = true;
                            }
                            await Task.Delay(GetRetryDelay(retryAttempt++), cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        if (permanentlyRejected.Add(hash))
                            Logger.Warning($"[Sparky live knowledge] Current season snapshot was rejected ({attempt.Status}); waiting for changed season data.");
                        break;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException or TaskCanceledException)
                    {
                        if (!unavailableLogged)
                        {
                            Logger.Warning("[Sparky live knowledge] Current season snapshot publishing is unavailable; retrying in the background.");
                            unavailableLogged = true;
                        }
                        await Task.Delay(GetRetryDelay(retryAttempt++), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Publisher retries are cancelled after the shutdown spool grace period.
        }
        catch (Exception exception)
        {
            Logger.Error($"[Sparky live knowledge] Current snapshot publisher stopped ({exception.GetType().Name}).");
        }
    }

    private async Task SpoolFinalizationsAsync(
        ChannelReader<SparkyLiveKnowledgeUpdate> reader,
        CancellationToken cancellationToken)
    {
        bool outboxWriteFailureLogged = false;
        try
        {
            await _liveKnowledgeOutbox!.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await foreach (var update in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                while (true)
                {
                    try
                    {
                        var disposition = await _liveKnowledgeOutbox.AppendAsync(update, cancellationToken).ConfigureAwait(false);
                        if (disposition == SparkyLiveKnowledgeOutboxAppendDisposition.CapacityExceeded)
                        {
                            Logger.Error("[Sparky live knowledge] Season finalization outbox reached its configured limit; this result was not persisted.");
                        }
                        else
                        {
                            _outboxChanged?.Writer.TryWrite(0);
                            if (outboxWriteFailureLogged)
                            {
                                Logger.Info("[Sparky live knowledge] Season finalization outbox writes recovered.");
                                outboxWriteFailureLogged = false;
                            }
                        }
                        break;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        if (!outboxWriteFailureLogged)
                        {
                            Logger.Error("[Sparky live knowledge] Could not persist a season finalization; retrying the local outbox write.");
                            outboxWriteFailureLogged = true;
                        }
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The shutdown grace period expired before all queued finalizations were persisted.
        }
        catch (Exception exception)
        {
            Logger.Error($"[Sparky live knowledge] Season finalization spooler stopped ({exception.GetType().Name}).");
        }
        finally
        {
            _outboxChanged?.Writer.TryComplete();
        }
    }

    private async Task DeliverFinalizationsAsync(CancellationToken cancellationToken)
    {
        bool unavailableLogged = false;
        var permanentlyRejected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int retryAttempt = 0;
        try
        {
            await _liveKnowledgeOutbox!.InitializeAsync(cancellationToken).ConfigureAwait(false);
            while (true)
            {
                var pending = await _liveKnowledgeOutbox.ReadPendingAsync(cancellationToken).ConfigureAwait(false);
                var candidate = pending.FirstOrDefault(update => update.Profile == _options!.Profile &&
                    !permanentlyRejected.Contains(GetFinalizationKey(update)));
                if (candidate is null)
                {
                    if (_outboxChanged is null ||
                        !await _outboxChanged.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        break;
                    }
                    while (_outboxChanged.Reader.TryRead(out _)) { }
                    continue;
                }

                try
                {
                    var result = await _apiClient!.PublishFinalizationAsync(candidate, cancellationToken).ConfigureAwait(false);
                    if (result.IsAcknowledged)
                    {
                        await _liveKnowledgeOutbox.AcknowledgeAsync(candidate, cancellationToken).ConfigureAwait(false);
                        retryAttempt = 0;
                        if (unavailableLogged)
                        {
                            Logger.Info("[Sparky live knowledge] Season finalization publishing recovered.");
                            unavailableLogged = false;
                        }
                        continue;
                    }

                    if (ShouldRetryLiveKnowledgePublish(result.HttpStatusCode))
                    {
                        if (!unavailableLogged)
                        {
                            Logger.Warning("[Sparky live knowledge] Season finalization publishing is unavailable; durable results will retry in the background.");
                            unavailableLogged = true;
                        }
                        await Task.Delay(GetRetryDelay(retryAttempt++), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    permanentlyRejected.Add(GetFinalizationKey(candidate));
                    Logger.Warning($"[Sparky live knowledge] Season {candidate.Finalization!.SeasonId} finalization was rejected ({result.Status}) and remains in the durable outbox.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException or TaskCanceledException)
                {
                    if (!unavailableLogged)
                    {
                        Logger.Warning("[Sparky live knowledge] Season finalization publishing is unavailable; durable results will retry in the background.");
                        unavailableLogged = true;
                    }
                    await Task.Delay(GetRetryDelay(retryAttempt++), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The durable outbox remains available for the next process start.
        }
        catch (Exception exception)
        {
            Logger.Error($"[Sparky live knowledge] Season finalization delivery stopped ({exception.GetType().Name}).");
        }
    }

    internal static bool ShouldRetryLiveKnowledgePublish(int httpStatusCode) =>
        httpStatusCode is 408 or 429 or >= 500;

    private static TimeSpan GetRetryDelay(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(attempt, 6))));

    private static string GetFinalizationKey(SparkyLiveKnowledgeUpdate update) =>
        $"{update.Profile}:{update.Finalization!.SeasonId}:{update.ContentHash}";

    public bool TrySubmitGameQuestion(string channelName, Character caller, string question)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var identity = new SparkyRequestIdentity(SparkySource.Game, (ulong)caller.Id);
        Submit(identity, caller, channelName, 0, question, SparkyRankResolver.ResolveGame(caller.AccessLevel));
        return true;
    }

    internal bool TrySubmitDiscordQuestion(
        ulong channelId,
        ulong callerId,
        string question,
        SparkyCallerRank callerRank = SparkyCallerRank.Agent)
    {
        if (!IsEnabled)
        {
            Logger.Warning("[Sparky] Discord request was not accepted because the adapter is disabled.");
            return false;
        }

        var identity = new SparkyRequestIdentity(SparkySource.Discord, callerId);
        Submit(identity, null, null, channelId, question, callerRank);
        return true;
    }

    private void Submit(
        SparkyRequestIdentity identity,
        Character? caller,
        string? channelName,
        ulong discordChannelId,
        string question,
        SparkyCallerRank callerRank)
    {
        long requestId = Interlocked.Increment(ref _requestSequence);
        Guid traceId = CreateTraceId();
        RecordTrace(traceId, identity.Source, "trigger", "accepted", detailCode: "addressed");
        var now = DateTimeOffset.UtcNow;
        var context = _contextStore.GetAndTouch(identity, now);
        int? fallbackLanguageId = context?.LanguageId ?? GetCharacterLanguage(caller);
        question = question.Trim();
        if (question.Length == 0)
        {
            RecordTrace(traceId, identity.Source, "queue", "rejected", detailCode: "empty_question");
            PublishImmediate(requestId, traceId, identity.Source, caller, channelName, discordChannelId, identity.Id,
                new SparkyApiResult(SparkyResultKind.Usage, null, null, [], fallbackLanguageId));
            return;
        }

        if (question.Length > _options!.MaxQuestionCharacters)
        {
            RecordTrace(traceId, identity.Source, "queue", "rejected", detailCode: "question_too_long");
            PublishImmediate(requestId, traceId, identity.Source, caller, channelName, discordChannelId, identity.Id,
                new SparkyApiResult(SparkyResultKind.QuestionTooLong, null, null, [], fallbackLanguageId));
            return;
        }

        if (!TryAddPending(identity, _options.PendingPerCallerLimit, _options.QueueCapacity + _options.WorkerCount))
        {
            RecordTrace(traceId, identity.Source, "queue", "dropped", detailCode: "pending_limit");
            Logger.Warning($"[Sparky] {identity.Source} request #{requestId:D6} was rejected because the pending request limit was reached.");
            PublishImmediate(requestId, traceId, identity.Source, caller, channelName, discordChannelId, identity.Id,
                new SparkyApiResult(SparkyResultKind.Busy, null, null, [], fallbackLanguageId));
            return;
        }

        var work = new SparkyWorkItem(requestId, traceId, identity, caller, channelName, discordChannelId, question, callerRank);
        if (_queue?.Writer.TryWrite(work) != true)
        {
            RemovePending(identity);
            RecordTrace(traceId, identity.Source, "queue", "dropped", detailCode: "queue_full");
            Logger.Warning($"[Sparky] {identity.Source} request #{requestId:D6} was rejected because the work queue was unavailable or full.");
            PublishImmediate(requestId, traceId, identity.Source, caller, channelName, discordChannelId, identity.Id,
                new SparkyApiResult(SparkyResultKind.Busy, null, null, [], fallbackLanguageId));
            return;
        }

        RecordTrace(traceId, identity.Source, "queue", "queued", detailCode: "accepted");
        Logger.Info($"[Sparky] {identity.Source} request #{requestId:D6} queued.");
    }

    private async Task WorkerLoopAsync(ChannelReader<SparkyWorkItem> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var work in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                _ = _callerLaneScheduler.Schedule(work.Identity, () => ProcessWorkItemAsync(work, cancellationToken));
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

    private async Task ProcessWorkItemAsync(SparkyWorkItem work, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        SparkyApiResult? result = null;
        SparkyContextEntry? context = null;
        bool concurrencySlotAcquired = false;
        try
        {
            await _workConcurrency!.WaitAsync(cancellationToken).ConfigureAwait(false);
            concurrencySlotAcquired = true;

            // Snapshot only when this request reaches the head of its caller lane. A prior turn can
            // therefore update clarification and entity state before this request is interpreted.
            context = _contextStore.GetAndTouch(work.Identity, DateTimeOffset.UtcNow);
            result = await _apiClient!.AnswerAsync(
                    work.TraceId,
                    work.Question,
                    context?.ItemDisplayName,
                    context?.LanguageId,
                    cancellationToken,
                    context,
                    work.CallerRank)
                .ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                RecordTrace(work.TraceId, work.Identity.Source, "api", "cancelled", timer.ElapsedMilliseconds);
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.Answer))
            {
                _contextStore.AppendExchange(
                    work.Identity,
                    work.TraceId,
                    work.Question,
                    result.Answer,
                    result.ContextUpdate,
                    result.LanguageId,
                    DateTimeOffset.UtcNow);
            }
            else if (result.LanguageId is not null || result.ItemDisplayName is not null)
            {
                _contextStore.Set(
                    work.Identity,
                    result.ItemDisplayName ?? context?.ItemDisplayName,
                    result.LanguageId ?? context?.LanguageId,
                    DateTimeOffset.UtcNow);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RecordTrace(work.TraceId, work.Identity.Source, "api", "cancelled", timer.ElapsedMilliseconds);
            return;
        }
        catch (Exception exception)
        {
            result = SparkyApiResult.Unavailable with { LanguageId = context?.LanguageId };
            Logger.Error($"[Sparky] Upstream request #{work.RequestId:D6} failed ({exception.GetType().Name}).");
        }
        finally
        {
            if (concurrencySlotAcquired)
            {
                _workConcurrency!.Release();
            }

            RemovePending(work.Identity);
        }

        timer.Stop();
        if (result is null)
        {
            return;
        }

        RecordTrace(
            work.TraceId,
            work.Identity.Source,
            "api",
            GetTraceOutcome(result.Kind),
            timer.ElapsedMilliseconds,
            "completed");
        Logger.Info($"[Sparky] {work.Identity.Source} request #{work.RequestId:D6} completed with {result.Kind} in {timer.ElapsedMilliseconds} ms.");
        PublishImmediate(work.RequestId, work.TraceId, work.Identity.Source, work.Caller, work.ChannelName, work.DiscordChannelId, work.Identity.Id, result);
    }

    private bool TryAddPending(SparkyRequestIdentity identity, int perCallerMaximum, int totalMaximum)
    {
        lock (_pendingLock)
        {
            var count = _pendingByCaller.GetValueOrDefault(identity);
            if (count >= perCallerMaximum || _pendingTotal >= totalMaximum)
            {
                return false;
            }

            _pendingByCaller[identity] = count + 1;
            _pendingTotal++;
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

            _pendingTotal = Math.Max(0, _pendingTotal - 1);
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

    private static int? GetCharacterLanguage(Character? character)
    {
        return character is not null && CustomDictionary.LanguageNames.ContainsKey(character.Language)
            ? character.Language
            : 0;
    }

    private void PublishImmediate(
        long requestId,
        Guid traceId,
        SparkySource source,
        Character? caller,
        string? channelName,
        ulong discordChannelId,
        ulong callerId,
        SparkyApiResult result)
    {
        try
        {
            _eventListenerFactory().PublishMessage(
                new SparkyResponseMessage(requestId, traceId, source, caller, channelName, discordChannelId, callerId, result));
            RecordTrace(traceId, source, "delivery", "queued", detailCode: "event_queue");
        }
        catch (Exception exception)
        {
            RecordTrace(traceId, source, "delivery", "failed", detailCode: "event_queue");
            Logger.Error($"[Sparky] Could not queue response #{requestId:D6} ({exception.GetType().Name}).");
        }
    }

    internal void RecordDelivery(Guid traceId, SparkySource source, string outcome, string detailCode)
    {
        RecordTrace(traceId, source, "delivery", outcome, detailCode: detailCode);
    }

    private void RecordTrace(
        Guid traceId,
        SparkySource source,
        string stage,
        string outcome,
        long? elapsedMilliseconds = null,
        string? detailCode = null)
    {
        if (traceId == Guid.Empty || _traceUploader is null)
        {
            return;
        }

        try
        {
            var traceEvent = SparkyTraceEvent.Create(traceId, source.ToString(), stage, outcome, elapsedMilliseconds, detailCode);
            _traceUploader.TryRecord(traceEvent);
        }
        catch (Exception exception)
        {
            if (Interlocked.Exchange(ref _traceErrorLogged, 1) == 0)
            {
                Logger.Warning($"[Sparky trace] Metadata could not be queued ({exception.GetType().Name}); answers remain enabled.");
            }
        }
    }

    private static string GetTraceOutcome(SparkyResultKind resultKind) => resultKind switch
    {
        SparkyResultKind.Answered => "answered",
        SparkyResultKind.NotFound => "not_found",
        SparkyResultKind.Ambiguous => "unsupported",
        SparkyResultKind.Busy => "busy",
        SparkyResultKind.Usage or SparkyResultKind.QuestionTooLong => "invalid_request",
        _ => "unavailable"
    };

    internal static Guid CreateTraceId() => Guid.NewGuid();

    private sealed record SparkyWorkItem(
        long RequestId,
        Guid TraceId,
        SparkyRequestIdentity Identity,
        Character? Caller,
        string? ChannelName,
        ulong DiscordChannelId,
        string Question,
        SparkyCallerRank CallerRank);
}
