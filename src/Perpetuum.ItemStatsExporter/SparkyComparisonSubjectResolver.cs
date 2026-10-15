namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyComparisonSubject(
    string Domain,
    string DefinitionName,
    string DisplayName,
    string ReferenceSource);

internal sealed record SparkyComparisonSubjectResolution(
    string Status,
    string? Domain,
    IReadOnlyList<SparkyComparisonSubject> Subjects,
    IReadOnlyList<string> CandidateNames);

/// <summary>
/// Resolves interpreter-proposed and contextual comparison subjects against the active bundle.
/// Dialogue is used only to identify catalog entries, never as a source of comparison facts.
/// </summary>
internal static class SparkyComparisonSubjectResolver
{
    public static SparkyComparisonSubjectResolution Resolve(
        SparkyComparisonIntent intent,
        string question,
        string? englishSearchQuery,
        IReadOnlyList<SparkyConversationTurn> contextTurns,
        SparkyKnowledgeBundle bundle,
        int languageId)
    {
        if (!intent.IsComparison)
        {
            return Result("not_comparison", null, []);
        }

        var subjects = new List<SparkyComparisonSubject>();
        AddMatches(subjects, question, intent.IsContextual ? null : englishSearchQuery, bundle, languageId, "current_question");

        foreach (string reference in intent.SubjectReferences)
        {
            bool referencedInCurrentQuestion = question.Contains(reference, StringComparison.OrdinalIgnoreCase);
            string referenceSource = referencedInCurrentQuestion || !intent.IsContextual
                ? "current_question"
                : "conversation_context";
            AddMatches(subjects, reference, reference, bundle, languageId, referenceSource);
        }

        if (intent.IsContextual && subjects.Count < 2)
        {
            foreach (SparkyConversationTurn turn in contextTurns)
            {
                if (!string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddMatches(subjects, turn.Content, null, bundle, languageId, "conversation_context");
            }
        }

        subjects = subjects
            .GroupBy(subject => (subject.Domain, subject.DefinitionName), StringTupleComparer.Instance)
            .Select(group => group.First())
            .Take(9)
            .ToList();

        if (subjects.Count == 0)
        {
            return Result("no_subjects", null, []);
        }

        if (subjects.Count > 2)
        {
            return Result("ambiguous", null, subjects);
        }

        if (subjects.Count < 2)
        {
            return Result("insufficient_subjects", subjects[0].Domain, subjects);
        }

        if (!string.Equals(subjects[0].Domain, subjects[1].Domain, StringComparison.Ordinal))
        {
            return Result("cross_domain", null, subjects);
        }

        return Result("resolved", subjects[0].Domain, subjects);
    }

    private static void AddMatches(
        ICollection<SparkyComparisonSubject> subjects,
        string text,
        string? englishSearchQuery,
        SparkyKnowledgeBundle bundle,
        int languageId,
        string referenceSource)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (SparkyRobotRecord robot in SparkyRobotLookup.FindMentionedRobots(
                     text, englishSearchQuery, bundle, languageId))
        {
            string? displayName = GetRobotDisplayName(robot, languageId);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                subjects.Add(new SparkyComparisonSubject(
                    "Robot", robot.DefinitionName, displayName, referenceSource));
            }
        }

        ItemStatsLookupResponse itemResponse = ItemStatsLookup.Resolve(
            text,
            bundle,
            languageId,
            englishFollowUpQuery: englishSearchQuery);
        if (itemResponse.Status == "found" && itemResponse.Item is { } item)
        {
            subjects.Add(new SparkyComparisonSubject(
                "Item", item.DefinitionName, item.DisplayName, referenceSource));
        }
        else if (itemResponse.Status == "ambiguous")
        {
            foreach (ItemStatsLookupCandidate candidate in itemResponse.Candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate.DefinitionName) &&
                    !string.IsNullOrWhiteSpace(candidate.DisplayName))
                {
                    subjects.Add(new SparkyComparisonSubject(
                        "Item", candidate.DefinitionName, candidate.DisplayName, referenceSource));
                }
            }
        }
    }

    private static string? GetRobotDisplayName(SparkyRobotRecord robot, int languageId)
    {
        if (robot.LocalizedViews.TryGetValue(languageId, out SparkyLocalizedRobotView? localized) &&
            !string.IsNullOrWhiteSpace(localized.DisplayName))
        {
            return localized.DisplayName;
        }

        return robot.LocalizedViews.TryGetValue(0, out SparkyLocalizedRobotView? english) &&
               !string.IsNullOrWhiteSpace(english.DisplayName)
            ? english.DisplayName
            : null;
    }

    private static SparkyComparisonSubjectResolution Result(
        string status,
        string? domain,
        IReadOnlyList<SparkyComparisonSubject> subjects) => new(
            status,
            domain,
            subjects,
            subjects.Select(subject => subject.DisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray());

    private sealed class StringTupleComparer : IEqualityComparer<(string Domain, string DefinitionName)>
    {
        public static readonly StringTupleComparer Instance = new();

        public bool Equals((string Domain, string DefinitionName) x, (string Domain, string DefinitionName) y) =>
            string.Equals(x.Domain, y.Domain, StringComparison.Ordinal) &&
            string.Equals(x.DefinitionName, y.DefinitionName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Domain, string DefinitionName) value) =>
            HashCode.Combine(StringComparer.Ordinal.GetHashCode(value.Domain),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.DefinitionName));
    }
}
