using System.Text.RegularExpressions;

namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyKnowledgeRetriever(SparkyKnowledgeBundle bundle)
{
    private const int DefaultMaximumPassages = 5;
    private const int DefaultCharacterBudget = 4200;
    private const double MinimumRelevanceScore = 0.25;
    private static readonly Regex WordPattern = new("[\\p{L}\\p{N}]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "about", "after", "again", "also", "are", "can", "could", "does", "for", "from", "how", "into",
        "is", "its", "please", "tell", "that", "the", "their", "them", "there", "these", "this", "those",
        "what", "when", "where", "which", "who", "whom", "whose", "why", "with", "would", "you", "your"
    };

    public IReadOnlyList<SparkyKnowledgePassage> Search(
        string playerQuery,
        string englishQuery,
        int maxPassages = DefaultMaximumPassages,
        int maxCharacters = DefaultCharacterBudget,
        IReadOnlyList<string>? mentionedEntities = null,
        bool prioritizeEntityDefinitions = false,
        double minimumRelevanceScore = MinimumRelevanceScore,
        IReadOnlyList<string>? sourceCategories = null,
        SparkyLiveKnowledgeReadView? liveKnowledge = null)
    {
        IReadOnlyList<SparkyKnowledgePassage> passages = liveKnowledge is null || liveKnowledge.Supplement.Passages.Count == 0
            ? bundle.KnowledgePassages
            : bundle.KnowledgePassages.Concat(liveKnowledge.Supplement.Passages).ToArray();
        if ((string.IsNullOrWhiteSpace(playerQuery) && string.IsNullOrWhiteSpace(englishQuery)) ||
            maxPassages <= 0 || maxCharacters <= 0 ||
            !double.IsFinite(minimumRelevanceScore) || minimumRelevanceScore is < 0 or > 1 ||
            passages.Count == 0)
        {
            return [];
        }

        var queryTokenGroups = new List<string[]>();
        foreach (string query in new[] { playerQuery, englishQuery })
        {
            string[] tokens = Tokenize(query ?? string.Empty).Distinct(StringComparer.Ordinal).ToArray();
            if (tokens.Length > 0 && !queryTokenGroups.Any(existing => SameTokens(existing, tokens)))
            {
                queryTokenGroups.Add(tokens);
            }
        }

        foreach (string entity in mentionedEntities ?? [])
        {
            string[] tokens = Tokenize(entity).Distinct(StringComparer.Ordinal).ToArray();
            if (tokens.Length > 0 && !queryTokenGroups.Any(existing => SameTokens(existing, tokens)))
            {
                queryTokenGroups.Add(tokens);
            }
        }

        var entityDefinitionPatterns = prioritizeEntityDefinitions
            ? (mentionedEntities ?? [])
                .Where(entity => !string.IsNullOrWhiteSpace(entity))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(CreateEntityDefinitionPattern)
                .ToArray()
            : [];
        var entityDefinitionPassages = new HashSet<int>();
        if (entityDefinitionPatterns.Length > 0)
        {
            for (int index = 0; index < passages.Count; index++)
            {
                if (entityDefinitionPatterns.Any(pattern => pattern.IsMatch(passages[index].Text)))
                {
                    entityDefinitionPassages.Add(index);
                }
            }
        }

        var passageMatches = new Dictionary<int, (double BestScore, int QueryMatchCount, double BestMatchedWeight, bool HasEntityDefinition)>();
        foreach (string[] queryTokens in queryTokenGroups)
        {
            var tokenWeights = new List<(IReadOnlyList<int> PassageIndexes, double Weight)>();
            double totalWeight = 0;
            foreach (string token in queryTokens)
            {
                bundle.SearchIndex.TokenToPassageIndexes.TryGetValue(token, out var bundleIndexes);
                IReadOnlyList<int>? liveIndexes = null;
                if (liveKnowledge is not null)
                {
                    liveKnowledge.Supplement.SearchIndex.TokenToPassageIndexes.TryGetValue(token, out liveIndexes);
                }
                int bundleCount = bundleIndexes?.Count ?? 0;
                int liveCount = liveIndexes?.Count ?? 0;
                if (bundleCount + liveCount == 0)
                {
                    continue;
                }

                var passageIndexes = new List<int>(bundleCount + liveCount);
                if (bundleIndexes is not null)
                {
                    passageIndexes.AddRange(bundleIndexes);
                }
                if (liveIndexes is not null)
                {
                    passageIndexes.AddRange(liveIndexes.Select(index => index + bundle.KnowledgePassages.Count));
                }

                double weight = Math.Log(1d + (double)passages.Count / (bundleCount + liveCount + 1d));
                tokenWeights.Add((passageIndexes, weight));
                totalWeight += weight;
            }

            if (totalWeight <= 0)
            {
                continue;
            }

            var matchedWeights = new Dictionary<int, double>();
            foreach (var (passageIndexes, weight) in tokenWeights)
            {
                foreach (int index in passageIndexes)
                {
                    matchedWeights[index] = matchedWeights.GetValueOrDefault(index) + weight;
                }
            }

            foreach (var (index, matchedWeight) in matchedWeights)
            {
                double score = matchedWeight / totalWeight;
                if (score < minimumRelevanceScore)
                {
                    continue;
                }

                if (passageMatches.TryGetValue(index, out var existing))
                {
                    passageMatches[index] = (
                        Math.Max(existing.BestScore, score),
                        existing.QueryMatchCount + 1,
                        Math.Max(existing.BestMatchedWeight, matchedWeight),
                        existing.HasEntityDefinition || entityDefinitionPassages.Contains(index));
                }
                else
                {
                    passageMatches[index] = (score, 1, matchedWeight, entityDefinitionPassages.Contains(index));
                }
            }
        }

        int[] rankedIndexes = passageMatches
            .Where(entry => sourceCategories is null || sourceCategories.Contains(
                passages[entry.Key].SourceCategory, StringComparer.Ordinal))
            .Where(entry => liveKnowledge is null || !liveKnowledge.ExcludedSourceIds.Contains(passages[entry.Key].SourceId))
            .OrderByDescending(entry => entry.Value.HasEntityDefinition)
            .ThenByDescending(entry => entry.Value.BestScore)
            .ThenByDescending(entry => entry.Value.QueryMatchCount)
            .ThenByDescending(entry => entry.Value.BestMatchedWeight)
            .ThenBy(entry => entry.Key)
            .Select(entry => entry.Key)
            .ToArray();

        int maximumKnownQueryTerms = queryTokenGroups
            .Select(tokens => tokens.Count(bundle.SearchIndex.TokenToPassageIndexes.ContainsKey))
            .DefaultIfEmpty(0)
            .Max();
        IEnumerable<int> selectedIndexes = maximumKnownQueryTerms <= 1
            ? SelectSourceDiversePassages(rankedIndexes, maxPassages, passages)
            : rankedIndexes.Take(maxPassages);

        var results = new List<SparkyKnowledgePassage>();
        int usedCharacters = 0;
        foreach (int index in selectedIndexes)
        {
            var passage = passages[index];
            if (usedCharacters + passage.Text.Length > maxCharacters)
            {
                continue;
            }

            results.Add(passage);
            usedCharacters += passage.Text.Length;
        }

        return results;
    }

    private static int[] SelectSourceDiversePassages(
        IReadOnlyList<int> rankedIndexes,
        int maxPassages,
        IReadOnlyList<SparkyKnowledgePassage> passages)
    {
        var selectedIndexes = new List<int>(maxPassages);
        var selectedPassages = new HashSet<int>();
        var selectedSources = new HashSet<string>(StringComparer.Ordinal);

        foreach (int index in rankedIndexes)
        {
            string sourceId = passages[index].SourceId;
            if (selectedSources.Add(sourceId))
            {
                selectedIndexes.Add(index);
                selectedPassages.Add(index);
                if (selectedIndexes.Count == maxPassages)
                {
                    return selectedIndexes.ToArray();
                }
            }
        }

        foreach (int index in rankedIndexes)
        {
            if (selectedPassages.Add(index))
            {
                selectedIndexes.Add(index);
                if (selectedIndexes.Count == maxPassages)
                {
                    break;
                }
            }
        }

        return selectedIndexes.ToArray();
    }

    private static bool SameTokens(IReadOnlyCollection<string> left, IReadOnlyCollection<string> right)
    {
        return left.Count == right.Count && left.All(right.Contains);
    }

    private static Regex CreateEntityDefinitionPattern(string entity)
    {
        return new Regex(
            $"(?:^|[.!?]\\s+|[\\r\\n]+)\\s*(?:[\\\"'“‘(]\\s*)?(?:the\\s+)?{Regex.Escape(entity.Trim())}(?![\\p{{L}}\\p{{N}}])\\s*(?:[,—:]\\s*)?(?:is|was|are|were|means|represents|refers\\s+to|is\\s+known\\s+as|was\\s+known\\s+as)\\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
    }

    internal static bool ContainsEntityDefinition(string passageText, string entity)
    {
        return !string.IsNullOrWhiteSpace(entity) && CreateEntityDefinitionPattern(entity).IsMatch(passageText);
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        return WordPattern.Matches(value)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => token.Length >= 3 && !StopWords.Contains(token));
    }
}
