using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Perpetuum.ItemStatsExporter;

internal static partial class SparkyTraceEventValidator
{
    public const int MaxBatchCount = 100;
    public const int MaxBodyBytes = 64 * 1024;
    public const int MaxDetailCodeCharacters = 64;

    private static readonly HashSet<string> ExternalSources = new(StringComparer.Ordinal)
    {
        "Game",
        "Discord"
    };

    private static readonly HashSet<string> Stages = new(StringComparer.Ordinal)
    {
        "trigger",
        "queue",
        "api",
        "request",
        "interpretation",
        "retrieval",
        "answer",
        "delivery",
        "trace_gap"
    };

    private static readonly HashSet<string> Outcomes = new(StringComparer.Ordinal)
    {
        "recognized",
        "rejected",
        "queued",
        "dropped",
        "accepted",
        "unauthorized",
        "invalid_request",
        "busy",
        "answered",
        "not_found",
        "unsupported",
        "cancelled",
        "timeout",
        "unavailable",
        "returned",
        "sent",
        "failed",
        "completed",
        "trace_gap"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    public static bool TryDeserializeBatch(
        ReadOnlySpan<byte> json,
        out IReadOnlyList<SparkyTraceEventRequest>? events)
    {
        events = null;
        if (json.IsEmpty || json.Length > MaxBodyBytes)
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<SparkyTraceEventRequest[]>(json, JsonOptions);
            if (parsed is null || !TryValidateBatch(parsed, out _))
            {
                return false;
            }

            events = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryValidateBatch(
        IReadOnlyList<SparkyTraceEventRequest> events,
        out string? error)
    {
        error = null;
        if (events.Count is < 1 or > MaxBatchCount)
        {
            error = "invalid_batch_size";
            return false;
        }

        foreach (var traceEvent in events)
        {
            if (traceEvent is null || !IsValid(traceEvent, allowAiSource: false))
            {
                error = "invalid_event";
                return false;
            }
        }

        return true;
    }

    internal static bool IsValidStoreEvent(SparkyTraceEvent traceEvent) =>
        traceEvent is not null &&
        IsValid(traceEvent, allowAiSource: true) &&
        IsOptionalTextValid(traceEvent.Intent, 64) &&
        IsOptionalTextValid(traceEvent.Focus, 64) &&
        IsOptionalTextValid(traceEvent.SearchQuery, 160) &&
        IsStringListValid(traceEvent.EntityCandidates, maxCount: 8, maxCharacters: 64) &&
        IsStringListValid(traceEvent.SelectedSourceIds, maxCount: 32, maxCharacters: 128) &&
        IsOptionalTextValid(traceEvent.ModelId, 128) &&
        IsOptionalTextValid(traceEvent.ConfigRevision, 64) &&
        IsOptionalTextValid(traceEvent.BundleVersion, 128) &&
        IsOptionalTextValid(traceEvent.FinishStatus, 32) &&
        (traceEvent.AnswerTemperature is null || double.IsFinite(traceEvent.AnswerTemperature.Value) && traceEvent.AnswerTemperature is >= 0 and <= 2) &&
        (traceEvent.AnswerTopP is null || double.IsFinite(traceEvent.AnswerTopP.Value) && traceEvent.AnswerTopP is > 0 and <= 1) &&
        (traceEvent.ConversationHistoryTurns is null || traceEvent.ConversationHistoryTurns is >= 2 and <= 16 && traceEvent.ConversationHistoryTurns % 2 == 0) &&
        (traceEvent.ConversationHistoryCharacters is null || traceEvent.ConversationHistoryCharacters is >= 256 and <= 8000) &&
        (traceEvent.ContextMessageCount is null || traceEvent.ContextMessageCount is >= 0 and <= SparkyApiRequest.MaximumContextTurns && traceEvent.ContextMessageCount % 2 == 0) &&
        (traceEvent.ContextCharacterCount is null || traceEvent.ContextCharacterCount is >= 0 and <= 12_000) &&
        IsOptionalCategory(traceEvent.EffectiveIntent, 64) &&
        IsOptionalCategory(traceEvent.EffectiveFocus, 64) &&
        IsOptionalCategory(traceEvent.ResolverCategory, 64) &&
        IsOptionalCategory(traceEvent.ResolverOutcome, 64) &&
        (traceEvent.ComparisonSubjectCount is null || traceEvent.ComparisonSubjectCount is >= 0 and <= 8) &&
        IsOptionalCategory(traceEvent.ComparisonAttributeCategory, 64) &&
        IsOptionalCategory(traceEvent.ComparisonResultCategory, 64) &&
        IsContentTextValid(traceEvent.Question, 2000) &&
        IsContentTextValid(traceEvent.Answer, 2000) &&
        IsContextTurnsValid(traceEvent.ContextTurns) &&
        IsContentTextValid(traceEvent.ContextSummary, SparkyApiRequest.MaximumSummaryCharacters) &&
        IsPendingClarificationValid(traceEvent.PendingClarification) &&
        IsComparisonDiagnosticsValid(traceEvent.ComparisonDiagnostics);

    internal static bool IsAllowedSource(string source, bool allowAiSource) =>
        ExternalSources.Contains(source) || allowAiSource && source == "AI";

    internal static bool IsAllowedOutcome(string outcome) => Outcomes.Contains(outcome);

    public static bool IsValidFeedback(SparkyTraceFeedback feedback) =>
        feedback.FeedbackId != Guid.Empty &&
        feedback.RequestId != Guid.Empty &&
        FeedbackLabels.Contains(feedback.Label) &&
        IsOptionalTextValid(feedback.Note, 500) &&
        feedback.CreatedAtUtc.Offset == TimeSpan.Zero;

    internal static bool IsAllowedFeedbackLabel(string label) => FeedbackLabels.Contains(label);

    internal static bool IsValidEvaluationExpectation(
        string? expectedIntent,
        IReadOnlyList<string>? expectedProperties) =>
        (expectedIntent is null || Enum.TryParse<SparkyIntent>(expectedIntent, ignoreCase: false, out var intent) && Enum.IsDefined(intent)) &&
        expectedProperties is { Count: <= 16 } &&
        expectedProperties.All(property => !string.IsNullOrWhiteSpace(property) &&
            property.Length <= 80 && EvaluationPropertyPattern().IsMatch(property));

    private static readonly HashSet<string> FeedbackLabels = new(StringComparer.Ordinal)
    {
        "wrong_route",
        "missed_entity",
        "retrieval_miss",
        "incorrect_fact",
        "language",
        "style",
        "context",
        "delivery",
        "infrastructure"
    };

    private static bool IsValid(SparkyTraceEventRequest traceEvent, bool allowAiSource) =>
        IsValid(
            traceEvent.EventId,
            traceEvent.RequestId,
            traceEvent.Source,
            traceEvent.Stage,
            traceEvent.Outcome,
            traceEvent.RecordedAtUtc,
            traceEvent.ElapsedMilliseconds,
            traceEvent.DetailCode,
            allowAiSource);

    private static bool IsValid(SparkyTraceEvent traceEvent, bool allowAiSource) =>
        IsValid(
            traceEvent.EventId,
            traceEvent.RequestId,
            traceEvent.Source,
            traceEvent.Stage,
            traceEvent.Outcome,
            traceEvent.RecordedAtUtc,
            traceEvent.ElapsedMilliseconds,
            traceEvent.DetailCode,
            allowAiSource);

    private static bool IsValid(
        Guid eventId,
        Guid requestId,
        string? source,
        string? stage,
        string? outcome,
        DateTimeOffset recordedAtUtc,
        long? elapsedMilliseconds,
        string? detailCode,
        bool allowAiSource) =>
        eventId != Guid.Empty &&
        requestId != Guid.Empty &&
        source is not null && IsAllowedSource(source, allowAiSource) &&
        stage is not null && Stages.Contains(stage) &&
        outcome is not null && Outcomes.Contains(outcome) &&
        recordedAtUtc.Offset == TimeSpan.Zero &&
        recordedAtUtc.Year is >= 2000 and <= 9999 &&
        elapsedMilliseconds is null or >= 0 and <= 86_400_000 &&
        IsOptionalTextValid(detailCode, MaxDetailCodeCharacters) &&
        (detailCode is null || DetailCodePattern().IsMatch(detailCode));

    private static bool IsOptionalTextValid(string? value, int maxCharacters) =>
        value is null || value.Length <= maxCharacters && !value.Any(char.IsControl);

    internal static bool IsContentTextValid(string? value, int maxCharacters) =>
        value is null || value.Length <= maxCharacters &&
        !value.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t');

    private static bool IsOptionalCategory(string? value, int maxCharacters) =>
        value is null || value.Length <= maxCharacters && CategoryPattern().IsMatch(value);

    private static bool IsContextTurnsValid(IReadOnlyList<SparkyConversationTurn>? turns)
    {
        if (turns is null)
            return true;
        if (turns.Count > SparkyApiRequest.MaximumContextTurns || turns.Count % 2 != 0)
            return false;

        int characters = 0;
        for (int index = 0; index < turns.Count; index += 2)
        {
            SparkyConversationTurn user = turns[index];
            SparkyConversationTurn assistant = turns[index + 1];
            if (user is null || assistant is null || user.Role != "user" || assistant.Role != "assistant" ||
                string.IsNullOrWhiteSpace(user.Content) || string.IsNullOrWhiteSpace(assistant.Content) ||
                !IsContentTextValid(user.Content, SparkyApiRequest.MaximumTurnCharacters) ||
                !IsContentTextValid(assistant.Content, SparkyApiRequest.MaximumTurnCharacters))
            {
                return false;
            }

            characters += user.Content.Length + assistant.Content.Length;
        }

        return characters <= 12_000;
    }

    private static bool IsPendingClarificationValid(SparkyPendingClarification? pending) =>
        pending is null ||
        IsContentTextValid(pending.OriginalQuestion, SparkyApiRequest.MaximumTurnCharacters) &&
        IsOptionalTextValid(pending.RequestedAnswerType, 80) &&
        pending.CandidateNames is { Count: > 0 and <= SparkyApiRequest.MaximumCandidates } &&
        pending.CandidateNames.All(candidate => !string.IsNullOrWhiteSpace(candidate) &&
            IsContentTextValid(candidate, SparkyApiRequest.MaximumCandidateCharacters));

    private static bool IsComparisonDiagnosticsValid(SparkyComparisonDiagnostics? diagnostics)
    {
        if (diagnostics is null)
            return true;
        if (diagnostics.Domain is not (null or "Item" or "Robot") ||
            diagnostics.Subjects is null || diagnostics.Subjects.Count > 8 ||
            !IsContentTextValid(diagnostics.MetricPhrase, 80) ||
            !IsOptionalCategory(diagnostics.ResultCategory, 64) ||
            diagnostics.Facts is null || diagnostics.Facts.Count > 32)
        {
            return false;
        }

        if (diagnostics.Subjects.Any(subject => subject is null ||
            !IsContentTextValid(subject.DisplayName, 120) ||
            subject.ReferenceSource is not ("current_question" or "conversation_context")))
        {
            return false;
        }

        var names = diagnostics.Subjects.Select(subject => subject.DisplayName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return diagnostics.Facts.All(fact => fact is not null &&
            IsContentTextValid(fact.AttributeLabel, 120) &&
            fact.Values is { Count: 2 } && fact.Values.All(value => IsContentTextValid(value, 64)) &&
            IsContentTextValid(fact.Unit, 32) &&
            IsNonNegativeDecimal(fact.Difference) &&
            (fact.LeadingSubjectName is null || names.Contains(fact.LeadingSubjectName)));
    }

    private static bool IsNonNegativeDecimal(string value) =>
        IsContentTextValid(value, 64) &&
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed) && parsed >= 0;

    private static bool IsStringListValid(IReadOnlyList<string>? values, int maxCount, int maxCharacters) =>
        values is null || values.Count <= maxCount &&
        values.All(value => !string.IsNullOrWhiteSpace(value) && IsOptionalTextValid(value, maxCharacters));

    [GeneratedRegex("^[a-z0-9_.:-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DetailCodePattern();

    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,80}$", RegexOptions.CultureInvariant)]
    private static partial Regex EvaluationPropertyPattern();

    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryPattern();
}
