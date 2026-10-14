namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyEntityCandidate
{
    public required string DefinitionName { get; init; }
    public required SparkyEntityKind EntityKind { get; init; }
    public required SparkyEntityTierMetadata TierMetadata { get; init; }
    public required IReadOnlyList<string> PublicAliases { get; init; }
    public required bool IsCalibrationTemplate { get; init; }
}

internal sealed record SparkyEntityResolutionResult(
    string Status,
    string? DefinitionName,
    IReadOnlyList<string> CandidateDefinitionNames)
{
    public bool HasNameQuery { get; init; }
}

internal static class SparkyTierAwareEntityResolver
{
    private static readonly HashSet<string> IntentStopWords = new(StringComparer.Ordinal)
    {
        "a", "about", "also", "an", "and", "another", "are", "can", "could", "for", "full", "give", "how",
        "fit", "fits", "i", "is", "it", "its", "list", "me", "of", "on", "one", "other", "please", "previous", "recipe",
        "same", "show", "some", "stats", "tell", "the", "their", "them", "then", "these", "they", "this",
        "those", "to", "what", "which", "who", "with", "would"
    };

    public static SparkyEntityResolutionResult Resolve(
        string question,
        string? englishQuery,
        SparkyEntityKind entityKind,
        IReadOnlyList<SparkyEntityCandidate> candidates,
        SparkyEntityResolutionRules rules,
        int languageId,
        bool matchAliasWithinQuestion = false)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(rules);

