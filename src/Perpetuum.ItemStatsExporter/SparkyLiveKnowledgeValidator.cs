using System.Text.Json;
using System.Text.Json.Serialization;
using Perpetuum.Services.Seasons;

namespace Perpetuum.ItemStatsExporter;

internal static class SparkyLiveKnowledgeValidator
{
    public const int SchemaVersion = 1;
    public const int MaxBodyBytes = 256 * 1024;
    public const int MaxActivityRates = 32;
    public const int MaxObjectives = 128;
    public const int MaxTiers = 32;
    public const int MaxStandings = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 12,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string ComputeCurrentHash(SparkyLiveKnowledgeUpdate update) => SparkyLiveKnowledgeHash.ComputeCurrent(update);

    public static string ComputeFinalizationHash(SparkyLiveKnowledgeUpdate update) => SparkyLiveKnowledgeHash.ComputeFinalization(update);

    public static bool TryDeserializeUpdate(
        ReadOnlySpan<byte> json,
        SparkyApiProfile authenticatedProfile,
        DateTimeOffset nowUtc,
        out SparkyLiveKnowledgeUpdate? update,
        out string? rejectionCode)
    {
        update = null;
        rejectionCode = null;
        if (json.IsEmpty || json.Length > MaxBodyBytes)
        {
            rejectionCode = json.Length > MaxBodyBytes ? "payload_too_large" : "invalid_payload";
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<SparkyLiveKnowledgeUpdate>(json, JsonOptions);
            if (parsed is null)
            {
                rejectionCode = "invalid_payload";
                return false;
            }

            var validation = ValidateUpdate(parsed, authenticatedProfile, nowUtc);
            rejectionCode = validation.RejectionCode;
            update = validation.IsValid ? validation.Update : null;
            return validation.IsValid;
        }
        catch (JsonException)
        {
            rejectionCode = "invalid_payload";
            return false;
        }
    }

