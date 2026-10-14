using System.Security.Cryptography;
using System.Globalization;
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
    public const int CurrentSchemaVersion = 4;

    public IReadOnlyList<SparkyRobotRecord> Robots { get; init; } = [];
    public IReadOnlyList<SparkyRobotModuleRecord> RobotModules { get; init; } = [];
    public int SkippedRobotCount { get; init; }
    public SparkyEntityResolutionRules? EntityResolutionRules { get; init; }
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
        if (bundle.SchemaVersion is not (1 or 2 or 3 or SparkyKnowledgeBundle.CurrentSchemaVersion))
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

        if (bundle.SchemaVersion >= 4)
        {
            if (bundle.EntityResolutionRules is null ||
                !bundle.Manifest.SourceHashes.TryGetValue("sparkyEntityResolutionRules", out string? rulesHash) ||
                rulesHash.Length != 64)
            {
                throw new InvalidDataException("Schema 4 Sparky bundles require validated entity-resolution rules and their SHA-256 source hash.");
            }

            try
            {
                bundle.EntityResolutionRules.Validate();
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException("Schema 4 Sparky bundles contain invalid entity-resolution rules.", exception);
            }
        }

        if (bundle.Languages is null || bundle.Languages.Count == 0 ||
            bundle.Items is null || bundle.ProductionRecipes is null ||
            bundle.KnowledgePassages is null || bundle.SearchIndex?.TokenToPassageIndexes is null ||
            bundle.Robots is null || bundle.RobotModules is null)
        {
            throw new InvalidDataException("The Sparky bundle is missing one or more required sections.");
        }

        if (bundle.SchemaVersion == 1 &&
            (bundle.Robots.Count != 0 || bundle.RobotModules.Count != 0 || bundle.SkippedRobotCount != 0))
        {
            throw new InvalidDataException("Schema 1 Sparky bundles must not contain robot knowledge data.");
        }

        if (bundle.SkippedRobotCount < 0)
        {
            throw new InvalidDataException("The Sparky bundle contains an invalid skipped-robot count.");
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
                languages.Keys.Any(languageId => !item.LocalizedViews.ContainsKey(languageId)) ||
                bundle.SchemaVersion >= 4 && item.TierMetadata is null)
            {
                throw new InvalidDataException("The Sparky bundle contains an invalid item, missing schema 4 tier metadata, or incomplete localized item views.");
            }
        }

        var recipeDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var recipe in bundle.ProductionRecipes)
        {
            if (recipe is null || string.IsNullOrWhiteSpace(recipe.DefinitionName) ||
                bundle.SchemaVersion >= 4 && !recipeDefinitions.Add(recipe.DefinitionName) ||
                recipe.LocalizedViews is null ||
                languages.Keys.Any(languageId => !recipe.LocalizedViews.ContainsKey(languageId)) ||
                bundle.SchemaVersion >= 4 && recipe.TierMetadata is null)
            {
                throw new InvalidDataException("The Sparky bundle contains an invalid or duplicate recipe, missing schema 4 tier metadata, or incomplete localized recipe views.");
            }
        }

        ValidateRobotSections(bundle, languages.Keys);

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

        var storedAmbiguities = bundle.SearchIndex.AmbiguousAliases;
        if (storedAmbiguities is null || storedAmbiguities.Any(ambiguity =>
                ambiguity is null || string.IsNullOrWhiteSpace(ambiguity.Alias) || ambiguity.Definitions is null ||
                ambiguity.Definitions.Count < 2 || ambiguity.Definitions.Any(string.IsNullOrWhiteSpace) ||
                ambiguity.Definitions.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ambiguity.Definitions.Count))
        {
            throw new InvalidDataException("The Sparky bundle contains an invalid alias ambiguity record.");
        }

        var expectedAmbiguities = SparkyAliasCatalog.Build(
            bundle.Languages,
            bundle.Items,
            bundle.ProductionRecipes,
            bundle.Robots,
            bundle.RobotModules);
        if (expectedAmbiguities.Count != storedAmbiguities.Count ||
            expectedAmbiguities.Where((expected, index) =>
                expected.LanguageId != storedAmbiguities[index].LanguageId ||
                !string.Equals(expected.Alias, storedAmbiguities[index].Alias, StringComparison.Ordinal) ||
                !expected.Definitions.SequenceEqual(storedAmbiguities[index].Definitions, StringComparer.Ordinal)).Any())
        {
            throw new InvalidDataException("The Sparky bundle alias ambiguity records do not match its localized item, recipe, robot, and module names.");
        }
    }

    private static void ValidateRobotSections(SparkyKnowledgeBundle bundle, IEnumerable<int> languageIds)
    {
        var languageSet = languageIds.ToHashSet();
        var moduleDefinitions = new HashSet<string>(
            bundle.RobotModules.Where(module => module is not null && !string.IsNullOrWhiteSpace(module.DefinitionName))
                .Select(module => module.DefinitionName),
            StringComparer.OrdinalIgnoreCase);
        var robotDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SparkyRobotRecord? robot in bundle.Robots)
        {
            if (robot is null || string.IsNullOrWhiteSpace(robot.DefinitionName) ||
                !robotDefinitions.Add(robot.DefinitionName) || robot.ComponentDefinitionNames is null ||
                robot.ComponentDefinitionNames.Any(string.IsNullOrWhiteSpace) ||
                robot.ComponentDefinitionNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != robot.ComponentDefinitionNames.Count ||
                robot.Compatibility is null || robot.ChassisBonusSources is null || robot.LocalizedViews is null ||
                bundle.SchemaVersion >= 4 && robot.TierMetadata is null ||
                languageSet.Any(languageId => !robot.LocalizedViews.ContainsKey(languageId)) ||
                robot.LocalizedViews.Keys.Any(languageId => !languageSet.Contains(languageId)))
            {
                throw new InvalidDataException("The Sparky bundle contains an invalid robot record or incomplete localized robot views.");
            }

            var componentDefinitions = new HashSet<string>(robot.ComponentDefinitionNames, StringComparer.OrdinalIgnoreCase);
            foreach (SparkyRobotChassisBonusSource? source in robot.ChassisBonusSources)
            {
                if (source is null || source.ExtensionId <= 0 || source.AggregateFieldId <= 0 ||
                    !double.IsFinite(source.BonusPerLevel) || source.Metadata is null ||
                    !double.IsFinite(source.Metadata.MeasurementMultiplier) ||
                    !double.IsFinite(source.Metadata.MeasurementOffset) || source.ComponentDefinitionNames is null ||
                    source.ComponentDefinitionNames.Count == 0 ||
                    source.ComponentDefinitionNames.Any(name => !componentDefinitions.Contains(name)))
                {
                    throw new InvalidDataException($"Robot '{robot.DefinitionName}' contains an invalid chassis-bonus source.");
                }
            }

            var componentSlotCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<int, SparkyLocalizedRobotView> localizedEntry in robot.LocalizedViews)
            {
                SparkyLocalizedRobotView? view = localizedEntry.Value;
                if (view is null || view.AssembledStats is null || view.Components is null || view.ChassisBonuses is null ||
                    !IsSafeOptionalPublicText(view.DisplayName, [robot.DefinitionName]))
                {
                    throw new InvalidDataException($"Robot '{robot.DefinitionName}' contains an invalid localized view.");
                }

                ValidateStats(view.AssembledStats, $"robot '{robot.DefinitionName}' assembled stats", [robot.DefinitionName]);
                var localizedComponentDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (SparkyLocalizedRobotComponentView? component in view.Components)
                {
                    if (component is null || string.IsNullOrWhiteSpace(component.DefinitionName) ||
                        !componentDefinitions.Contains(component.DefinitionName) || component.SlotCount < 0 ||
                        string.IsNullOrWhiteSpace(component.DisplayName) ||
                        !localizedComponentDefinitions.Add(component.DefinitionName) ||
                        !IsSafePublicText(component.DisplayName, [robot.DefinitionName, component.DefinitionName]) ||
                        component.Stats is null)
                    {
                        throw new InvalidDataException($"Robot '{robot.DefinitionName}' contains an invalid localized component.");
                    }

                    componentSlotCounts[component.DefinitionName] = component.SlotCount;
                    ValidateStats(component.Stats, $"robot '{robot.DefinitionName}' component stats", [robot.DefinitionName, component.DefinitionName]);
                }

                foreach (SparkyLocalizedChassisBonus? bonus in view.ChassisBonuses)
                {
                    var forbiddenNames = new List<string> { robot.DefinitionName };
                    forbiddenNames.AddRange(robot.ComponentDefinitionNames);
                    if (bonus is null || bonus.ExtensionLevel != 1 ||
                        !IsFiniteNumber(bonus.Value) || string.IsNullOrWhiteSpace(bonus.ExtensionName) ||
                        !IsSafePublicText(bonus.ExtensionName, forbiddenNames) ||
                        string.IsNullOrWhiteSpace(bonus.PropertyName) ||
                        !IsSafePublicText(bonus.PropertyName, forbiddenNames) || bonus.ComponentNames is null ||
                        bonus.ComponentNames.Count == 0 ||
                        bonus.ComponentNames.Any(name => string.IsNullOrWhiteSpace(name) || !IsSafePublicText(name, forbiddenNames)) ||
                        !IsSafeOptionalPublicText(bonus.Unit, forbiddenNames))
                    {
                        throw new InvalidDataException($"Robot '{robot.DefinitionName}' contains an invalid localized chassis bonus.");
                    }
                }
            }

            var compatibilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SparkyRobotCompatibility? relation in robot.Compatibility)
            {
                if (relation is null || string.IsNullOrWhiteSpace(relation.ComponentDefinitionName) ||
                    string.IsNullOrWhiteSpace(relation.ModuleDefinitionName) ||
                    !componentDefinitions.Contains(relation.ComponentDefinitionName) ||
                    !moduleDefinitions.Contains(relation.ModuleDefinitionName) || relation.EligibleSlots is null ||
                    relation.EligibleSlots.Count == 0 || relation.EligibleSlots.Any(slot => slot < 1) ||
                    relation.EligibleSlots.Distinct().Count() != relation.EligibleSlots.Count ||
                    componentSlotCounts.TryGetValue(relation.ComponentDefinitionName, out int slotCount) &&
                    relation.EligibleSlots.Any(slot => slot > slotCount) ||
                    !compatibilityKeys.Add($"{relation.ComponentDefinitionName}\0{relation.ModuleDefinitionName}"))
                {
                    throw new InvalidDataException($"Robot '{robot.DefinitionName}' contains an invalid or dangling compatibility reference.");
                }
            }
        }

        var seenModuleDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SparkyRobotModuleRecord? module in bundle.RobotModules)
        {
            if (module is null || string.IsNullOrWhiteSpace(module.DefinitionName) ||
                !seenModuleDefinitions.Add(module.DefinitionName) ||
                (bundle.SchemaVersion >= 3 && module.IsLaser is null) ||
                (bundle.SchemaVersion >= 4 && module.TierMetadata is null) ||
                module.LocalizedViews is null ||
                languageSet.Any(languageId => !module.LocalizedViews.ContainsKey(languageId)) ||
                module.LocalizedViews.Keys.Any(languageId => !languageSet.Contains(languageId)))
            {
                throw new InvalidDataException("The Sparky bundle contains an invalid robot module or incomplete localized module views.");
            }

            foreach (SparkyLocalizedRobotModuleView? view in module.LocalizedViews.Values)
            {
                if (view is null || view.CpuAndPowerGridStats is null ||
                    !IsSafeOptionalPublicText(view.DisplayName, [module.DefinitionName]))
                {
                    throw new InvalidDataException($"Robot module '{module.DefinitionName}' contains an invalid localized view.");
                }

                ValidateStats(view.CpuAndPowerGridStats, $"robot module '{module.DefinitionName}' stats", [module.DefinitionName]);
            }
        }
    }

    private static void ValidateStats(IEnumerable<AnswerStat> stats, string description, IReadOnlyList<string> internalNames)
    {
        foreach (AnswerStat? stat in stats)
        {
            if (stat is null || string.IsNullOrWhiteSpace(stat.Label) || !IsSafePublicText(stat.Label, internalNames) || !IsFiniteNumber(stat.Value) ||
                !IsSafeOptionalPublicText(stat.Unit, internalNames))
            {
                throw new InvalidDataException($"The Sparky bundle contains invalid or non-finite {description}.");
            }
        }
    }

    private static bool IsFiniteNumber(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number);
    }

    private static bool IsSafeOptionalPublicText(string? value, IReadOnlyList<string> internalNames)
    {
        return value is null || (!string.IsNullOrWhiteSpace(value) && IsSafePublicText(value, internalNames));
    }

    private static bool IsSafePublicText(string? value, IReadOnlyList<string> internalNames)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               (!internalNames.Any(name => string.Equals(value, name, StringComparison.OrdinalIgnoreCase)) &&
                !Regex.IsMatch(value, @"\A(?:def_[a-z0-9_]+|ext_[a-z0-9_]+|Aggregate_(?:unknown_)?[0-9]+|[0-9]+)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }
}

internal static class SparkyAliasCatalog
{
    private static readonly Regex WordPattern = new("[\\p{L}\\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<SparkyAliasAmbiguity> Build(
        IReadOnlyList<SparkySupportedLanguage> languages,
        IReadOnlyList<WeaponItem> items,
        IReadOnlyList<ProductionRecipe> recipes,
        IReadOnlyList<SparkyRobotRecord>? robots = null,
        IReadOnlyList<SparkyRobotModuleRecord>? robotModules = null)
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

            foreach (SparkyRobotRecord robot in robots ?? [])
            {
                robot.LocalizedViews.TryGetValue(language.LanguageId, out SparkyLocalizedRobotView? view);
                Add(view?.DisplayName, robot.DefinitionName);
            }

            foreach (SparkyRobotModuleRecord module in robotModules ?? [])
            {
                module.LocalizedViews.TryGetValue(language.LanguageId, out SparkyLocalizedRobotModuleView? view);
                Add(view?.DisplayName, module.DefinitionName);
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
