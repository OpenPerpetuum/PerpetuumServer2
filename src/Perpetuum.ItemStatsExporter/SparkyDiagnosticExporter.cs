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
    IReadOnlyList<SparkyDiagnosticEvent> Events);

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
    string? Question,
    string? Answer);

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
            includeContent ? traceEvent.Question : null,
            includeContent ? traceEvent.Answer : null)).ToArray();
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
