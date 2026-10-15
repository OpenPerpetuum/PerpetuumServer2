using System.Security.Cryptography;
using System.Text.Json;

namespace Perpetuum.Services.Sparky;

// Keep this wire contract property-for-property in sync with the exporter models.
internal sealed record SparkyLiveKnowledgeUpdate(
    int SchemaVersion,
    string Provider,
    string Profile,
    string Operation,
    DateTimeOffset CapturedAtUtc,
    string ContentHash,
    SparkySeasonCurrentState? Current,
    SparkySeasonFinalization? Finalization);

internal sealed record SparkySeasonCurrentState(
    SparkySeasonFacts? ActiveSeason,
    IReadOnlyList<SparkySeasonStanding> CurrentTopTen);

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

internal sealed record SparkySeasonActivityRate(string ActivityType, double PointsPerUnit, int UnitScale);

internal sealed record SparkySeasonObjective(
    string Name,
    string Description,
    string ActivityType,
    long TargetValue,
    int BonusPoints,
    bool IsDaily);

internal sealed record SparkySeasonTier(int TierNumber, string TierName, int PointsRequired);

internal sealed record SparkySeasonStanding(int Rank, string DisplayName, double TotalPoints);

internal sealed record SparkySeasonFinalization(
    int SeasonId,
    string Name,
    DateTimeOffset EndTimeUtc,
    IReadOnlyList<SparkySeasonStanding> TopTen);

internal static class SparkyLiveKnowledgeHash
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string ComputeCurrent(SparkyLiveKnowledgeUpdate update)
    {
        var payload = new CurrentHashPayload(update.SchemaVersion, update.Provider, update.Profile, update.Current);
        return Hash(payload);
    }

    public static string ComputeFinalization(SparkyLiveKnowledgeUpdate update)
    {
        var payload = new FinalHashPayload(update.SchemaVersion, update.Provider, update.Profile, update.Finalization);
        return Hash(payload);
    }

    private static string Hash<T>(T payload) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions))).ToLowerInvariant();

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
