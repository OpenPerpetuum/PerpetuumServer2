namespace Perpetuum.ItemStatsExporter;

internal static class SparkyConversationUpdateFactory
{
    public static SparkyConversationUpdate Create(
        string question,
        SparkyQuestionInterpretation interpretation,
        SparkyAnswerResult answer)
    {
        var candidates = (answer.Candidates ?? [])
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(SparkyApiRequest.MaximumCandidates)
            .ToArray();
        bool candidatesWereOffered = answer.Status is "answered" or "ambiguous" or "not_found" &&
                                     !string.IsNullOrWhiteSpace(answer.Answer) &&
                                     candidates.Length > 0 &&
                                     candidates.All(candidate => answer.Answer.Contains(candidate, StringComparison.OrdinalIgnoreCase));

        var pending = candidatesWereOffered
            ? new SparkyPendingClarification(
                question,
                interpretation.RequestedAnswerType ?? InferRequestedAnswerType(question, interpretation),
                candidates)
            : null;

        return new SparkyConversationUpdate(
            interpretation.ContextSummary,
            answer.Item,
            pending);
    }

    private static string InferRequestedAnswerType(string question, SparkyQuestionInterpretation interpretation)
    {
        string query = string.Join(' ', question, interpretation.EnglishSearchQuery).ToLowerInvariant();
        if (query.Contains("full", StringComparison.Ordinal) || query.Contains("all materials", StringComparison.Ordinal) ||
            query.Contains("raw materials", StringComparison.Ordinal) || query.Contains("recursive", StringComparison.Ordinal))
        {
            return "FullRecipe";
        }

        if (query.Contains("recipe", StringComparison.Ordinal) || query.Contains("materials", StringComparison.Ordinal) ||
            query.Contains("components", StringComparison.Ordinal))
        {
            return "Recipe";
        }

        if (query.Contains("ammo", StringComparison.Ordinal) || query.Contains("ammunition", StringComparison.Ordinal))
        {
            return "Ammo";
        }

        if (query.Contains("compatib", StringComparison.Ordinal) || query.Contains("fit", StringComparison.Ordinal))
        {
            return "Compatibility";
        }

        return interpretation.Intent == SparkyIntent.StructuredRobot &&
               interpretation.RobotFocus == SparkyRobotQuestionFocus.Components
            ? "Components"
            : query.Contains("stat", StringComparison.Ordinal) || query.Contains("damage", StringComparison.Ordinal)
                ? "Stats"
                : "General";
    }
}
