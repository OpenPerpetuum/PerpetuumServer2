using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Perpetuum;
using Perpetuum.Services.Sparky;

namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyKnowledgeBundle(
    int SchemaVersion,
    SparkyBundleManifest Manifest,
    IReadOnlyList<SparkySupportedLanguage> Languages,
    IReadOnlyList<WeaponItem> Items,
    IReadOnlyList<ProductionRecipe> ProductionRecipes,
    IReadOnlyList<SparkyKnowledgePassage> KnowledgePassages,
    SparkySearchIndex SearchIndex)
{
    public const int CurrentSchemaVersion = 1;
}

internal sealed record SparkyBundleManifest(
    string BundleVersion,
    string GameContentRevision,
    DateTimeOffset BuiltAtUtc,
    IReadOnlyDictionary<int, string> DictionaryHashes,
    IReadOnlyDictionary<string, string> SourceHashes);

internal sealed record SparkySupportedLanguage(
    int LanguageId,
    string DisplayName,
    string DictionaryHash,
    IReadOnlyDictionary<string, string> Translations);

internal sealed record SparkyKnowledgePassage(
    string SourceId,
    string SourceCategory,
    string SourceRevision,
    string Sha256,
    string Text);

internal sealed record SparkySearchIndex(
    IReadOnlyDictionary<string, IReadOnlyList<int>> TokenToPassageIndexes,
    IReadOnlyList<SparkyAliasAmbiguity> AmbiguousAliases);

internal sealed record SparkyAliasAmbiguity(int LanguageId, string Alias, IReadOnlyList<string> Definitions);

internal static class SparkyKnowledgeIndexLimits
{
    public const int MaxPassageCharacters = 1600;
}

internal sealed class SparkyLocalizedItemView
{
    public string? DisplayName { get; init; }
    public bool HasLocalizedDisplayName { get; init; }
    public ItemAnswerView AnswerView { get; init; } = new();
}

internal sealed class SparkyLocalizedRecipeView
{
    public string? DisplayName { get; init; }
    public bool HasCompleteTranslations { get; init; }
    public ProductionRecipeAnswerView? AnswerView { get; init; }
    public FullMaterialsAnswerView? FullMaterialsAnswerView { get; init; }
}

