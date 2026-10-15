using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyDiagnosticExport(
    Guid RequestId,
    SparkyApiProfile Environment,
    DateTimeOffset ExportedAtUtc,
    bool ContentIncluded,
    bool Truncated,
    IReadOnlyList<string> Feedback,
    IReadOnlyList<SparkyDiagnosticEvent> Events,
    int SchemaVersion = 1);

internal sealed record SparkyDiagnosticEvent(
    DateTimeOffset RecordedAtUtc,
    string Source,
    string Stage,
    string Outcome,
    long? ElapsedMilliseconds,
    string? DetailCode,
    string? Intent,
    string? Focus,
    IReadOnlyList<string>? SelectedSourceIds,
    string? ModelId,
    string? ConfigRevision,
    string? BundleVersion,
    double? AnswerTemperature,
    double? AnswerTopP,
    int? ConversationHistoryTurns,
    int? ConversationHistoryCharacters,
    string? Question,
    string? Answer,
    int? ContextMessageCount = null,
    int? ContextCharacterCount = null,
    bool? HasContextSummary = null,
    bool? HasPendingClarification = null,
    bool? ContextBudgetTrimmed = null,
    string? EffectiveIntent = null,
    string? EffectiveFocus = null,
    string? ResolverCategory = null,
    string? ResolverOutcome = null,
    int? ComparisonSubjectCount = null,
    string? ComparisonAttributeCategory = null,
    string? ComparisonResultCategory = null,
    string? SearchQuery = null,
    IReadOnlyList<string>? EntityCandidates = null,
    IReadOnlyList<SparkyConversationTurn>? ContextTurns = null,
    string? ContextSummary = null,
    SparkyPendingClarification? PendingClarification = null,
    SparkyComparisonDiagnostics? ComparisonDiagnostics = null,
    string? ParsedIntent = null,
    string? ParsedFocus = null);

internal static class SparkyDiagnosticExporter
{
    public const int MaximumEvents = 64;
    public const int MaximumExportBytes = 512 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 8
    };

    static SparkyDiagnosticExporter() => JsonOptions.Converters.Add(new JsonStringEnumConverter());

    public static string Serialize(
        SparkyTraceTimeline timeline,
        IReadOnlyList<string> feedback,
        bool includeContent)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        var selectedEvents = timeline.Events.TakeLast(MaximumEvents).ToArray();
        bool truncated = timeline.Events.Count > selectedEvents.Length;
        var events = selectedEvents.Select(traceEvent => new SparkyDiagnosticEvent(
            traceEvent.RecordedAtUtc,
            traceEvent.Source,
            traceEvent.Stage,
            traceEvent.Outcome,
            traceEvent.ElapsedMilliseconds,
            traceEvent.DetailCode,
            traceEvent.Intent,
            traceEvent.Focus,
            traceEvent.SelectedSourceIds?.Take(16).ToArray(),
            traceEvent.ModelId,
            traceEvent.ConfigRevision,
            traceEvent.BundleVersion,
            traceEvent.AnswerTemperature,
            traceEvent.AnswerTopP,
            traceEvent.ConversationHistoryTurns,
            traceEvent.ConversationHistoryCharacters,
            includeContent ? traceEvent.Question : null,
            includeContent ? traceEvent.Answer : null,
            traceEvent.ContextMessageCount,
            traceEvent.ContextCharacterCount,
            traceEvent.HasContextSummary,
            traceEvent.HasPendingClarification,
            traceEvent.ContextBudgetTrimmed,
            traceEvent.EffectiveIntent,
            traceEvent.EffectiveFocus,
            traceEvent.ResolverCategory,
            traceEvent.ResolverOutcome,
            traceEvent.ComparisonSubjectCount,
            traceEvent.ComparisonAttributeCategory,
            traceEvent.ComparisonResultCategory,
            includeContent ? traceEvent.SearchQuery : null,
            includeContent ? traceEvent.EntityCandidates?.Take(8).ToArray() : null,
            includeContent ? traceEvent.ContextTurns : null,
            includeContent ? traceEvent.ContextSummary : null,
            includeContent ? traceEvent.PendingClarification : null,
            includeContent ? traceEvent.ComparisonDiagnostics : null,
            traceEvent.Intent,
            traceEvent.Focus)).ToArray();
        var document = new SparkyDiagnosticExport(
            timeline.RequestId,
            timeline.Environment,
            DateTimeOffset.UtcNow,
            includeContent,
            truncated,
            feedback.Take(16).ToArray(),
            events);
        string json = JsonSerializer.Serialize(document, JsonOptions);
        if (Encoding.UTF8.GetByteCount(json) > MaximumExportBytes)
            throw new InvalidDataException("The Sparky diagnostic export exceeds its size limit.");
        return json;
    }
}
