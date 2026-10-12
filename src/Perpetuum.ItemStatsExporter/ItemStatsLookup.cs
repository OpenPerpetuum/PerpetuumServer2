using System.Text.RegularExpressions;

namespace Perpetuum.ItemStatsExporter;

internal static class ItemStatsLookup
{
    public static ItemStatsLookupResponse Resolve(
        string question,
        IReadOnlyCollection<WeaponItem> items,
        IReadOnlyCollection<ProductionRecipe>? recipes = null,
        string? contextItem = null)
    {
        var catalog = items
            .Where(item => ToPublicDisplayName(item.DisplayName, item.DefinitionName) is not null)
            .Select(item => new LookupItem
            {
                DefinitionName = item.DefinitionName,
                DisplayName = ToPublicDisplayName(item.DisplayName, item.DefinitionName)!,
                ContextDisplayName = ToPublicDisplayName(item.DisplayName, item.DefinitionName),
                Stats = item.AnswerView.Stats,
                AmmoVariants = item.AnswerView.AmmoVariants
            })
            .ToDictionary(item => item.DefinitionName, StringComparer.OrdinalIgnoreCase);

        foreach (var recipe in recipes ?? [])
        {
            if (recipe.AnswerView is null)
            {
                continue;
            }

            if (!catalog.TryGetValue(recipe.DefinitionName, out var item))
            {
                var publicDisplayName = ToPublicDisplayName(recipe.DisplayName, recipe.DefinitionName);
                if (publicDisplayName is null)
                {
                    continue;
                }

                item = new LookupItem
                {
                    DefinitionName = recipe.DefinitionName,
                    DisplayName = publicDisplayName,
                    ContextDisplayName = publicDisplayName
                };
                catalog.Add(item.DefinitionName, item);
            }

            item.Recipe = recipe.AnswerView;
            item.FullMaterials = recipe.FullMaterialsAnswerView;
        }

        var normalizedQuestion = Normalize(question);
        var matches = string.IsNullOrEmpty(normalizedQuestion)
            ? Array.Empty<ItemMatch>()
            : catalog.Values.Select(item => new ItemMatch(
                    item,
                    GetAliases(item)
                        .Where(alias => ContainsPhrase(normalizedQuestion, alias))
                        .ToArray()))
                .Where(match => match.Aliases.Length > 0)
                .ToArray();

        var independentMatches = matches
            .Where(match => match.Aliases.Any(alias => !IsNestedAlias(alias, match.Item, matches)))
            .OrderBy(match => match.Item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0 && independentMatches.Length == 0 &&
            IsItemFollowUp(question) && TryGetContextItem(contextItem, catalog, out var contextMatch))
        {
            independentMatches = [new ItemMatch(contextMatch, GetAliases(contextMatch))];
        }

        if (independentMatches.Length == 1)
        {
            var item = independentMatches[0].Item;
            return new ItemStatsLookupResponse
            {
                Status = "found",
                Query = question,
                Item = new ItemStatsLookupAnswer
                {
                    DefinitionName = item.DefinitionName,
                    DisplayName = item.DisplayName,
                    ContextDisplayName = item.ContextDisplayName,
                    Stats = item.Stats,
                    AmmoVariants = item.AmmoVariants,
                    Recipe = item.Recipe,
                    FullMaterials = item.FullMaterials,
                    FullMaterialsAvailable = item.FullMaterials is not null
                }
            };
        }

        return new ItemStatsLookupResponse
        {
            Status = independentMatches.Length == 0 ? "not_found" : "ambiguous",
            Query = question,
            Candidates = independentMatches
                .Select(match => new ItemStatsLookupCandidate
                {
                    DefinitionName = match.Item.DefinitionName,
                    DisplayName = match.Item.DisplayName
                })
                .ToList()
        };
    }

    private static string[] GetAliases(LookupItem item)
    {
        return new[] { Normalize(item.DisplayName), Normalize(item.DefinitionName) }
            .Where(alias => !string.IsNullOrEmpty(alias))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? ToPublicDisplayName(string? displayName, string definitionName)
    {
        return string.IsNullOrWhiteSpace(displayName) ||
               string.Equals(displayName.Trim(), definitionName, StringComparison.OrdinalIgnoreCase)
            ? null
            : displayName.Trim();
    }

    private static bool TryGetContextItem(
        string? contextItem,
        IReadOnlyDictionary<string, LookupItem> catalog,
        out LookupItem item)
    {
        item = null!;
        if (string.IsNullOrWhiteSpace(contextItem) || contextItem.Length > 256)
        {
            return false;
        }

        var matches = catalog.Values
            .Where(candidate => candidate.ContextDisplayName is not null && string.Equals(
                candidate.ContextDisplayName,
                contextItem.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        if (matches.Length != 1)
        {
            return false;
        }

        item = matches[0];
        return true;
    }

    private static bool IsItemFollowUp(string question)
    {
        var normalized = Normalize(question);
        return Regex.IsMatch(
            normalized,
            @"\b(it|its|they|them|their|this|that|these|those)\b|\b(what|how) about\b|^and\b|\bsame (item|one)\b|^(?:(?:what|list|show) )?(?:(?:are|is) )?(?:the )?(?:stats?|attributes?|ammo|ammunition|recipe|materials?|components?|full materials|full components|production cost|cost|range|accuracy|damage)(?: list)?$",
            RegexOptions.CultureInvariant);
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', Regex.Matches(value.ToLowerInvariant(), @"[\p{L}\p{N}]+")
            .Select(match => match.Value));
    }

    private static bool ContainsPhrase(string text, string phrase)
    {
        return $" {text} ".Contains($" {phrase} ", StringComparison.Ordinal);
    }

    private static bool IsNestedAlias(string alias, LookupItem owner, IReadOnlyCollection<ItemMatch> matches)
    {
        return matches.Any(other => other.Item != owner && other.Aliases.Any(otherAlias =>
            otherAlias.Length > alias.Length && ContainsPhrase(otherAlias, alias)));
    }

    private sealed class LookupItem
    {
        public string DefinitionName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? ContextDisplayName { get; init; }
        public List<AnswerStat> Stats { get; init; } = [];
        public List<AmmoAnswerView> AmmoVariants { get; init; } = [];
        public ProductionRecipeAnswerView? Recipe { get; set; }
        public FullMaterialsAnswerView? FullMaterials { get; set; }
    }

    private sealed record ItemMatch(LookupItem Item, string[] Aliases);
}

internal sealed class ItemStatsLookupResponse
{
    public string Status { get; init; } = string.Empty;
    public string Query { get; init; } = string.Empty;
    public ItemStatsLookupAnswer? Item { get; init; }
    public List<ItemStatsLookupCandidate> Candidates { get; init; } = [];
}

internal sealed class ItemStatsLookupAnswer
{
    public string DefinitionName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? ContextDisplayName { get; init; }
    public List<AnswerStat> Stats { get; init; } = [];
    public List<AmmoAnswerView> AmmoVariants { get; init; } = [];
    public ProductionRecipeAnswerView? Recipe { get; init; }
    public FullMaterialsAnswerView? FullMaterials { get; init; }
    public bool FullMaterialsAvailable { get; init; }
}

internal sealed class ItemStatsLookupCandidate
{
    public string DefinitionName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
