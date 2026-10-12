using System.Text.RegularExpressions;

namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyKnowledgeRetriever(SparkyKnowledgeBundle bundle)
{
    private const int DefaultMaximumPassages = 5;
    private const int DefaultCharacterBudget = 4200;
    private static readonly Regex WordPattern = new("[\\p{L}\\p{N}]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "about", "after", "again", "also", "are", "can", "could", "does", "for", "from", "how", "into",
        "is", "its", "please", "tell", "that", "the", "their", "them", "there", "these", "this", "those",
        "what", "when", "where", "which", "with", "would", "you", "your"
    };

    public IReadOnlyList<SparkyKnowledgePassage> Search(
        string englishQuery,
        int maxPassages = DefaultMaximumPassages,
        int maxCharacters = DefaultCharacterBudget)
    {
        if (string.IsNullOrWhiteSpace(englishQuery) || maxPassages <= 0 || maxCharacters <= 0 ||
            bundle.KnowledgePassages.Count == 0)
        {
            return [];
        }

        string[] queryTokens = Tokenize(englishQuery).Distinct(StringComparer.Ordinal).ToArray();
        if (queryTokens.Length == 0)
        {
            return [];
        }

        var matchCounts = new Dictionary<int, int>();
        foreach (string token in queryTokens)
        {
            if (!bundle.SearchIndex.TokenToPassageIndexes.TryGetValue(token, out var passageIndexes))
            {
                continue;
            }

            foreach (int index in passageIndexes)
            {
                matchCounts[index] = matchCounts.GetValueOrDefault(index) + 1;
            }
        }

        var ranked = matchCounts
            .Select(entry => new
            {
                Index = entry.Key,
                MatchCount = entry.Value,
                Score = (double)entry.Value / queryTokens.Length
            })
            .Where(entry => entry.Score >= 0.25)
            .OrderByDescending(entry => entry.Score)
            .ThenByDescending(entry => entry.MatchCount)
            .ThenBy(entry => bundle.KnowledgePassages[entry.Index].SourceId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Index)
            .Take(maxPassages);

        var results = new List<SparkyKnowledgePassage>();
        int usedCharacters = 0;
        foreach (var result in ranked)
        {
            var passage = bundle.KnowledgePassages[result.Index];
            if (usedCharacters + passage.Text.Length > maxCharacters)
            {
                continue;
            }

            results.Add(passage);
            usedCharacters += passage.Text.Length;
        }

        return results;
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        return WordPattern.Matches(value)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => token.Length >= 3 && !StopWords.Contains(token));
    }
}
