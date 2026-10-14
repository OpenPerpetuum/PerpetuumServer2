using System.Text.Json.Serialization;

namespace Perpetuum.Services.Sparky;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyTraceEvent(
    Guid EventId,
    Guid RequestId,
    string Source,
    string Stage,
    string Outcome,
    DateTimeOffset RecordedAtUtc,
    long? ElapsedMilliseconds = null,
    string? DetailCode = null)
{
    private static readonly HashSet<string> Sources = new(StringComparer.Ordinal)
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

    [JsonIgnore]
    public bool IsValid =>
        EventId != Guid.Empty &&
        RequestId != Guid.Empty &&
        Sources.Contains(Source) &&
        Stages.Contains(Stage) &&
        Outcomes.Contains(Outcome) &&
        RecordedAtUtc.Offset == TimeSpan.Zero &&
        RecordedAtUtc.Year is >= 2000 and <= 9999 &&
        ElapsedMilliseconds is null or >= 0 and <= 86_400_000 &&
        (DetailCode is null ||
         DetailCode.Length is >= 1 and <= 64 &&
         DetailCode.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or ':' or '-'));

    public static SparkyTraceEvent Create(
        Guid requestId,
        string source,
        string stage,
        string outcome,
        long? elapsedMilliseconds = null,
        string? detailCode = null)
    {
        var traceEvent = new SparkyTraceEvent(
            Guid.NewGuid(),
            requestId,
            source,
            stage,
            outcome,
            DateTimeOffset.UtcNow,
            elapsedMilliseconds,
            detailCode);
        if (!traceEvent.IsValid)
        {
            throw new ArgumentException("The Sparky trace event does not match the upload contract.", nameof(source));
        }

        return traceEvent;
    }

    public static SparkyTraceEvent CreateGap(string source, string detailCode)
    {
        Guid eventId = Guid.NewGuid();
        return new SparkyTraceEvent(
            eventId,
            eventId,
            source,
            "trace_gap",
            "trace_gap",
            DateTimeOffset.UtcNow,
            DetailCode: detailCode);
    }
}

internal enum SparkyTraceAppendDisposition
{
    Added,
    Duplicate,
    CapacityExceeded
}
