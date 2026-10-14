using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyItemTierLookupTests
{
    private static readonly SparkyEntityResolutionRules Rules = SparkyEntityResolutionRules.Load(TestRepositoryRoot);

    [Fact]
    public void TieredShortNameSelectsTheMatchingItemStats()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(
            Item("def_standard_medium_plate", "Standard medium armor plate", 1, 1, "Armor max", "100"),
            Item("def_t2_medium_plate", "Standard medium armor plate", 1, 2, "Armor max", "200"));

        ItemStatsLookupResponse result = ItemStatsLookup.Resolve("T1 medium plate", bundle, languageId: 0);

        Assert.Equal("found", result.Status);
        Assert.Equal("Standard medium armor plate", result.Item!.DisplayName);
        Assert.Equal("100", Assert.Single(result.Item.Stats).Value);
    }

    [Fact]
    public void PlusTierSelectsTheMatchingRecipeAndFullMaterials()
    {
        const string definition = "def_scorcher_t2plus";
        SparkyKnowledgeBundle bundle = CreateBundle(
            Item(definition, "Scorcher", 3, 2, "Damage", "25"),
            Recipe(definition, "Scorcher", 3, 2, "Axicoline", 100));

        ItemStatsLookupResponse result = ItemStatsLookup.Resolve("What is the full recipe for T2+ Scorcher?", bundle, 0);

        Assert.Equal("found", result.Status);
        Assert.Equal("Scorcher", result.Item!.DisplayName);
        Assert.NotNull(result.Item.Recipe);
        Assert.Equal("Axicoline", Assert.Single(result.Item.Recipe!.Components).DisplayName);
        Assert.Equal("HDT", Assert.Single(result.Item.FullMaterials!.Materials).DisplayName);
    }

    [Fact]
    public void TierFourPrototypeSelectsExactSensorAmplifier()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(
            Item("def_sensor_amplifier_t4p", "Sensor amplifier", 2, 4, "CPU usage", "10"),
            Item("def_sensor_amplifier_t4", "Sensor amplifier", 1, 4, "CPU usage", "20"));

        ItemStatsLookupResponse result = ItemStatsLookup.Resolve(
            "T4 prototype sensor amplifier", bundle, 0);

        Assert.Equal("found", result.Status);
        Assert.Equal("10", Assert.Single(result.Item!.Stats).Value);
    }

    [Fact]
    public void RobotOnlyPrototypeAliasRemainsPartOfAnItemName()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(
            Item("def_armor_plate", "Armor plate", 1, 1, "Armor max", "100"),
            Item("def_prototype_armor_plate", "Prototype armor plate", 2, 1, "Armor max", "150"));

        ItemStatsLookupResponse result = ItemStatsLookup.Resolve(
            "What are the stats for prototype armor plate?", bundle, 0);

        Assert.Equal("found", result.Status);
        Assert.Equal("Prototype armor plate", result.Item!.DisplayName);
        Assert.Equal("150", Assert.Single(result.Item.Stats).Value);
    }

    [Theory]
    [InlineData("CT medium plate")]
    [InlineData("blueprint medium plate")]
    public void CalibrationCategorySelectsRecipeProductWithExactCprgSuffix(string query)
    {
        const string calibrationDefinition = "def_medium_plate_cprg";
        SparkyKnowledgeBundle bundle = CreateBundle(
            Item("def_medium_plate", "Medium plate", 1, 1, "Armor max", "100"),
            Item(calibrationDefinition, "Medium plate calibration template", 1, 1, "Calibration", "1"),
            Recipe(calibrationDefinition, "Medium plate calibration template", 1, 1, "Titanium", 10));

        ItemStatsLookupResponse result = ItemStatsLookup.Resolve(query, bundle, 0);

        Assert.Equal("found", result.Status);
        Assert.Equal(calibrationDefinition, result.Item!.DefinitionName);
        Assert.NotNull(result.Item.Recipe);
    }

    [Fact]
    public void SameNameAcrossTiersRemainsAmbiguousWithoutTierQualifier()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(
            Item("def_medium_plate_t1", "Medium armor plate", 1, 1),
            Item("def_medium_plate_t2", "Medium armor plate", 1, 2));

        ItemStatsLookupResponse result = ItemStatsLookup.Resolve("medium armor plate", bundle, 0);

        Assert.Equal("ambiguous", result.Status);
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, candidate => Assert.Equal("Medium armor plate", candidate.DisplayName));
    }

    [Fact]
    public void ConflictingTierTermsAskForClarificationInsteadOfFallingBackToNameOnly()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(
            Item("def_medium_plate_t1", "Medium armor plate", 1, 1),
            Item("def_medium_plate_t2", "Medium armor plate", 1, 2));

        ItemStatsLookupResponse result = ItemStatsLookup.Resolve("T1 T2 medium plate", bundle, 0);

        Assert.Equal("ambiguous", result.Status);
        Assert.Empty(result.Candidates);
        Assert.Null(result.Item);
    }

    [Fact]
    public void ExplicitUnsupportedTierDoesNotFallBackToPriorItemContext()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(
            Item("def_medium_plate_t1", "Standard medium armor plate", 1, 1));

        ItemStatsLookupResponse result = ItemStatsLookup.Resolve(
            "What about its stats for T9 medium plate?", bundle, 0,
            contextItem: "Standard medium armor plate", contextLanguageId: 0);

        Assert.Equal("not_found", result.Status);
        Assert.Null(result.Item);
    }

    [Fact]
    public void GenericShortenedNameRemainsAmbiguousAcrossEntities()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(
            Item("def_medium_plate", "Standard medium armor plate", 1, 1),
            Item("def_large_plate", "Standard large armor plate", 1, 1));

        ItemStatsLookupResponse result = ItemStatsLookup.Resolve("plate", bundle, 0);

        Assert.Equal("ambiguous", result.Status);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void LegacyBundleKeepsNameOnlyResolution()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(
            Item("def_medium_plate_t1", "Standard medium armor plate", 1, 1)) with
        {
            SchemaVersion = 3,
            EntityResolutionRules = null
        };

        ItemStatsLookupResponse result = ItemStatsLookup.Resolve("standard medium armor plate", bundle, 0);

        Assert.Equal("found", result.Status);
    }

    private static SparkyKnowledgeBundle CreateBundle(params object[] records)
    {
        WeaponItem[] items = records.OfType<WeaponItem>().ToArray();
        ProductionRecipe[] recipes = records.OfType<ProductionRecipe>().ToArray();
        var languages = new[]
        {
            new SparkySupportedLanguage(0, "English", "hash", new Dictionary<string, string>())
        };
        var manifest = new SparkyBundleManifest(
            "bundle-test", new string('A', 64), DateTimeOffset.UtcNow,
            new Dictionary<int, string> { [0] = "hash" },
            new Dictionary<string, string> { ["fixture"] = "hash" });
        var searchIndex = new SparkySearchIndex(new Dictionary<string, IReadOnlyList<int>>(), []);
        return new SparkyKnowledgeBundle(4, manifest, languages, items, recipes, [], searchIndex)
        {
            EntityResolutionRules = Rules
        };
    }

    private static WeaponItem Item(
        string definitionName,
        string displayName,
        int? tierType,
        int? tierLevel,
        string? statLabel = null,
        string? statValue = null)
    {
        var stats = statLabel is null ? [] : new List<AnswerStat>
        {
            new() { FieldId = 1, Label = statLabel, Value = statValue ?? string.Empty }
        };
        return new WeaponItem
        {
            DefinitionName = definitionName,
            DisplayName = displayName,
            TierMetadata = Tier(tierType, tierLevel),
            AnswerView = new ItemAnswerView { Stats = stats },
            LocalizedViews = new Dictionary<int, SparkyLocalizedItemView>
            {
                [0] = new() { DisplayName = displayName, HasLocalizedDisplayName = true, AnswerView = new ItemAnswerView { Stats = stats } }
            }
        };
    }

    private static ProductionRecipe Recipe(
        string definitionName,
        string displayName,
        int? tierType,
        int? tierLevel,
        string componentName,
        int componentAmount)
    {
        var direct = new ProductionRecipeAnswerView
        {
            OutputQuantity = 1,
            Components = [new ProductionRecipeAnswerComponent { DisplayName = componentName, Amount = componentAmount }]
        };
        var full = new FullMaterialsAnswerView
        {
            OutputQuantity = 1,
            Materials = [new ProductionRecipeAnswerComponent { DisplayName = "HDT", Amount = 1000 }]
        };
        return new ProductionRecipe
        {
            DefinitionName = definitionName,
            DisplayName = displayName,
            TierMetadata = Tier(tierType, tierLevel),
            AnswerView = direct,
            FullMaterialsAnswerView = full,
            LocalizedViews = new Dictionary<int, SparkyLocalizedRecipeView>
            {
                [0] = new() { DisplayName = displayName, HasCompleteTranslations = true, AnswerView = direct, FullMaterialsAnswerView = full }
            }
        };
    }

    private static SparkyEntityTierMetadata Tier(int? tierType, int? tierLevel) => new()
    {
        TierType = tierType,
        TierLevel = tierLevel
    };

    private static string TestRepositoryRoot
    {
        get
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);
            while (current is not null && !File.Exists(Path.Combine(current.FullName, "tools", "sparky-entity-resolution-rules.json")))
            {
                current = current.Parent;
            }

            return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate the entity-resolution rules catalog.");
        }
    }
}