    public static SparkyLiveKnowledgeValidationResult ValidateUpdate(
        SparkyLiveKnowledgeUpdate? update,
        SparkyApiProfile authenticatedProfile,
        DateTimeOffset nowUtc)
    {
        if (update is null)
        {
            return Reject("invalid_payload");
        }

        if (update.SchemaVersion != SchemaVersion)
        {
            return Reject("invalid_schema");
        }

        var expectedProfile = authenticatedProfile == SparkyApiProfile.Dev ? "dev" : "live";
        if (!string.Equals(update.Profile, expectedProfile, StringComparison.Ordinal))
        {
            return Reject("invalid_profile");
        }

        if (!string.Equals(update.Provider, "seasons", StringComparison.Ordinal) ||
            !IsUtc(update.CapturedAtUtc) ||
            update.CapturedAtUtc > nowUtc.ToUniversalTime().AddMinutes(5))
        {
            return Reject(!IsUtc(update.CapturedAtUtc) || update.CapturedAtUtc > nowUtc.ToUniversalTime().AddMinutes(5)
                ? "invalid_timestamp"
                : "invalid_payload");
        }

        if (!IsHash(update.ContentHash))
        {
            return Reject("invalid_hash");
        }

        if (!HasValidOperationShape(update))
        {
            return Reject("invalid_payload");
        }

        if (!HasValidContent(update))
        {
            return Reject("invalid_payload");
        }

        int payloadLength;
        try
        {
            payloadLength = JsonSerializer.SerializeToUtf8Bytes(update, JsonOptions).Length;
        }
        catch (JsonException)
        {
            return Reject("invalid_payload");
        }

        if (payloadLength > MaxBodyBytes)
        {
            return Reject("payload_too_large");
        }

        string expectedHash = update.Operation switch
        {
            "replace-current" => ComputeCurrentHash(update),
            "finalize-season" => ComputeFinalizationHash(update),
            "refresh-current" => update.ContentHash,
            _ => string.Empty
        };
        if (!string.Equals(update.ContentHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            return Reject("invalid_hash");
        }

        return new SparkyLiveKnowledgeValidationResult(true, Update: update);
    }

    private static bool HasValidOperationShape(SparkyLiveKnowledgeUpdate update) => update.Operation switch
    {
        "replace-current" => update.Current is not null && update.Finalization is null,
        "refresh-current" => update.Current is null && update.Finalization is null,
        "finalize-season" => update.Current is null && update.Finalization is not null,
        _ => false
    };

    private static bool HasValidContent(SparkyLiveKnowledgeUpdate update)
    {
        if (update.Operation == "refresh-current")
        {
            return true;
        }

        if (update.Operation == "replace-current")
        {
            var current = update.Current!;
            if (current.CurrentTopTen is null || current.CurrentTopTen.Count > MaxStandings ||
                !HasValidStandings(current.CurrentTopTen))
            {
                return false;
            }

            if (current.ActiveSeason is null)
            {
                return current.CurrentTopTen.Count == 0;
            }

            return HasValidFacts(current.ActiveSeason);
        }

        var finalization = update.Finalization!;
        return finalization.SeasonId > 0 &&
               IsValidName(finalization.Name, 120) &&
               IsUtc(finalization.EndTimeUtc) &&
               finalization.TopTen is not null &&
               finalization.TopTen.Count <= MaxStandings &&
               HasValidStandings(finalization.TopTen);
    }

    private static bool HasValidFacts(SparkySeasonFacts facts) =>
        facts.SeasonId > 0 &&
        IsValidName(facts.Name, 120) &&
        IsValidText(facts.Description, 2000) &&
        IsUtc(facts.StartTimeUtc) &&
        IsUtc(facts.EndTimeUtc) &&
        facts.EndTimeUtc > facts.StartTimeUtc &&
        IsDefinedEnum<SeasonScoringMode>(facts.ScoringMode) &&
        facts.ActivityRates is not null && facts.ActivityRates.Count <= MaxActivityRates && facts.ActivityRates.All(HasValidRate) &&
        facts.Objectives is not null && facts.Objectives.Count <= MaxObjectives && facts.Objectives.All(HasValidObjective) &&
        facts.Tiers is not null && facts.Tiers.Count <= MaxTiers && facts.Tiers.All(HasValidTier);

    private static bool HasValidRate(SparkySeasonActivityRate rate) =>
        rate is not null && IsDefinedEnum<SeasonActivityType>(rate.ActivityType) &&
        double.IsFinite(rate.PointsPerUnit) && rate.PointsPerUnit >= 0 && rate.UnitScale > 0;

    private static bool HasValidObjective(SparkySeasonObjective objective) =>
        objective is not null && IsValidName(objective.Name, 120) && IsValidText(objective.Description, 2000) &&
        IsDefinedEnum<SeasonActivityType>(objective.ActivityType) && objective.TargetValue >= 0 && objective.BonusPoints >= 0;

    private static bool HasValidTier(SparkySeasonTier tier) =>
        tier is not null && tier.TierNumber > 0 && IsValidName(tier.TierName, 120) && tier.PointsRequired >= 0;

    private static bool HasValidStandings(IReadOnlyList<SparkySeasonStanding> standings)
    {
        for (var index = 0; index < standings.Count; index++)
        {
            var standing = standings[index];
            if (standing is null || standing.Rank != index + 1 ||
                !IsValidName(standing.DisplayName, 100) ||
                !double.IsFinite(standing.TotalPoints) || standing.TotalPoints < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDefinedEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed);

    private static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;

    private static bool IsHash(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsValidName(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && IsValidText(value, maxLength);

    private static bool IsValidText(string? value, int maxLength) =>
        value is not null && value.Length <= maxLength && value.All(character => !char.IsControl(character));

    private static SparkyLiveKnowledgeValidationResult Reject(string code) => new(false, code);
}
