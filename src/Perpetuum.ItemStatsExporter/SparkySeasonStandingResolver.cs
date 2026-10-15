using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace Perpetuum.ItemStatsExporter;

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum SparkySeasonStandingPeriod
{
    Current,
    MostRecentFinal
}

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum SparkySeasonStandingStatus
{
    LeaderFound,
    NoActiveSeason,
    NoEligibleParticipants,
    NoFinalizedSeason,
    NoFinalStandings
}

internal sealed record SparkySeasonStandingAnswerFacts(
    SparkySeasonStandingPeriod Period,
    SparkySeasonStandingStatus Status,
    string? SeasonName,
    string? LeaderName,
    double? Points,
    string? SourceId);

internal static class SparkySeasonStandingResolver
{
    private static readonly Regex StandingQuestion = new(
        @"\b(?:leaders?|leading|led|champion|winner|top\s+(?:player|scorer|participant)|rank\s*(?:one|1))\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CurrentSeasonQuestion = new(
        @"\b(?:current(?:ly)?|active|ongoing|present|this|live)\s+(?:live\s+)?season\b|\bseason\b.{0,32}\b(?:current(?:ly)?|active|ongoing|present|this|live)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex PastSeasonQuestion = new(
        @"\b(?:past|previous|last|latest|most\s+recent|preceding)\s+season\b|\bseason\b.{0,32}\b(?:past|previous|last|latest|most\s+recent|preceding)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryResolve(
        string playerQuestion,
        string englishQuery,
        SparkyLiveKnowledgeReadView? liveKnowledge,
        out SparkySeasonStandingAnswerFacts facts)
    {
        facts = null!;
        if (liveKnowledge?.State is not { } state ||
            !StandingQuestion.IsMatch(playerQuestion) && !StandingQuestion.IsMatch(englishQuery))
        {
            return false;
        }

        bool asksCurrent = CurrentSeasonQuestion.IsMatch(playerQuestion) || CurrentSeasonQuestion.IsMatch(englishQuery);
        bool asksPast = PastSeasonQuestion.IsMatch(playerQuestion) || PastSeasonQuestion.IsMatch(englishQuery);
        if (asksCurrent == asksPast)
        {
            return false;
        }

        if (asksCurrent)
        {
            if (liveKnowledge.Diagnostics.Freshness != "fresh" ||
                liveKnowledge.ExcludedSourceIds.Contains("live-season-current") ||
                state.Current?.Current is not { } current)
            {
                return false;
            }

            if (current.ActiveSeason is not { } activeSeason)
            {
                facts = new SparkySeasonStandingAnswerFacts(
                    SparkySeasonStandingPeriod.Current,
                    SparkySeasonStandingStatus.NoActiveSeason,
                    null,
                    null,
                    null,
                    "live-season-current");
                return true;
            }

            SparkySeasonStanding? leader = current.CurrentTopTen.FirstOrDefault(standing => standing.Rank == 1);
            facts = leader is null
                ? new SparkySeasonStandingAnswerFacts(
                    SparkySeasonStandingPeriod.Current,
                    SparkySeasonStandingStatus.NoEligibleParticipants,
                    activeSeason.Name,
                    null,
                    null,
                    "live-season-current")
                : new SparkySeasonStandingAnswerFacts(
                    SparkySeasonStandingPeriod.Current,
                    SparkySeasonStandingStatus.LeaderFound,
                    activeSeason.Name,
                    leader.DisplayName,
                    leader.TotalPoints,
                    "live-season-current");
            return true;
        }

        SparkySeasonFinalRecord? mostRecentFinal = state.FinalizedSeasons
            .OrderByDescending(record => record.Finalization.EndTimeUtc)
            .ThenByDescending(record => record.SeasonId)
            .FirstOrDefault();
        if (mostRecentFinal is null)
        {
            facts = new SparkySeasonStandingAnswerFacts(
                SparkySeasonStandingPeriod.MostRecentFinal,
                SparkySeasonStandingStatus.NoFinalizedSeason,
                null,
                null,
                null,
                null);
            return true;
        }

        SparkySeasonStanding? finalLeader = mostRecentFinal.Finalization.TopTen
            .FirstOrDefault(standing => standing.Rank == 1);
        facts = finalLeader is null
            ? new SparkySeasonStandingAnswerFacts(
                SparkySeasonStandingPeriod.MostRecentFinal,
                SparkySeasonStandingStatus.NoFinalStandings,
                mostRecentFinal.Finalization.Name,
                null,
                null,
                $"live-season-final-{mostRecentFinal.SeasonId}")
            : new SparkySeasonStandingAnswerFacts(
                SparkySeasonStandingPeriod.MostRecentFinal,
                SparkySeasonStandingStatus.LeaderFound,
                mostRecentFinal.Finalization.Name,
                finalLeader.DisplayName,
                finalLeader.TotalPoints,
                $"live-season-final-{mostRecentFinal.SeasonId}");
        return true;
    }

    public static IReadOnlyList<string> GetRequiredExactStrings(SparkySeasonStandingAnswerFacts facts)
    {
        var required = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(facts.SeasonName))
        {
            required.Add(facts.SeasonName);
        }

        if (!string.IsNullOrWhiteSpace(facts.LeaderName))
        {
            required.Add(facts.LeaderName);
        }

        return required;
    }

    public static string GetPromptGuidance(SparkySeasonStandingAnswerFacts facts) => facts.Status switch
    {
        SparkySeasonStandingStatus.LeaderFound => facts.Period == SparkySeasonStandingPeriod.Current
            ? "Verified current-season standing evidence identifies the rank-one points leader. Answer who leads directly, include the exact season and leader names, and do not say that the records omit the leader or ask which season is meant."
            : "Verified final-standing evidence identifies the most recently completed season's rank-one leader. Answer who led directly, include the exact season and leader names, and do not ask which past season is meant.",
        SparkySeasonStandingStatus.NoActiveSeason => "Verified live data says there is no active season. State that directly; do not ask what 'current season' refers to.",
        SparkySeasonStandingStatus.NoEligibleParticipants => "Verified live data says this season has no eligible leaderboard participants. State that directly; do not invent a leader.",
        SparkySeasonStandingStatus.NoFinalizedSeason => "The local archive contains no finalized season record. Say that the archive has no past-season result available; do not ask which past season is meant.",
        SparkySeasonStandingStatus.NoFinalStandings => "The most recently completed season has no recorded public final standings. State that directly; do not invent a leader.",
        _ => string.Empty
    };
}
