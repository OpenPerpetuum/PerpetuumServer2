using Perpetuum.Services.Sparky;

namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyBoundedConversationContext(
    IReadOnlyList<SparkyConversationTurn> Turns,
    string? Summary,
    SparkyPendingClarification? PendingClarification);

internal static class SparkyConversationContextBudget
{
    public static SparkyBoundedConversationContext Apply(
        IReadOnlyList<SparkyConversationTurn>? turns,
        string? summary,
        SparkyPendingClarification? pendingClarification,
        int maximumTurns,
        int maximumCharacters)
    {
        int turnLimit = Math.Clamp(maximumTurns, 2, SparkyApiRequest.MaximumContextTurns);
        turnLimit -= turnLimit % 2;
        int characterLimit = Math.Clamp(maximumCharacters, 256, SparkyApiRequest.MaximumContextCharacters);
        int summaryLimit = Math.Min(SparkyApiRequest.MaximumSummaryCharacters, characterLimit / 4);
        string? boundedSummary = string.IsNullOrWhiteSpace(summary)
            ? null
            : summary[..Math.Min(summary.Length, summaryLimit)];
        int remainingCharacters = characterLimit - (boundedSummary?.Length ?? 0);

        var boundedTurns = new List<SparkyConversationTurn>();
        var source = turns ?? [];
        int completeSourceCount = source.Count - source.Count % 2;
        int availableTurns = Math.Min(completeSourceCount, turnLimit);
        int index = completeSourceCount - availableTurns;
        for (; index + 1 < source.Count; index += 2)
        {
            SparkyConversationTurn user = source[index];
            SparkyConversationTurn assistant = source[index + 1];
            if (!string.Equals(user.Role, "user", StringComparison.Ordinal) ||
                !string.Equals(assistant.Role, "assistant", StringComparison.Ordinal))
                continue;

            int pairLength = user.Content.Length + assistant.Content.Length;
            if (pairLength > remainingCharacters)
                break;

            boundedTurns.Add(user);
            boundedTurns.Add(assistant);
            remainingCharacters -= pairLength;
        }

        return new SparkyBoundedConversationContext(
            boundedTurns,
            boundedSummary,
            ClonePending(pendingClarification));
    }

    private static SparkyPendingClarification? ClonePending(SparkyPendingClarification? pending) => pending is null
        ? null
        : pending with { CandidateNames = pending.CandidateNames.Take(SparkyApiRequest.MaximumCandidates).ToArray() };
}
