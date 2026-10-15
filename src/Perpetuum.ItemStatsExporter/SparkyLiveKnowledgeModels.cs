using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Perpetuum.ItemStatsExporter;

// Keep this wire contract property-for-property in sync with the game models.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyLiveKnowledgeUpdate(
    int SchemaVersion,
    string Provider,
    string Profile,
    string Operation,
    DateTimeOffset CapturedAtUtc,
    string ContentHash,
    SparkySeasonCurrentState? Current,
    SparkySeasonFinalization? Finalization);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkySeasonCurrentState(
    SparkySeasonFacts? ActiveSeason,
    IReadOnlyList<SparkySeasonStanding> CurrentTopTen);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkySeasonFacts(
    int SeasonId,
    string Name,
    string Description,
    DateTimeOffset StartTimeUtc,
    DateTimeOffset EndTimeUtc,
    string ScoringMode,
    IReadOnlyList<SparkySeasonActivityRate> ActivityRates,
    IReadOnlyList<SparkySeasonObjective> Objectives,
    IReadOnlyList<SparkySeasonTier> Tiers);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkySeasonActivityRate(string ActivityType, double PointsPerUnit, int UnitScale);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkySeasonObjective(
    string Name,
    string Description,
    string ActivityType,
    long TargetValue,
    int BonusPoints,
    bool IsDaily);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkySeasonTier(int TierNumber, string TierName, int PointsRequired);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkySeasonStanding(int Rank, string DisplayName, double TotalPoints);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkySeasonFinalization(
    int SeasonId,
    string Name,
    DateTimeOffset EndTimeUtc,
    IReadOnlyList<SparkySeasonStanding> TopTen);

internal sealed record SparkyLiveKnowledgeValidationResult(
    bool IsValid,
    string? RejectionCode = null,
    SparkyLiveKnowledgeUpdate? Update = null);

internal sealed record SparkyLiveKnowledgeApplyResult(string Status, string ContentHash, int? Revision = null, string? Operation = null);

internal sealed record SparkySeasonFinalRecord(
    int SeasonId,
    int Revision,
    string ContentHash,
    DateTimeOffset CapturedAtUtc,
    SparkySeasonFinalization Finalization);

internal sealed record SparkyLiveKnowledgeProfileState(
    SparkyLiveKnowledgeUpdate? Current,
    IReadOnlyList<SparkySeasonFinalRecord> FinalizedSeasons);

internal sealed record SparkyKnowledgeSupplement(
    IReadOnlyList<SparkyKnowledgePassage> Passages,
    SparkySearchIndex SearchIndex);

internal sealed record SparkyLiveKnowledgeDiagnostics(
    string? Provider,
    int? SchemaVersion,
    string? ContentHash,
    double? CaptureAgeMinutes,
    string Freshness,
    int CompletedSeasonCount);

internal sealed record SparkyLiveKnowledgeReadView(
    SparkyKnowledgeSupplement Supplement,
    IReadOnlySet<string> ExcludedSourceIds,
    SparkyLiveKnowledgeDiagnostics Diagnostics,
    SparkyLiveKnowledgeProfileState? State = null);

internal static class SparkyLiveKnowledgeHash
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string ComputeCurrent(SparkyLiveKnowledgeUpdate update)
    {
        var payload = new CurrentHashPayload(update.SchemaVersion, update.Provider, update.Profile, update.Current);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions))).ToLowerInvariant();
    }

    public static string ComputeFinalization(SparkyLiveKnowledgeUpdate update)
    {
        var payload = new FinalHashPayload(update.SchemaVersion, update.Provider, update.Profile, update.Finalization);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions))).ToLowerInvariant();
    }

    private sealed record CurrentHashPayload(
        int SchemaVersion,
        string Provider,
        string Profile,
        SparkySeasonCurrentState? Current);

    private sealed record FinalHashPayload(
        int SchemaVersion,
        string Provider,
        string Profile,
        SparkySeasonFinalization? Finalization);
}
