using System.Text.Json.Serialization;

namespace Perpetuum.ItemStatsExporter;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyTraceEventRequest(
    Guid EventId,
    Guid RequestId,
    string Source,
    string Stage,
    string Outcome,
    DateTimeOffset RecordedAtUtc,
    long? ElapsedMilliseconds = null,
    string? DetailCode = null);

internal sealed record SparkyTraceEvent(
    Guid EventId,
    Guid RequestId,
    string Source,
    string Stage,
    string Outcome,
    DateTimeOffset RecordedAtUtc,
    long? ElapsedMilliseconds = null,
    string? DetailCode = null,
    string? Intent = null,
    string? Focus = null,
    string? SearchQuery = null,
    IReadOnlyList<string>? EntityCandidates = null,
    IReadOnlyList<string>? SelectedSourceIds = null,
    string? ModelId = null,
    string? ConfigRevision = null,
    string? BundleVersion = null,
    string? Question = null,
    string? Answer = null,
    string? FinishStatus = null);

internal sealed record SparkyTraceAppendResult(
    IReadOnlyList<Guid> AcknowledgedEventIds,
    IReadOnlyList<Guid> ConflictingEventIds);

internal sealed record SparkyTraceTimeline(
    Guid RequestId,
    SparkyApiProfile Environment,
    IReadOnlyList<SparkyTraceEvent> Events);

internal sealed record SparkyTraceQuery(
    SparkyApiProfile Environment,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Source = null,
    string? Outcome = null,
    int Limit = 50,
    string? Label = null);

internal sealed record SparkyTraceSummary(
    Guid RequestId,
    SparkyApiProfile Environment,
    DateTimeOffset FirstRecordedAtUtc,
    DateTimeOffset LastRecordedAtUtc,
    string? Source,
    string? Outcome,
    string? Question,
    string? Answer,
    IReadOnlyList<string>? Labels = null);

internal sealed record SparkyTraceFeedback(
    Guid FeedbackId,
    Guid RequestId,
    string Label,
    string? Note,
    DateTimeOffset CreatedAtUtc);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyTraceFeedbackRequest(string Label, string? Note = null);

internal sealed record SparkyEvaluationExample(
    Guid ExampleId,
    Guid RequestId,
    string Question,
    string? ExpectedIntent,
    IReadOnlyList<string> ExpectedProperties,
    DateTimeOffset CreatedAtUtc);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyEvaluationExampleRequest(
    Guid RequestId,
    string? ExpectedIntent,
    IReadOnlyList<string>? ExpectedProperties);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyReplayRequest(
    SparkyApiProfile Environment,
    string DraftRevisionId,
    IReadOnlyList<Guid> ExampleIds);

internal sealed record SparkyReplayOutput(
    string Status,
    string? Intent,
    IReadOnlyList<string>? Entities,
    IReadOnlyList<string>? SelectedSourceIds,
    string? Answer);

internal sealed record SparkyReplayComparison(
    Guid ExampleId,
    string Question,
    string? ExpectedIntent,
    IReadOnlyList<string> ExpectedProperties,
    SparkyReplayOutput Active,
    SparkyReplayOutput Candidate);

internal sealed record SparkyReplayRun(
    Guid ReplayId,
    SparkyApiProfile Environment,
    string ActiveRevisionId,
    string CandidateRevisionId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<SparkyReplayComparison> Results,
    bool ContentExpired = false);

internal sealed record SparkyTraceRetentionResult(
    int ContentRowsCleared,
    int MetadataRowsDeleted,
    int ReplayContentRowsCleared = 0,
    int EvaluationQuestionsCleared = 0,
    int FeedbackNotesCleared = 0);
