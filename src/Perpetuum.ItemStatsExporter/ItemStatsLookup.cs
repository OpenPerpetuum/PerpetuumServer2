using System.Text.RegularExpressions;

namespace Perpetuum.ItemStatsExporter;

internal static class ItemStatsLookup
{
    public static ItemStatsLookupResponse Resolve(
        string question,
        SparkyKnowledgeBundle bundle,
        int languageId,
        string? contextItem = null,
        int? contextLanguageId = null,
        string? englishFollowUpQuery = null)
    {
        var language = bundle.Languages.FirstOrDefault(candidate => candidate.LanguageId == languageId);
        var english = bundle.Languages.FirstOrDefault(candidate => candidate.LanguageId == 0);
        if (language is null || english is null)
        {
            return new ItemStatsLookupResponse { Status = "not_found", Query = question };
        }

        var catalog = new Dictionary<string, LookupItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in bundle.Items)
        {
            string? englishName = ToPublicDisplayName(item.DisplayName, item.DefinitionName);
            if (englishName is null)
            {
                continue;
            }

            item.LocalizedViews.TryGetValue(languageId, out var localizedView);
            bool hasLocalizedName = localizedView?.HasLocalizedDisplayName == true;
            string displayName = hasLocalizedName ? localizedView!.DisplayName! : englishName;
            string[] aliases = new[] { hasLocalizedName ? localizedView!.DisplayName : null, englishName }
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Select(alias => Normalize(alias!))
                .Where(alias => alias.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string[] contextAliases = item.LocalizedViews.Values
                .Where(view => view.HasLocalizedDisplayName && !string.IsNullOrWhiteSpace(view.DisplayName))
                .Select(view => Normalize(view.DisplayName!))
                .Append(Normalize(englishName))
                .Where(alias => alias.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            catalog[item.DefinitionName] = new LookupItem
            {
                DefinitionName = item.DefinitionName,
                DisplayName = displayName,
                ContextDisplayName = displayName,
                PublicAliases = aliases,
                ContextAliases = contextAliases,
                Stats = localizedView?.AnswerView.Stats ?? [],
                AmmoVariants = localizedView?.AnswerView.AmmoVariants ?? []
            };
        }

        foreach (var recipe in bundle.ProductionRecipes)
        {
            string? englishName = ToPublicDisplayName(recipe.DisplayName, recipe.DefinitionName);
            recipe.LocalizedViews.TryGetValue(languageId, out var localizedRecipe);
            string? localizedName = ToPublicDisplayName(localizedRecipe?.DisplayName, recipe.DefinitionName);
            string? publicName = localizedName ?? englishName;
            if (publicName is null)
            {
                continue;
            }

            if (!catalog.TryGetValue(recipe.DefinitionName, out var item))
            {
                string[] aliases = new[] { localizedName, englishName }
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Select(alias => Normalize(alias!))
                    .Where(alias => alias.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                string[] contextAliases = recipe.LocalizedViews.Values
                    .Where(view => !string.IsNullOrWhiteSpace(view.DisplayName))
                    .Select(view => Normalize(view.DisplayName!))
                    .Append(englishName is null ? string.Empty : Normalize(englishName))
                    .Where(alias => alias.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                item = new LookupItem
                {
                    DefinitionName = recipe.DefinitionName,
                    DisplayName = publicName,
                    ContextDisplayName = publicName,
                    PublicAliases = aliases,
                    ContextAliases = contextAliases
                };
                catalog.Add(item.DefinitionName, item);
            }

            item.Recipe = localizedRecipe?.AnswerView;
            item.FullMaterials = localizedRecipe?.FullMaterialsAnswerView;
            item.RecipeAvailable = true;
            item.RecipeTranslationsComplete = localizedRecipe?.HasCompleteTranslations == true;
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

        bool isFollowUp = IsItemFollowUp(question) || IsItemFollowUp(englishFollowUpQuery ?? string.Empty);
        if (matches.Length == 0 && independentMatches.Length == 0 &&
            isFollowUp && TryGetContextItem(contextItem, contextLanguageId, catalog, out var contextMatch))
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
                    LanguageId = languageId,
                    Stats = item.Stats,
                    AmmoVariants = item.AmmoVariants,
                    Recipe = item.Recipe,
                    RecipeAvailable = item.RecipeAvailable,
                    RecipeTranslationsComplete = item.RecipeTranslationsComplete,
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
        if (item.PublicAliases is not null)
        {
            return item.PublicAliases;
        }

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
        return TryGetContextItem(contextItem, null, catalog, out item);
    }

    private static bool TryGetContextItem(
        string? contextItem,
        int? contextLanguageId,
        IReadOnlyDictionary<string, LookupItem> catalog,
        out LookupItem item)
    {
        item = null!;
        if (string.IsNullOrWhiteSpace(contextItem) || contextItem.Length > 256)
        {
            return false;
        }

        var normalizedContextItem = Normalize(contextItem.Trim());
        var matches = catalog.Values
            .Where(candidate => candidate.ContextAliases is not null
                ? candidate.ContextAliases.Contains(normalizedContextItem, StringComparer.Ordinal)
                : candidate.ContextDisplayName is not null && string.Equals(
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
        public string[]? PublicAliases { get; init; }
        public string[]? ContextAliases { get; init; }
        public bool RecipeAvailable { get; set; }
        public bool RecipeTranslationsComplete { get; set; } = true;
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
    public int LanguageId { get; init; }
    public List<AnswerStat> Stats { get; init; } = [];
    public List<AmmoAnswerView> AmmoVariants { get; init; } = [];
    public ProductionRecipeAnswerView? Recipe { get; init; }
    public bool RecipeAvailable { get; init; }
    public bool RecipeTranslationsComplete { get; init; }
    public FullMaterialsAnswerView? FullMaterials { get; init; }
    public bool FullMaterialsAvailable { get; init; }
}

internal sealed class ItemStatsLookupCandidate
{
    public string DefinitionName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
