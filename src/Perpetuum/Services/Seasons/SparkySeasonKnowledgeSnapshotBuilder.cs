using Perpetuum.Services.Sparky;
using System.Text.Json;

namespace Perpetuum.Services.Seasons;

internal static class SparkySeasonKnowledgeSnapshotBuilder
{
    private const int MaximumActivityRates = 32;
    private const int MaximumObjectives = 128;
    private const int MaximumTiers = 32;
    private const int MaximumStandings = 10;
    private const int MaximumRequestBodyBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static SparkyLiveKnowledgeUpdate BuildCurrent(
        Season? season,
        IReadOnlyList<SeasonActivityRate> rates,
        IReadOnlyList<SeasonObjective> objectives,
        IReadOnlyList<SeasonTier> tiers,
        IReadOnlyList<SparkySeasonStanding> currentTopTen,
        DateTimeOffset capturedAtUtc,
        string profile)
    {
        ArgumentNullException.ThrowIfNull(rates);
        ArgumentNullException.ThrowIfNull(objectives);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentNullException.ThrowIfNull(currentTopTen);
        ValidateProfile(profile);

        SparkySeasonFacts? facts = season is null
            ? null
            : new SparkySeasonFacts(
                season.Id,
                CleanText(season.Name, 120, required: true),
                CleanText(season.Description, 2000),
                AsUtc(season.StartTime),
                AsUtc(season.EndTime),
                season.ScoringMode.ToString(),
                rates
                    .Where(rate => rate.UnitScale > 0 && double.IsFinite(rate.PointsPerUnit) && rate.PointsPerUnit >= 0)
                    .OrderBy(rate => rate.ActivityType)
                    .ThenBy(rate => rate.UnitScale)
                    .ThenBy(rate => rate.PointsPerUnit)
                    .Take(MaximumActivityRates)
                    .Select(rate => new SparkySeasonActivityRate(rate.ActivityType.ToString(), rate.PointsPerUnit, rate.UnitScale))
                    .ToArray(),
                objectives
                    .Where(objective => objective.TargetValue >= 0 && objective.BonusPoints >= 0)
                    .OrderBy(objective => objective.DisplayOrder)
                    .ThenBy(objective => objective.Name, StringComparer.Ordinal)
                    .Take(MaximumObjectives)
                    .Select(objective => new SparkySeasonObjective(
                        CleanText(objective.Name, 120, required: true),
                        CleanText(objective.Description, 2000),
                        objective.ActivityType.ToString(),
                        objective.TargetValue,
                        objective.BonusPoints,
                        objective.IsDaily))
                    .ToArray(),
                tiers
                    .Where(tier => tier.TierNumber > 0 && tier.PointsRequired >= 0)
                    .OrderBy(tier => tier.TierNumber)
                    .ThenBy(tier => tier.TierName, StringComparer.Ordinal)
                    .Take(MaximumTiers)
                    .Select(tier => new SparkySeasonTier(
                        tier.TierNumber,
                        CleanText(tier.TierName, 120, required: true),
                        tier.PointsRequired))
                    .ToArray());

        var current = new SparkySeasonCurrentState(
            facts,
            season is null ? [] : PreserveRankingOrder(currentTopTen));
        var update = new SparkyLiveKnowledgeUpdate(
            SchemaVersion: 1,
            Provider: "seasons",
            Profile: profile,
            Operation: "replace-current",
            CapturedAtUtc: capturedAtUtc.ToUniversalTime(),
            ContentHash: new string('0', 64),
            Current: current,
            Finalization: null);
        update = FitCurrentUpdateToRequestLimit(update);
        return update with { ContentHash = SparkyLiveKnowledgeHash.ComputeCurrent(update) };
    }

