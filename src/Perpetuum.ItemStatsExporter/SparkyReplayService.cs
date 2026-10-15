using Perpetuum.Services.Sparky;

namespace Perpetuum.ItemStatsExporter;

internal interface ISparkyReplayAnswerer
{
    Task<SparkyReplayStep> AnswerAsync(
        string question,
        SparkyProfileSnapshot snapshot,
        IReadOnlyList<SparkyConversationTurn> contextTurns,
        string? contextSummary,
        SparkyPendingClarification? pendingClarification,
        CancellationToken cancellationToken);
}

internal sealed record SparkyReplayStep(SparkyReplayOutput Output, SparkyConversationUpdate? ContextUpdate);

internal sealed class SparkyReplayAnswerer(SparkyServiceOptions defaults) : ISparkyReplayAnswerer
{
    public async Task<SparkyReplayStep> AnswerAsync(
        string question,
        SparkyProfileSnapshot snapshot,
        IReadOnlyList<SparkyConversationTurn> contextTurns,
        string? contextSummary,
        SparkyPendingClarification? pendingClarification,
        CancellationToken cancellationToken)
    {
        var options = snapshot.ApplyTo(defaults);
        var interpreter = new SparkyQuestionInterpreter(options);
        var interpretation = await interpreter.InterpretAsync(
            question,
            snapshot.Bundle.Languages,
            contextLanguageId: null,
            cancellationToken,
            snapshot.Configuration.RouteAliases,
            contextTurns,
            contextSummary,
            pendingClarification).ConfigureAwait(false);
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
            cancellationToken,
            contextTurns,
            contextSummary,
            pendingClarification).ConfigureAwait(false);
        var output = new SparkyReplayOutput(
            answer.Status,
            interpretation.Intent.ToString(),
            interpretation.MentionedEntities,
            answer.SelectedSourceIds,
            answer.Answer,
            interpretation.Scope.ToString(),
            answer.EnglishSearchQuery ?? interpretation.EnglishSearchQuery,
            answer.Item ?? (answer.SelectedSourceIds is { Count: > 0 } sourceIds
                ? $"{sourceIds.Count} verified knowledge passage(s)"
                : null),
            new SparkyReplayProfileMetadata(
                options.ModelId,
                snapshot.RevisionId,
                snapshot.Bundle.Manifest.BundleVersion,
                options.AnswerTemperature,
                options.AnswerTopP,
                options.ConversationHistoryTurns,
                options.ConversationHistoryCharacters),
            answer.EffectiveIntent,
            answer.EffectiveFocus,
            answer.ResolverOutcome,
            FormatComparisonSummary(answer.ComparisonDiagnostics));
        return new SparkyReplayStep(output, SparkyConversationUpdateFactory.Create(question, interpretation, answer));
    }

    private static string? FormatComparisonSummary(SparkyComparisonDiagnostics? diagnostics)
    {
        if (diagnostics is null)
            return null;

        string subjects = string.Join(" vs ", diagnostics.Subjects.Select(subject => subject.DisplayName));
        var facts = diagnostics.Facts.Take(8).Select(fact =>
        {
            string values = string.Join(" vs ", fact.Values);
            string unit = string.IsNullOrWhiteSpace(fact.Unit) ? string.Empty : $" {fact.Unit}";
            string leader = fact.LeadingSubjectName is null ? string.Empty : $"; direction favors {fact.LeadingSubjectName}";
            return $"{fact.AttributeLabel}: {values}{unit} (difference {fact.Difference}{unit}{leader})";
        });
        string summary = $"{diagnostics.ResultCategory}: {subjects}; " +
            (string.IsNullOrWhiteSpace(diagnostics.MetricPhrase) ? string.Empty : $"requested {diagnostics.MetricPhrase}; ") +
            string.Join("; ", facts);
        return summary.Length <= 500 ? summary : summary[..500];
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
                IReadOnlyList<SparkyEvaluationTurn> turns = example.Turns ??
                    SparkyEvaluationValidator.FromLegacy(example.Question, example.ExpectedIntent, example.ExpectedProperties);
                IReadOnlyList<SparkyReplayOutput> activeTurns = await RunSequenceBoundedAsync(turns, activeSnapshot, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<SparkyReplayOutput> candidateTurns = await RunSequenceBoundedAsync(turns, candidateSnapshot, cancellationToken).ConfigureAwait(false);
                var perTurn = turns.Select((turn, index) => new SparkyReplayTurnResult(
                    turn.Question,
                    turn,
                    activeTurns[index],
                    candidateTurns[index])).ToArray();
                results.Add(new SparkyReplayComparison(
                    example.ExampleId,
                    turns[0].Question,
                    example.ExpectedIntent,
                    example.ExpectedProperties,
                    activeTurns[^1],
                    candidateTurns[^1],
                    perTurn));
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

    private async Task<IReadOnlyList<SparkyReplayOutput>> RunSequenceBoundedAsync(
        IReadOnlyList<SparkyEvaluationTurn> turns,
        SparkyProfileSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var history = new List<SparkyConversationTurn>();
        string? summary = null;
        SparkyPendingClarification? pending = null;
        var outputs = new List<SparkyReplayOutput>(turns.Count);
        foreach (SparkyEvaluationTurn turn in turns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int globalLimit = Math.Max(1, _defaults.MaxConcurrentRequests);
            if (!_limiter.TryAcquire(snapshot.Profile, snapshot.Configuration.MaxConcurrentRequests, globalLimit))
                throw new InvalidOperationException("replay_busy");
            try
            {
                var step = await _answerer.AnswerAsync(
                    turn.Question, snapshot, history.ToArray(), summary, pending, cancellationToken).ConfigureAwait(false);
                outputs.Add(step.Output);
                if (!string.IsNullOrWhiteSpace(step.Output.Answer))
                {
                    history.Add(new SparkyConversationTurn("user", turn.Question));
                    history.Add(new SparkyConversationTurn("assistant", step.Output.Answer));
                }
                if (step.ContextUpdate is not null)
                {
                    summary = step.ContextUpdate.Summary ?? summary;
                    pending = step.ContextUpdate.PendingClarification;
                }
                var bounded = SparkyConversationContextBudget.Apply(
                    history, summary, pending,
                    snapshot.Configuration.ConversationHistoryTurns,
                    snapshot.Configuration.ConversationHistoryCharacters);
                history = bounded.Turns.ToList();
                summary = bounded.Summary;
                pending = bounded.PendingClarification;
            }
            finally
            {
                _limiter.Release(snapshot.Profile);
            }
        }
        return outputs;
    }
}