        if (languageId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(languageId));
        }

        rules.Validate();
        var sourceTexts = new[] { question, englishQuery }
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        IReadOnlyList<TierAliasPattern> tierAliases = CreateTierPatterns(rules, entityKind, languageId);
        IReadOnlyList<string[]> calibrationAliases = CreateCalibrationPatterns(rules, languageId);
        var parsedQueries = sourceTexts
            .Select(text => ParseQuery(text, entityKind, tierAliases, calibrationAliases))
            .ToArray();
        bool hasNameQuery = parsedQueries.Any(query => RemoveIntentStopWords(query.RemainingTokens).Length > 0);

        var tierPairs = parsedQueries
            .SelectMany(query => query.TierPairs)
            .Distinct()
            .ToArray();
        if (tierPairs.Length > 1)
        {
            return Result("conflicting_tiers", hasNameQuery: hasNameQuery);
        }

        if (parsedQueries.Any(query => query.HasUnsupportedTierSyntax))
        {
            return Result("not_found", hasNameQuery: hasNameQuery);
        }

        (int TierType, int? TierLevel)? requiredTier = tierPairs.Length == 1 ? tierPairs[0] : null;
        bool requiresCalibration = entityKind == SparkyEntityKind.Item &&
                                   parsedQueries.Any(query => query.HasCalibrationCategory);
        var eligible = candidates
            .Where(candidate => candidate.EntityKind == entityKind)
            .Where(candidate => requiredTier is null ||
                candidate.TierMetadata.TierType == requiredTier.Value.TierType &&
                candidate.TierMetadata.TierLevel == requiredTier.Value.TierLevel)
            .Where(candidate => !requiresCalibration || candidate.IsCalibrationTemplate)
            .ToArray();

        string[][] nameQueries = parsedQueries
            .Select(query => RemoveIntentStopWords(query.RemainingTokens))
            .Where(tokens => tokens.Length > 0)
            .ToArray();

        if (nameQueries.Length == 0)
        {
            string[] tierCandidates = requiredTier is not null || requiresCalibration
                ? eligible.Select(candidate => candidate.DefinitionName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                : [];
            return tierCandidates.Length > 0
                ? Result("ambiguous", tierCandidates, hasNameQuery)
                : Result("not_found", hasNameQuery: hasNameQuery);
        }

        var matches = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (SparkyEntityCandidate candidate in eligible)
        {
            int bestScore = 0;
            foreach (string alias in candidate.PublicAliases.Where(alias => !string.IsNullOrWhiteSpace(alias)))
            {
                string[] aliasTokens = Tokenize(alias);
                if (aliasTokens.Length == 0)
                {
                    continue;
                }

                foreach (string[] nameQuery in nameQueries)
                {
                    bestScore = Math.Max(bestScore, GetNameMatchScore(aliasTokens, nameQuery, matchAliasWithinQuestion));
                }
            }

            if (bestScore > 0 && (!matches.TryGetValue(candidate.DefinitionName, out int existingScore) || bestScore > existingScore))
            {
                matches[candidate.DefinitionName] = bestScore;
            }
        }

        if (matches.Count == 0)
        {
            return Result("not_found", hasNameQuery: hasNameQuery);
        }

        int topScore = matches.Values.Max();
        string[] topCandidates = matches
            .Where(entry => entry.Value == topScore)
            .Select(entry => entry.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return topCandidates.Length == 1
            ? new SparkyEntityResolutionResult("found", topCandidates[0], topCandidates) { HasNameQuery = hasNameQuery }
            : Result("ambiguous", topCandidates, hasNameQuery);
    }

    private static IReadOnlyList<TierAliasPattern> CreateTierPatterns(
        SparkyEntityResolutionRules rules,
        SparkyEntityKind entityKind,
        int languageId)
    {
        var patterns = new Dictionary<string, TierAliasPattern>(StringComparer.Ordinal);
        foreach (SparkyEntityTierRule rule in rules.TierRules)
        {
            foreach (string alias in rules.GetAliases(rule, languageId))
            {
                string[] tokens = Tokenize(alias);
                if (tokens.Length == 0)
                {
                    continue;
                }

                string key = $"{rule.EntityKind}\u001f{string.Join('\u001f', tokens)}";
                patterns.TryAdd(key, new TierAliasPattern(tokens, rule.EntityKind, rule.TierType, rule.TierLevel));
            }
        }

        return patterns.Values
            .OrderByDescending(pattern => pattern.Tokens.Length)
            .ThenBy(pattern => pattern.EntityKind == entityKind ? 0 : 1)
            .ThenBy(pattern => string.Join(' ', pattern.Tokens), StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string[]> CreateCalibrationPatterns(SparkyEntityResolutionRules rules, int languageId)
    {
        return rules.GetCalibrationAliases(languageId)
            .Select(Tokenize)
            .Where(tokens => tokens.Length > 0)
            .Distinct(TokenSequenceComparer.Instance)
            .OrderByDescending(tokens => tokens.Length)
            .ThenBy(tokens => string.Join(' ', tokens), StringComparer.Ordinal)
            .ToArray();
    }

    private static ParsedQuery ParseQuery(
        string text,
        SparkyEntityKind entityKind,
        IReadOnlyList<TierAliasPattern> tierAliases,
        IReadOnlyList<string[]> calibrationAliases)
    {
        string[] tokens = Tokenize(text);
        var removed = new bool[tokens.Length];
        var tierPairs = new HashSet<(int TierType, int? TierLevel)>();
        bool hasUnsupportedTierSyntax = HasUnsupportedTierSyntax(tokens, tierAliases);

        for (int index = 0; index < tokens.Length;)
        {
            TierAliasPattern? matched = tierAliases.FirstOrDefault(pattern => MatchesAt(tokens, index, pattern.Tokens));
            if (matched is null)
            {
                index++;
                continue;
            }

            if (matched.EntityKind != entityKind)
            {
                index += matched.Tokens.Length;
                continue;
            }

            tierPairs.Add((matched.TierType, matched.TierLevel));
            MarkRemoved(removed, index, matched.Tokens.Length);
            index += matched.Tokens.Length;
        }

        bool hasCalibrationCategory = false;
        for (int index = 0; index < tokens.Length;)
        {
            string[]? matched = calibrationAliases.FirstOrDefault(pattern => MatchesAt(tokens, index, pattern));
            if (matched is null)
            {
                index++;
                continue;
            }

            hasCalibrationCategory = true;
            MarkRemoved(removed, index, matched.Length);
            index += matched.Length;
        }

        string[] remaining = tokens.Where((_, index) => !removed[index]).ToArray();
        return new ParsedQuery(remaining, tierPairs, hasCalibrationCategory, hasUnsupportedTierSyntax);
    }

    private static bool HasUnsupportedTierSyntax(
        IReadOnlyList<string> tokens,
        IReadOnlyList<TierAliasPattern> tierAliases)
    {
        for (int index = 0; index < tokens.Count; index++)
        {
            string token = tokens[index];
            bool hasAnyRuleAtStart = tierAliases.Any(pattern => MatchesAt(tokens, index, pattern.Tokens));

            if ((System.Text.RegularExpressions.Regex.IsMatch(token, @"^t\d+p?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant) ||
                 System.Text.RegularExpressions.Regex.IsMatch(token, @"^mk\d+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) &&
                !hasAnyRuleAtStart)
            {
                return true;
            }

            if ((token is "tier" or "mark") && index + 1 < tokens.Count &&
                int.TryParse(tokens[index + 1], out _) && !hasAnyRuleAtStart)
            {
                return true;
            }

            int qualifierLength = GetExtendedTierQualifierLength(tokens, index);
            if (qualifierLength > 0 && !tierAliases.Any(pattern =>
                    pattern.Tokens.Length >= qualifierLength &&
                    MatchesAt(tokens, index, pattern.Tokens)))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetExtendedTierQualifierLength(IReadOnlyList<string> tokens, int start)
    {
        int prefixLength;
        if (System.Text.RegularExpressions.Regex.IsMatch(tokens[start], @"^t\d+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            prefixLength = 1;
        }
        else if (tokens[start] == "tier" && start + 1 < tokens.Count && int.TryParse(tokens[start + 1], out _))
        {
            prefixLength = 2;
        }
        else if ((tokens[start] is "mk" or "mark") && start + 1 < tokens.Count && int.TryParse(tokens[start + 1], out _))
        {
            prefixLength = 2;
        }
        else
        {
            return 0;
        }

        if (start + prefixLength < tokens.Count && (tokens[start + prefixLength] is "prototype" or "plus" or "minus"))
        {
            return prefixLength + 1;
        }

        return 0;
    }

    private static string[] RemoveIntentStopWords(IEnumerable<string> tokens) =>
        tokens.Where(token => !IntentStopWords.Contains(token)).ToArray();

    private static int GetNameMatchScore(
        IReadOnlyList<string> aliasTokens,
        IReadOnlyList<string> queryTokens,
        bool matchAliasWithinQuestion)
    {
        if (queryTokens.Count == 0)
        {
            return 0;
        }

        if (matchAliasWithinQuestion && ContainsSequence(queryTokens, aliasTokens))
        {
            return 100 + aliasTokens.Count;
        }

        if (aliasTokens.SequenceEqual(queryTokens, StringComparer.Ordinal))
        {
            return 3;
        }

        if (ContainsSequence(aliasTokens, queryTokens))
        {
            return 2;
        }

        var aliasTokenSet = new HashSet<string>(aliasTokens, StringComparer.Ordinal);
        return queryTokens.All(aliasTokenSet.Contains) ? 1 : 0;
    }

    private static bool ContainsSequence(IReadOnlyList<string> source, IReadOnlyList<string> sequence)
    {
        if (sequence.Count > source.Count)
        {
            return false;
        }

        for (int start = 0; start <= source.Count - sequence.Count; start++)
        {
            bool matches = true;
            for (int offset = 0; offset < sequence.Count; offset++)
            {
                if (!string.Equals(source[start + offset], sequence[offset], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAt(IReadOnlyList<string> source, int start, IReadOnlyList<string> sequence)
    {
        if (start + sequence.Count > source.Count)
        {
            return false;
        }

        for (int offset = 0; offset < sequence.Count; offset++)
        {
            if (!string.Equals(source[start + offset], sequence[offset], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void MarkRemoved(bool[] removed, int start, int length)
    {
        for (int index = start; index < start + length; index++)
        {
            removed[index] = true;
        }
    }

    private static string[] Tokenize(string text) =>
        SparkyEntityResolutionRules.NormalizeTierAlias(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static SparkyEntityResolutionResult Result(
        string status,
        IReadOnlyList<string>? candidateNames = null,
        bool hasNameQuery = false) =>
        new(status, null, candidateNames ?? Array.Empty<string>()) { HasNameQuery = hasNameQuery };

    private sealed record TierAliasPattern(string[] Tokens, SparkyEntityKind EntityKind, int TierType, int? TierLevel);

    private sealed record ParsedQuery(
        string[] RemainingTokens,
        IReadOnlyCollection<(int TierType, int? TierLevel)> TierPairs,
        bool HasCalibrationCategory,
        bool HasUnsupportedTierSyntax);

    private sealed class TokenSequenceComparer : IEqualityComparer<string[]>
    {
        public static TokenSequenceComparer Instance { get; } = new();

        public bool Equals(string[]? left, string[]? right) =>
            ReferenceEquals(left, right) || left is not null && right is not null && left.SequenceEqual(right, StringComparer.Ordinal);

        public int GetHashCode(string[] tokens)
        {
            var hash = new HashCode();
            foreach (string token in tokens)
            {
                hash.Add(token, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }
}
