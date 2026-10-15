using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Perpetuum.ItemStatsExporter;

internal static class SparkyLiveKnowledgePassageBuilder
{
    private const string CurrentSourceId = "live-season-current";

    public static SparkyKnowledgeSupplement BuildSupplement(SparkyLiveKnowledgeProfileState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var passages = new List<SparkyKnowledgePassage>();

        if (state.Current is { } current)
        {
            AddCurrentPassages(passages, current);
        }

        SparkySeasonFinalRecord? mostRecentFinal = state.FinalizedSeasons
            .OrderByDescending(record => record.Finalization.EndTimeUtc)
            .ThenByDescending(record => record.SeasonId)
            .FirstOrDefault();
        foreach (SparkySeasonFinalRecord final in state.FinalizedSeasons
                     .OrderByDescending(record => record.Finalization.EndTimeUtc)
                     .ThenByDescending(record => record.SeasonId))
        {
            AddFinalPassages(passages, final, final == mostRecentFinal);
        }

        return new SparkyKnowledgeSupplement(passages, SparkyKnowledgeIndexBuilder.BuildSearchIndex(passages));
    }

    private static void AddCurrentPassages(List<SparkyKnowledgePassage> passages, SparkyLiveKnowledgeUpdate current)
    {
        string revision = current.ContentHash;
        SparkySeasonCurrentState state = current.Current!;
        if (state.ActiveSeason is not { } season)
        {
            Add("The game is between seasons. There is no active season and no current season leaderboard.");
            return;
        }

        string seasonName = Quote(season.Name, 120);
        Add($"Current season: {seasonName}. Scoring mode: {season.ScoringMode}. Starts {season.StartTimeUtc:O}; scheduled end {season.EndTimeUtc:O}. Description: {Quote(season.Description, 900)}");

        if (state.CurrentTopTen.Count == 0)
        {
            Add($"The current public leaderboard for {seasonName} has no eligible participants.");
        }
        else
        {
            foreach (SparkySeasonStanding standing in state.CurrentTopTen)
            {
                string participant = Quote(standing.DisplayName, 100);
                string leaderDescription = standing.Rank == 1
                    ? $" Current points leader: {participant} leads the standings."
                    : string.Empty;
                Add($"Current public leaderboard for {seasonName}: rank {standing.Rank}, {participant}, {standing.TotalPoints.ToString("G", System.Globalization.CultureInfo.InvariantCulture)} points.{leaderDescription}");
            }
        }

        foreach (SparkySeasonActivityRate rate in season.ActivityRates)
        {
            Add($"Current season {seasonName} activity scoring: {rate.ActivityType} earns {rate.PointsPerUnit.ToString("G", System.Globalization.CultureInfo.InvariantCulture)} points per {rate.UnitScale} unit(s).");
        }

        foreach (SparkySeasonObjective objective in season.Objectives)
        {
            Add($"Current season {seasonName} objective {Quote(objective.Name, 120)}: {Quote(objective.Description, 900)} Activity: {objective.ActivityType}; target: {objective.TargetValue}; bonus: {objective.BonusPoints} points; daily: {objective.IsDaily}.");
        }

        foreach (SparkySeasonTier tier in season.Tiers)
        {
            Add($"Current season {seasonName} tier {tier.TierNumber}, {Quote(tier.TierName, 120)}: requires {tier.PointsRequired} points.");
        }

        void Add(string text) => AddPassage(passages, CurrentSourceId, revision, text);
    }

    private static void AddFinalPassages(
        List<SparkyKnowledgePassage> passages,
        SparkySeasonFinalRecord record,
        bool isMostRecentFinal)
    {
        string sourceId = $"live-season-final-{record.SeasonId}";
        string revision = $"{record.Revision}:{record.ContentHash}";
        string seasonName = Quote(record.Finalization.Name, 120);
        string header = isMostRecentFinal
            ? $"Most recent past (previous) season final standings for season {record.SeasonId}, {seasonName}; ended {record.Finalization.EndTimeUtc:O}."
            : $"Historical final standings for season {record.SeasonId}, {seasonName}; ended {record.Finalization.EndTimeUtc:O}.";
        if (record.Finalization.TopTen.Count == 0)
        {
            AddPassage(passages, sourceId, revision,
                $"{header} No public {(isMostRecentFinal ? "past season" : "final")} standings were recorded.");
            return;
        }

        foreach (SparkySeasonStanding standing in record.Finalization.TopTen)
        {
            string rankDescription = standing.Rank == 1
                ? isMostRecentFinal
                    ? "Most recent past season leader, champion, and winner (rank 1)"
                    : "Champion and winner (rank 1)"
                : $"Rank {standing.Rank}";
            AddPassage(passages, sourceId, revision,
                $"{header} {rankDescription}: {Quote(standing.DisplayName, 100)}, {standing.TotalPoints.ToString("G", System.Globalization.CultureInfo.InvariantCulture)} points.");
        }
    }

    private static void AddPassage(List<SparkyKnowledgePassage> passages, string sourceId, string revision, string text)
    {
        if (text.Length > SparkyKnowledgeIndexLimits.MaxPassageCharacters)
        {
            throw new InvalidDataException("A generated live season passage exceeded its configured size limit.");
        }

        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        passages.Add(new SparkyKnowledgePassage(sourceId, "mechanics", revision, hash, text));
    }

    private static string Quote(string value, int maximumCharacters)
    {
        string bounded = value.Length <= maximumCharacters ? value : value[..maximumCharacters] + "…";
        return JsonSerializer.Serialize(bounded);
    }
}