internal static class SparkyKnowledgeBundleValidator
{
    public static void Validate(SparkyKnowledgeBundle bundle)
    {
        if (bundle.SchemaVersion != SparkyKnowledgeBundle.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported Sparky bundle schema version {bundle.SchemaVersion}.");
        }

        if (bundle.Manifest is null ||
            string.IsNullOrWhiteSpace(bundle.Manifest.BundleVersion) ||
            string.IsNullOrWhiteSpace(bundle.Manifest.GameContentRevision) ||
            bundle.Manifest.BuiltAtUtc == default ||
            bundle.Manifest.DictionaryHashes is null ||
            bundle.Manifest.SourceHashes is null || bundle.Manifest.SourceHashes.Count == 0 ||
            bundle.Manifest.SourceHashes.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key) ||
                entry.Value is null ||
                entry.Value.Length is not (40 or 64) ||
                !entry.Value.All(Uri.IsHexDigit)))
        {
            throw new InvalidDataException("The Sparky bundle manifest is incomplete.");
        }

        if (bundle.Languages is null || bundle.Languages.Count == 0 ||
            bundle.Items is null || bundle.ProductionRecipes is null ||
            bundle.KnowledgePassages is null || bundle.SearchIndex?.TokenToPassageIndexes is null)
        {
            throw new InvalidDataException("The Sparky bundle is missing one or more required sections.");
        }

        var languages = new Dictionary<int, SparkySupportedLanguage>();
        foreach (var language in bundle.Languages)
        {
            if (language is null ||
                !CustomDictionary.LanguageNames.TryGetValue(language.LanguageId, out var expectedName) ||
                !string.Equals(language.DisplayName, expectedName, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(language.DictionaryHash) ||
                language.Translations is null ||
                !languages.TryAdd(language.LanguageId, language))
            {
                throw new InvalidDataException("The Sparky bundle contains an invalid or duplicate language entry.");
            }

            string actualHash = SparkyDictionaryCatalog.ComputeDictionaryHash(language.Translations);
            if (!string.Equals(actualHash, language.DictionaryHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Dictionary hash validation failed for language {language.LanguageId}.");
            }
        }

        if (!languages.ContainsKey(0))
        {
            throw new InvalidDataException("The Sparky bundle must include the canonical English dictionary (language 0).");
        }

        SparkyResponseTemplates.ValidateCoverage(languages.Keys);

        var manifestLanguages = bundle.Manifest.DictionaryHashes;
        if (manifestLanguages.Count != languages.Count || languages.Any(language =>
                !manifestLanguages.TryGetValue(language.Key, out var hash) ||
                !string.Equals(hash, language.Value.DictionaryHash, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("The Sparky bundle manifest dictionary hashes do not match its language dictionaries.");
        }

        var itemDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in bundle.Items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.DefinitionName) ||
                !itemDefinitions.Add(item.DefinitionName) || item.LocalizedViews is null ||
                languages.Keys.Any(languageId => !item.LocalizedViews.ContainsKey(languageId)))
            {
                throw new InvalidDataException("The Sparky bundle contains an invalid item or incomplete localized item views.");
            }
        }

        foreach (var recipe in bundle.ProductionRecipes)
        {
            if (recipe is null || string.IsNullOrWhiteSpace(recipe.DefinitionName) ||
                recipe.LocalizedViews is null ||
                languages.Keys.Any(languageId => !recipe.LocalizedViews.ContainsKey(languageId)))
            {
                throw new InvalidDataException("The Sparky bundle contains an invalid recipe or incomplete localized recipe views.");
            }
        }

        for (var index = 0; index < bundle.KnowledgePassages.Count; index++)
        {
            var passage = bundle.KnowledgePassages[index];
            if (passage is null || string.IsNullOrWhiteSpace(passage.SourceId) ||
                passage.SourceCategory is not ("mechanics" or "lore") ||
                !Regex.IsMatch(passage.SourceId, @"\A[a-zA-Z0-9_-]{1,80}\z") ||
                string.IsNullOrWhiteSpace(passage.SourceRevision) ||
                string.IsNullOrWhiteSpace(passage.Sha256) || string.IsNullOrWhiteSpace(passage.Text) ||
                passage.Text.Length > SparkyKnowledgeIndexLimits.MaxPassageCharacters ||
                !bundle.Manifest.SourceHashes.TryGetValue(passage.SourceId, out var sourceRevision) ||
                !string.Equals(sourceRevision, passage.SourceRevision, StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(passage.Text, @"(?i)([a-z]:\\|/src/|\.cs\b|password\s*[:=]|bearer\s+[a-z0-9._-]{16,}|connection string|select\s+\*\s+from|insert\s+into|delete\s+from)"))
            {
                throw new InvalidDataException($"Sparky knowledge passage {index} has invalid, unsafe, or incomplete provenance/text.");
            }

            var actualHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passage.Text)));
            if (!string.Equals(actualHash, passage.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Sparky knowledge passage {index} failed its SHA-256 check.");
            }
        }

        foreach (var entry in bundle.SearchIndex.TokenToPassageIndexes)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value is null ||
                entry.Value.Any(index => index < 0 || index >= bundle.KnowledgePassages.Count))
            {
                throw new InvalidDataException("The Sparky bundle search index contains an invalid token or passage reference.");
            }
        }

        var expectedIndex = SparkyKnowledgeIndexBuilder.BuildSearchIndex(bundle.KnowledgePassages).TokenToPassageIndexes;
        if (expectedIndex.Count != bundle.SearchIndex.TokenToPassageIndexes.Count ||
            expectedIndex.Any(entry =>
                !bundle.SearchIndex.TokenToPassageIndexes.TryGetValue(entry.Key, out var actual) ||
                !entry.Value.SequenceEqual(actual)))
        {
            throw new InvalidDataException("The Sparky bundle search index does not match its passage text.");
        }

        var expectedAmbiguities = SparkyAliasCatalog.Build(bundle.Languages, bundle.Items, bundle.ProductionRecipes);
        if (bundle.SearchIndex.AmbiguousAliases is null ||
            expectedAmbiguities.Count != bundle.SearchIndex.AmbiguousAliases.Count ||
            expectedAmbiguities.Where((expected, index) =>
                expected.LanguageId != bundle.SearchIndex.AmbiguousAliases[index].LanguageId ||
                !string.Equals(expected.Alias, bundle.SearchIndex.AmbiguousAliases[index].Alias, StringComparison.Ordinal) ||
                !expected.Definitions.SequenceEqual(bundle.SearchIndex.AmbiguousAliases[index].Definitions, StringComparer.Ordinal)).Any())
        {
            throw new InvalidDataException("The Sparky bundle alias ambiguity records do not match its localized item and recipe names.");
        }
    }
}

internal static class SparkyAliasCatalog
{
    private static readonly Regex WordPattern = new("[\\p{L}\\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<SparkyAliasAmbiguity> Build(
        IReadOnlyList<SparkySupportedLanguage> languages,
        IReadOnlyList<WeaponItem> items,
        IReadOnlyList<ProductionRecipe> recipes)
    {
        var ambiguities = new List<SparkyAliasAmbiguity>();
        foreach (var language in languages.OrderBy(language => language.LanguageId))
        {
            var aliases = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                item.LocalizedViews.TryGetValue(language.LanguageId, out var view);
                string? localizedName = view?.HasLocalizedDisplayName == true ? view.DisplayName : null;
                Add(localizedName, item.DefinitionName);
                Add(item.DisplayName, item.DefinitionName);
            }

            foreach (var recipe in recipes)
            {
                recipe.LocalizedViews.TryGetValue(language.LanguageId, out var view);
                Add(view?.DisplayName, recipe.DefinitionName);
                Add(recipe.DisplayName, recipe.DefinitionName);
            }

            ambiguities.AddRange(aliases
                .Where(entry => entry.Value.Count > 1)
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new SparkyAliasAmbiguity(
                    language.LanguageId,
                    entry.Key,
                    entry.Value.OrderBy(name => name, StringComparer.Ordinal).ToArray())));

            void Add(string? name, string definitionName)
            {
                if (string.IsNullOrWhiteSpace(name) || string.Equals(name.Trim(), definitionName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string alias = Normalize(name);
                if (alias.Length == 0)
                {
                    return;
                }

                if (!aliases.TryGetValue(alias, out var definitions))
                {
                    definitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    aliases.Add(alias, definitions);
                }

                definitions.Add(definitionName);
            }
        }

        return ambiguities;
    }

    private static string Normalize(string value) => string.Join(' ', WordPattern.Matches(value)
        .Select(match => match.Value.ToLowerInvariant()));
}
