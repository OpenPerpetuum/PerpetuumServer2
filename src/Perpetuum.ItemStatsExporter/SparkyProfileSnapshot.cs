using System.Text.Json.Serialization;
using Perpetuum.Services.Sparky;

namespace Perpetuum.ItemStatsExporter;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyProfileDraft(
    int SchemaVersion,
    string ModelId,
    int MaxAnswerCharacters,
    int MaxCompletionTokens,
    int InferenceTimeoutSeconds,
    int MaxConcurrentRequests,
    bool CaptureContent,
    int ContentRetentionDays,
    int MetadataRetentionDays,
    string PersonaStyle,
    SparkyRetrievalSettings Retrieval,
    IReadOnlyList<SparkyRouteAlias> RouteAliases,
    string BundleVersion,
    int ConversationHistoryTurns = 8,
    int ConversationHistoryCharacters = 4000,
    double AnswerTemperature = 0.2,
    double AnswerTopP = 0.9,
    SparkyLiveKnowledgeSettings? LiveKnowledge = null)
{
    public static SparkyProfileDraft CreateDefault(
        string modelId,
        string bundleVersion,
        int maxAnswerCharacters,
        int maxCompletionTokens,
        int timeoutSeconds,
        int maxConcurrentRequests,
        bool captureContent,
        int contentRetentionDays,
        int metadataRetentionDays) => new(
            SchemaVersion: 2,
            ModelId: modelId,
            MaxAnswerCharacters: Math.Min(maxAnswerCharacters, 2000),
            MaxCompletionTokens: Math.Min(maxCompletionTokens, 4096),
            InferenceTimeoutSeconds: Math.Clamp(timeoutSeconds, 5, 300),
            MaxConcurrentRequests: Math.Max(1, maxConcurrentRequests),
            CaptureContent: captureContent,
            ContentRetentionDays: contentRetentionDays,
            MetadataRetentionDays: metadataRetentionDays,
            PersonaStyle: "Clear, calm, practical, and friendly. Use dry humor only when it fits.",
            Retrieval: new SparkyRetrievalSettings(5, 4200, 0.25),
            RouteAliases: [],
            BundleVersion: bundleVersion,
            ConversationHistoryTurns: 8,
            ConversationHistoryCharacters: 4000,
            AnswerTemperature: 0.2,
            AnswerTopP: 0.9,
            LiveKnowledge: new SparkyLiveKnowledgeSettings());
}

internal sealed record SparkyLiveKnowledgeSettings(int CurrentSnapshotMaxAgeMinutes = 15);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyRetrievalSettings(
    int MaxPassages,
    int MaxCharacters,
    double MinimumScore,
    IReadOnlyList<string>? SourceCategories = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyRouteAlias(
    string Phrase,
    SparkyIntent Intent,
    int Priority = 0,
    int? LanguageId = null,
    IReadOnlyList<string>? ExcludedPhrases = null);

internal sealed record SparkyProfileSnapshot(
    SparkyApiProfile Profile,
    string RevisionId,
    DateTimeOffset ActivatedAtUtc,
    SparkyProfileDraft Configuration,
    SparkyKnowledgeBundle Bundle)
{
    public string BundleVersion => Configuration.BundleVersion;

    public SparkyServiceOptions ApplyTo(SparkyServiceOptions defaults) => defaults with
    {
        ModelId = Configuration.ModelId,
        MaxAnswerCharacters = Configuration.MaxAnswerCharacters,
        MaxCompletionTokens = Configuration.MaxCompletionTokens,
        InferenceTimeout = TimeSpan.FromSeconds(Configuration.InferenceTimeoutSeconds),
        MaxConcurrentRequests = Configuration.MaxConcurrentRequests,
        ConversationHistoryTurns = Configuration.ConversationHistoryTurns,
        ConversationHistoryCharacters = Configuration.ConversationHistoryCharacters,
        AnswerTemperature = Configuration.AnswerTemperature,
        AnswerTopP = Configuration.AnswerTopP
    };
}

internal sealed record SparkyProfileSummary(
    string Profile,
    bool Enabled,
    string? ActiveRevisionId,
    string? DraftRevisionId,
    string? BundleVersion,
    bool? CaptureContent);

internal sealed record SparkyProfileRevisionRecord(
    SparkyApiProfile Profile,
    string RevisionId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ActivatedAtUtc,
    string ConfigurationJson,
    string BundleVersion,
    bool IsActive,
    bool IsDraft);

internal sealed record SparkyProfileAuditRecord(
    string AuditId,
    SparkyApiProfile Profile,
    string Actor,
    string Action,
    string? SourceRevisionId,
    string Result,
    IReadOnlyList<string>? ChangedFields,
    string? Reason,
    DateTimeOffset CreatedAtUtc);

internal sealed record SparkyBundleInfo(string Version, string FileName, long SizeBytes, DateTimeOffset StagedAtUtc);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyProfileDraftRequest(SparkyProfileDraft Configuration, string? Reason = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyProfileActivationRequest(string RevisionId, string? Reason = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyProfilePromotionRequest(string SourceRevisionId, string? Reason = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyProfileRollbackRequest(string? Reason = null);
