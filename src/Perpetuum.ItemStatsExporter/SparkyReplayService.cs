using Perpetuum.Services.Sparky;

namespace Perpetuum.ItemStatsExporter;

internal interface ISparkyReplayAnswerer
{
    Task<SparkyReplayOutput> AnswerAsync(string question, SparkyProfileSnapshot snapshot, CancellationToken cancellationToken);
}

internal sealed class SparkyReplayAnswerer(SparkyServiceOptions defaults) : ISparkyReplayAnswerer
{
    public async Task<SparkyReplayOutput> AnswerAsync(
        string question,
        SparkyProfileSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var options = snapshot.ApplyTo(defaults);
        var interpreter = new SparkyQuestionInterpreter(options);
        var interpretation = await interpreter.InterpretAsync(
            question,
            snapshot.Bundle.Languages,
            contextLanguageId: null,
            cancellationToken,
            snapshot.Configuration.RouteAliases).ConfigureAwait(false);
        var router = new SparkyAnswerRouter(
            options,
            snapshot.Configuration.Retrieval,
            snapshot.Configuration.PersonaStyle);
        var answer = await router.AnswerAsync(
            question,
            interpretation,
            snapshot.Bundle,
            contextItem: null,
            contextLanguageId: null,
            cancellationToken).ConfigureAwait(false);
        return new SparkyReplayOutput(
            answer.Status,
            interpretation.Intent.ToString(),
            interpretation.MentionedEntities,
            answer.SelectedSourceIds,
            answer.Answer);
    }
}

internal sealed class SparkyReplayService
{
    public const int MaximumExamples = 100;
    public const int MaximumStoredResultBytes = 1024 * 1024;

    private readonly SparkyTraceStore? _store;
    private readonly SparkyProfileManager _profiles;
    private readonly SparkyConcurrencyLimiter _limiter;
    private readonly SparkyServiceOptions _defaults;
    private readonly ISparkyReplayAnswerer _answerer;
    private readonly SemaphoreSlim _runGate = new(1, 1);

    public SparkyReplayService(
        SparkyTraceStore? store,
        SparkyProfileManager profiles,
        SparkyConcurrencyLimiter limiter,
        SparkyServiceOptions defaults,
        ISparkyReplayAnswerer? answerer = null)
    {
        _store = store;
        _profiles = profiles;
        _limiter = limiter;
        _defaults = defaults;
        _answerer = answerer ?? new SparkyReplayAnswerer(defaults);
    }

    public async Task<SparkyReplayRun> RunAsync(
        SparkyReplayRequest request,
        CancellationToken cancellationToken)
    {
        if (_store is null)
            throw new InvalidOperationException("trace_storage_unavailable");
        if (request.Environment is not (SparkyApiProfile.Dev or SparkyApiProfile.Live) ||
            string.IsNullOrWhiteSpace(request.DraftRevisionId) || request.DraftRevisionId.Length > 80 ||
            request.ExampleIds is not { Count: > 0 and <= MaximumExamples } ||
            request.ExampleIds.Any(id => id == Guid.Empty) || request.ExampleIds.Distinct().Count() != request.ExampleIds.Count)
            throw new InvalidDataException("The replay request is invalid or exceeds the configured limit.");
        if (!_profiles.TryGetSnapshot(request.Environment, out var activeSnapshot))
            throw new InvalidOperationException("profile_disabled");
        var candidateSnapshot = await _profiles.GetDraftSnapshotAsync(
            request.Environment, request.DraftRevisionId, cancellationToken).ConfigureAwait(false);
        if (candidateSnapshot is null)
            throw new InvalidDataException("draft_not_found");

        await _runGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var available = await _store.GetEvaluationExamplesAsync(
                request.Environment, MaximumExamples, cancellationToken).ConfigureAwait(false);
            var byId = available.ToDictionary(example => example.ExampleId);
            if (request.ExampleIds.Any(exampleId => !byId.ContainsKey(exampleId)))
                throw new InvalidDataException("One or more reviewed evaluation examples were not found.");

            var results = new List<SparkyReplayComparison>(request.ExampleIds.Count);
            foreach (Guid exampleId in request.ExampleIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var example = byId[exampleId];
                var active = await RunBoundedAsync(example.Question, activeSnapshot, cancellationToken).ConfigureAwait(false);
                var candidate = await RunBoundedAsync(example.Question, candidateSnapshot, cancellationToken).ConfigureAwait(false);
                results.Add(new SparkyReplayComparison(
                    example.ExampleId,
                    example.Question,
                    example.ExpectedIntent,
                    example.ExpectedProperties,
                    active,
                    candidate));
            }

            var replay = new SparkyReplayRun(
                Guid.NewGuid(),
                request.Environment,
                activeSnapshot.RevisionId,
                candidateSnapshot.RevisionId,
                DateTimeOffset.UtcNow,
                results);
            await _store.SaveReplayAsync(replay, cancellationToken).ConfigureAwait(false);
            return replay;
        }
        finally
        {
            _runGate.Release();
        }
    }

    public Task<SparkyReplayRun?> GetAsync(
        Guid replayId,
        SparkyApiProfile environment,
        CancellationToken cancellationToken) =>
        _store is null
            ? Task.FromResult<SparkyReplayRun?>(null)
            : _store.GetReplayAsync(replayId, environment, cancellationToken);

    private async Task<SparkyReplayOutput> RunBoundedAsync(
        string question,
        SparkyProfileSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        int globalLimit = Math.Max(1, _defaults.MaxConcurrentRequests);
        if (!_limiter.TryAcquire(snapshot.Profile, snapshot.Configuration.MaxConcurrentRequests, globalLimit))
            throw new InvalidOperationException("replay_busy");
        try
        {
            return await _answerer.AnswerAsync(question, snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _limiter.Release(snapshot.Profile);
        }
    }
}
