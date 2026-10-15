using System.Globalization;

namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyComparisonRequest(
    string Domain,
    string FirstDefinitionName,
    string SecondDefinitionName,
    string? MetricPhrase,
    SparkyComparisonDirection Direction,
    bool CompareAllSharedFields);

internal sealed record SparkyComparisonFact(
    string AttributeLabel,
    IReadOnlyList<string> Values,
    string? Unit,
    string Difference,
    string? LeadingSubjectName);

internal sealed record SparkyComparisonResolution(
    string Status,
    IReadOnlyList<string> LocalizedSubjectNames,
    string? AttributeLabel,
    IReadOnlyList<string> Values,
    string? Unit,
    string? Difference,
    string? LeadingSubjectName,
    string DiagnosticCategory,
    IReadOnlyList<SparkyComparisonFact> Facts);

internal interface ISparkyComparisonDomainResolver
{
    string Domain { get; }

    SparkyComparisonResolution Resolve(
        SparkyComparisonRequest request,
        SparkyKnowledgeBundle bundle,
        int languageId);
}

internal static class SparkyComparisonCoordinator
{
    public static SparkyComparisonResolution Resolve(
        SparkyComparisonRequest request,
        SparkyKnowledgeBundle bundle,
        int languageId,
        IEnumerable<ISparkyComparisonDomainResolver> domainResolvers)
    {
        ISparkyComparisonDomainResolver? resolver = domainResolvers.FirstOrDefault(candidate =>
            string.Equals(candidate.Domain, request.Domain, StringComparison.Ordinal));
        if (resolver is null)
        {
            return new SparkyComparisonResolution("unsupported_domain", [], null, [], null, null, null,
                "unsupported_domain", []);
        }

        return resolver.Resolve(request, bundle, languageId);
    }

    internal static SparkyComparisonResolution Compare(
        string domain,
        string firstName,
        string secondName,
        IReadOnlyList<AnswerStat> firstStats,
        IReadOnlyList<AnswerStat> secondStats,
        SparkyComparisonRequest request)
    {
        var names = new[] { firstName, secondName };
        if (request.CompareAllSharedFields)
        {
            var secondById = secondStats.GroupBy(stat => stat.FieldId)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single());
            var facts = firstStats
                .Where(stat => secondById.ContainsKey(stat.FieldId))
                .OrderBy(stat => stat.FieldId)
                .Select(stat => TryCreateFact(stat, secondById[stat.FieldId], names, request.Direction))
                .Where(fact => fact is not null)
                .Cast<SparkyComparisonFact>()
                .ToArray();
            return facts.Length == 0
                ? Missing(names, "no_shared_numeric_fields")
                : new SparkyComparisonResolution("resolved", names, null, [], null, null, null,
                    "comparison_resolved", facts);
        }

        if (string.IsNullOrWhiteSpace(request.MetricPhrase))
        {
            return new SparkyComparisonResolution("needs_criterion", names, null, [], null, null, null,
                "criterion_missing", []);
        }

        IReadOnlyList<AnswerStat> commonStats = firstStats
            .Where(first => secondStats.Any(second => second.FieldId == first.FieldId && UnitsMatch(first.Unit, second.Unit)))
            .ToArray();
        IReadOnlyList<AnswerStat> selected = MatchMetric(commonStats, request.MetricPhrase, domain);
        if (selected.Count == 0)
        {
            return Missing(names, "attribute_unavailable");
        }

        if (selected.Count > 1)
        {
            return new SparkyComparisonResolution("ambiguous_attribute", names, null, [], null, null, null,
                "attribute_ambiguous", []);
        }

        AnswerStat firstStat = selected[0];
        AnswerStat? secondStat = secondStats.FirstOrDefault(stat =>
            stat.FieldId == firstStat.FieldId && UnitsMatch(firstStat.Unit, stat.Unit));
        if (secondStat is null)
        {
            return Missing(names, "attribute_unavailable");
        }

        SparkyComparisonFact? fact = TryCreateFact(firstStat, secondStat, names, request.Direction);
        if (fact is null)
        {
            return Missing(names, "value_unavailable");
        }

        return new SparkyComparisonResolution("resolved", names, fact.AttributeLabel, fact.Values,
            fact.Unit, fact.Difference, fact.LeadingSubjectName, "comparison_resolved", [fact]);
    }

    private static IReadOnlyList<AnswerStat> MatchMetric(
        IReadOnlyList<AnswerStat> stats,
        string metricPhrase,
        string domain)
    {
        string normalized = Normalize(metricPhrase);
        if (normalized.Length == 0)
        {
            return [];
        }

        if (domain == "Robot" && IsSpeedPhrase(normalized))
        {
            const int speedFieldId = (int)Perpetuum.ExportedTypes.AggregateField.speed_max;
            return stats.Where(stat => stat.FieldId == speedFieldId).ToArray();
        }

        string[] words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matches = stats.Where(stat =>
        {
            string label = Normalize(stat.Label);
            return string.Equals(label, normalized, StringComparison.Ordinal) ||
                   label.Contains(normalized, StringComparison.Ordinal) ||
                   normalized.Contains(label, StringComparison.Ordinal) ||
                   words.Length >= 2 && words.All(word => label.Contains(word, StringComparison.Ordinal));
        }).ToArray();
        return matches;
    }

    private static SparkyComparisonFact? TryCreateFact(
        AnswerStat first,
        AnswerStat second,
        IReadOnlyList<string> names,
        SparkyComparisonDirection direction)
    {
        if (!UnitsMatch(first.Unit, second.Unit) ||
            !decimal.TryParse(first.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal firstValue) ||
            !decimal.TryParse(second.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal secondValue))
        {
            return null;
        }

        string? leader = direction switch
        {
            SparkyComparisonDirection.Higher when firstValue > secondValue => names[0],
            SparkyComparisonDirection.Higher when secondValue > firstValue => names[1],
            SparkyComparisonDirection.Lower when firstValue < secondValue => names[0],
            SparkyComparisonDirection.Lower when secondValue < firstValue => names[1],
            _ => null
        };
        return new SparkyComparisonFact(
            first.Label,
            [first.Value, second.Value],
            first.Unit,
            Math.Abs(firstValue - secondValue).ToString(CultureInfo.InvariantCulture),
            leader);
    }

    private static bool UnitsMatch(string? first, string? second) =>
        string.Equals(first?.Trim(), second?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool IsSpeedPhrase(string phrase) => phrase is "fast" or "faster" or "speed" or "speedier" or
        "top speed" or "maximum speed" or "max speed" or "highest speed";

    private static string Normalize(string value) => string.Join(' ',
        System.Text.RegularExpressions.Regex.Matches(value.ToLowerInvariant(), @"[\p{L}\p{N}]+")
            .Select(match => match.Value));

    private static SparkyComparisonResolution Missing(IReadOnlyList<string> names, string category) =>
        new("unavailable", names, null, [], null, null, null, category, []);
}