    public static SparkyLiveKnowledgeUpdate BuildFinal(
        Season season,
        IReadOnlyList<SparkySeasonStanding> finalTopTen,
        DateTimeOffset capturedAtUtc,
        string profile)
    {
        ArgumentNullException.ThrowIfNull(season);
        ArgumentNullException.ThrowIfNull(finalTopTen);
        ValidateProfile(profile);

        var finalization = new SparkySeasonFinalization(
            season.Id,
            CleanText(season.Name, 120, required: true),
            EarlierOf(AsUtc(season.EndTime), capturedAtUtc.ToUniversalTime()),
            PreserveRankingOrder(finalTopTen));
        var update = new SparkyLiveKnowledgeUpdate(
            SchemaVersion: 1,
            Provider: "seasons",
            Profile: profile,
            Operation: "finalize-season",
            CapturedAtUtc: capturedAtUtc.ToUniversalTime(),
            ContentHash: new string('0', 64),
            Current: null,
            Finalization: finalization);
        return update with { ContentHash = SparkyLiveKnowledgeHash.ComputeFinalization(update) };
    }

    private static IReadOnlyList<SparkySeasonStanding> PreserveRankingOrder(IReadOnlyList<SparkySeasonStanding> standings) =>
        standings
            .Where(standing => standing is not null &&
                               standing.Rank > 0 &&
                               !string.IsNullOrWhiteSpace(standing.DisplayName) &&
                               double.IsFinite(standing.TotalPoints) && standing.TotalPoints >= 0)
            .OrderBy(standing => standing.Rank)
            .Select(standing => new
            {
                Name = CleanText(standing.DisplayName, 100, required: true),
                standing.TotalPoints
            })
            .Take(MaximumStandings)
            .Select((standing, index) => new SparkySeasonStanding(index + 1, standing.Name, standing.TotalPoints))
            .ToArray();

    private static SparkyLiveKnowledgeUpdate FitCurrentUpdateToRequestLimit(SparkyLiveKnowledgeUpdate update)
    {
        if (GetSerializedSize(update) <= MaximumRequestBodyBytes || update.Current?.ActiveSeason is not { } season)
            return update;

        int low = 0;
        int high = Math.Max(season.Description.Length,
            season.Objectives.Count == 0 ? 0 : season.Objectives.Max(objective => objective.Description.Length));
        SparkyLiveKnowledgeUpdate? best = null;

        while (low <= high)
        {
            int descriptionLimit = low + (high - low) / 2;
            var boundedSeason = season with
            {
                Description = TruncateDescription(season.Description, descriptionLimit),
                Objectives = season.Objectives
                    .Select(objective => objective with
                    {
                        Description = TruncateDescription(objective.Description, descriptionLimit)
                    })
                    .ToArray()
            };
            var candidate = update with
            {
                Current = update.Current with { ActiveSeason = boundedSeason }
            };

            if (GetSerializedSize(candidate) <= MaximumRequestBodyBytes)
            {
                best = candidate;
                low = descriptionLimit + 1;
            }
            else
            {
                high = descriptionLimit - 1;
            }
        }

        if (best is null)
            throw new InvalidDataException("Public season labels exceed Sparky's live-knowledge request size limit.");

        return best;
    }

    private static int GetSerializedSize(SparkyLiveKnowledgeUpdate update) =>
        JsonSerializer.SerializeToUtf8Bytes(update, JsonOptions).Length;

    private static string TruncateDescription(string value, int maximumLength)
    {
        if (value.Length <= maximumLength)
            return value;
        if (maximumLength <= 1)
            return maximumLength == 0 ? string.Empty : "…";

        int contentLength = maximumLength - 1;
        if (contentLength < value.Length && char.IsHighSurrogate(value[contentLength - 1]) && char.IsLowSurrogate(value[contentLength]))
            contentLength--;
        return value[..contentLength] + "…";
    }

    private static DateTimeOffset EarlierOf(DateTimeOffset first, DateTimeOffset second) => first <= second ? first : second;

    private static string CleanText(string? value, int maximumLength, bool required = false)
    {
        string cleaned = string.Concat((value ?? string.Empty).Where(character => !char.IsControl(character))).Trim();
        if (cleaned.Length > maximumLength)
            cleaned = cleaned[..maximumLength];
        if (required && cleaned.Length == 0)
            throw new InvalidDataException("A required public season label is empty.");
        return cleaned;
    }

    private static DateTimeOffset AsUtc(DateTime value)
    {
        DateTime utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return new DateTimeOffset(utc);
    }

    private static void ValidateProfile(string profile)
    {
        if (profile is not ("dev" or "live"))
            throw new ArgumentOutOfRangeException(nameof(profile), "The Sparky season profile must be dev or live.");
    }
}
