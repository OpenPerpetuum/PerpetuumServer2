using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyTierAwareEntityResolverTests
{
    private static readonly SparkyEntityResolutionRules Rules = SparkyEntityResolutionRules.Load(TestRepositoryRoot);

    [Fact]
    public void ResolvesEveryReviewedTierRuleAgainstItsExactPair()
    {
        foreach (SparkyEntityTierRule rule in Rules.TierRules)
        {
            const string publicName = "Example item";
            foreach (string alias in Rules.GetAliases(rule, languageId: 0))
            {
                SparkyEntityCandidate matching = Candidate(
                    $"def_matching_{rule.CanonicalLabel}", rule.EntityKind, rule.TierType, rule.TierLevel, publicName);
                SparkyEntityCandidate distractor = Candidate(
                    $"def_distractor_{rule.CanonicalLabel}", rule.EntityKind,
                    rule.TierType == 1 ? 2 : 1, rule.TierType == 1 ? 1 : 2, publicName);

                SparkyEntityResolutionResult result = Resolve(
                    $"{alias} Example item", rule.EntityKind, [matching, distractor]);

                Assert.Equal("found", result.Status);
                Assert.Equal(matching.DefinitionName, result.DefinitionName);
            }
        }
    }

    [Theory]
    [InlineData("T2+ Scorcher", "def_scorcher_t2plus", 3, 2)]
    [InlineData("tier 2 plus Scorcher", "def_scorcher_t2plus", 3, 2)]
    [InlineData("T3- Niani module", "def_module_t3minus", 3, 3)]
    [InlineData("T4 prototype sensor amplifier", "def_sensor_amplifier_t4p", 2, 4)]
    [InlineData("T1 prototype sensor amplifier", "def_sensor_amplifier_t1p", 2, 1)]
    public void ResolvesItemByTierAndRemainingName(string query, string expectedDefinitionName, int tierType, int tierLevel)
    {
        var candidates = new[]
        {
            Candidate(expectedDefinitionName, SparkyEntityKind.Item, tierType, tierLevel, "Scorcher", "Niani module", "Sensor amplifier"),
            Candidate("def_other_tier", SparkyEntityKind.Item, 1, 2, "Scorcher", "Niani module", "Sensor amplifier")
        };

        SparkyEntityResolutionResult result = Resolve(query, SparkyEntityKind.Item, candidates);

        Assert.Equal("found", result.Status);
        Assert.Equal(expectedDefinitionName, result.DefinitionName);
    }

    [Fact]
    public void ParsesPlusAndMinusTierAliasesAsDistinctFromTheirBaseTier()
    {
        SparkyEntityCandidate t2 = Candidate("def_scorcher_t2", SparkyEntityKind.Item, 1, 2, "Scorcher");
        SparkyEntityCandidate t2Plus = Candidate("def_scorcher_t2plus", SparkyEntityKind.Item, 3, 2, "Scorcher");
        SparkyEntityCandidate t3 = Candidate("def_niani_t3", SparkyEntityKind.Item, 1, 3, "Niani module");
        SparkyEntityCandidate t3Minus = Candidate("def_niani_t3minus", SparkyEntityKind.Item, 3, 3, "Niani module");

        Assert.Equal(t2.DefinitionName, Resolve("T2 Scorcher", SparkyEntityKind.Item, [t2, t2Plus]).DefinitionName);
        Assert.Equal(t2Plus.DefinitionName, Resolve("T2+ Scorcher", SparkyEntityKind.Item, [t2, t2Plus]).DefinitionName);
        Assert.Equal(t3.DefinitionName, Resolve("T3 Niani module", SparkyEntityKind.Item, [t3, t3Minus]).DefinitionName);
        Assert.Equal(t3Minus.DefinitionName, Resolve("T3- Niani module", SparkyEntityKind.Item, [t3, t3Minus]).DefinitionName);
    }

    [Fact]
    public void LongestTierAliasMakesT1PrototypeOneDescriptor()
    {
        SparkyEntityCandidate prototype = Candidate("def_sensor_t1p", SparkyEntityKind.Item, 2, 1, "Sensor amplifier");
        SparkyEntityCandidate standard = Candidate("def_sensor_t1", SparkyEntityKind.Item, 1, 1, "Sensor amplifier");

        SparkyEntityResolutionResult result = Resolve(
            "T1 prototype sensor amplifier", SparkyEntityKind.Item, [prototype, standard]);

        Assert.Equal("found", result.Status);
        Assert.Equal(prototype.DefinitionName, result.DefinitionName);
    }

    [Fact]
    public void UsesSelectedLanguageTierAliasesAndSharedEnglishAliases()
    {
        SparkyEntityTierRule[] tierRules = Rules.TierRules
            .Select(rule => rule.CanonicalLabel == "T2+"
                ? rule with
                {
                    AliasesByLanguage = new Dictionary<int, IReadOnlyList<string>>
                    {
                        [0] = ["T2+", "tier 2 plus"],
                        [2] = ["level two plus"]
                    }
                }
                : rule)
            .ToArray();
        SparkyEntityResolutionRules localizedRules = Rules with { TierRules = tierRules };
        localizedRules.Validate();
        SparkyEntityCandidate scorcher = Candidate("def_scorcher_t2plus", SparkyEntityKind.Item, 3, 2, "Scorcher");

        SparkyEntityResolutionResult localized = SparkyTierAwareEntityResolver.Resolve(
            "level two plus Scorcher", null, SparkyEntityKind.Item, [scorcher], localizedRules, languageId: 2);
        SparkyEntityResolutionResult sharedEnglish = SparkyTierAwareEntityResolver.Resolve(
            "tier 2 plus Scorcher", null, SparkyEntityKind.Item, [scorcher], localizedRules, languageId: 2);

        Assert.Equal("found", localized.Status);
        Assert.Equal(scorcher.DefinitionName, localized.DefinitionName);
        Assert.Equal("found", sharedEnglish.Status);
        Assert.Equal(scorcher.DefinitionName, sharedEnglish.DefinitionName);
    }

    [Theory]
    [InlineData("Mk2 Arbalest")]
    [InlineData("Mark 2 Arbalest")]
    [InlineData("mark, 2 ARBALEST!")]
    public void ResolvesRobotMarkAliasesAndPunctuation(string query)
    {
        SparkyEntityCandidate mk2 = Candidate("def_arbalest_mk2", SparkyEntityKind.Robot, 1, 2, "Arbalest");
        SparkyEntityCandidate mk1 = Candidate("def_arbalest_mk1", SparkyEntityKind.Robot, 1, 1, "Arbalest");

        SparkyEntityResolutionResult result = Resolve(query, SparkyEntityKind.Robot, [mk2, mk1]);

        Assert.Equal("found", result.Status);
        Assert.Equal(mk2.DefinitionName, result.DefinitionName);
    }

    [Fact]
    public void RobotPrototypePMatchesNullLevelOnly()
    {
        SparkyEntityCandidate prototype = Candidate("def_weasel_p", SparkyEntityKind.Robot, 2, null, "Weasel");
        SparkyEntityCandidate typeTwoLevelZero = Candidate("def_weasel_type2_level0", SparkyEntityKind.Robot, 2, 0, "Weasel");
        SparkyEntityCandidate mk2 = Candidate("def_weasel_mk2", SparkyEntityKind.Robot, 1, 2, "Weasel");

        SparkyEntityResolutionResult result = Resolve("prototype Weasel", SparkyEntityKind.Robot,
            [prototype, typeTwoLevelZero, mk2]);

        Assert.Equal("found", result.Status);
        Assert.Equal(prototype.DefinitionName, result.DefinitionName);
    }

    [Fact]
    public void UnqualifiedNameDoesNotTreatNullMetadataAsATierFilter()
    {
        SparkyEntityCandidate prototype = Candidate("def_weasel_p", SparkyEntityKind.Robot, 2, null, "Weasel");

        SparkyEntityResolutionResult result = Resolve("Weasel", SparkyEntityKind.Robot, [prototype]);

        Assert.Equal("found", result.Status);
        Assert.Equal(prototype.DefinitionName, result.DefinitionName);
    }

    [Fact]
    public void ConflictingTierDescriptorsReturnConflictWithoutSelecting()
    {
        SparkyEntityCandidate t1 = Candidate("def_plate_t1", SparkyEntityKind.Item, 1, 1, "Medium armor plate");

        SparkyEntityResolutionResult result = Resolve("T1 T2 medium plate", SparkyEntityKind.Item, [t1]);

        Assert.Equal("conflicting_tiers", result.Status);
        Assert.Null(result.DefinitionName);
    }

    [Fact]
    public void TierOnlyQuestionNeverSelectsEvenWhenOneCandidateRemains()
    {
        SparkyEntityCandidate t2 = Candidate("def_only_t2", SparkyEntityKind.Item, 1, 2, "Scorcher");

        SparkyEntityResolutionResult result = Resolve("T2", SparkyEntityKind.Item, [t2]);

        Assert.Equal("ambiguous", result.Status);
        Assert.Null(result.DefinitionName);
        Assert.Equal([t2.DefinitionName], result.CandidateDefinitionNames);
    }

    [Fact]
    public void MatchesShortenedWholeNameTokensAndIntentWording()
    {
        SparkyEntityCandidate plate = Candidate("def_standard_medium_armor_plate", SparkyEntityKind.Item, 1, 1,
            "Standard medium armor plate");

        SparkyEntityResolutionResult result = Resolve(
            "What are the stats for T1 medium plate?", SparkyEntityKind.Item, [plate]);

        Assert.Equal("found", result.Status);
        Assert.Equal(plate.DefinitionName, result.DefinitionName);
    }

    [Fact]
    public void ExactPublicAliasBeatsShortenedAlias()
    {
        SparkyEntityCandidate exact = Candidate("def_medium_plate", SparkyEntityKind.Item, 1, 1, "Medium plate");
        SparkyEntityCandidate longer = Candidate("def_medium_armor_plate", SparkyEntityKind.Item, 1, 1,
            "Standard medium armor plate");

        SparkyEntityResolutionResult result = Resolve("medium plate", SparkyEntityKind.Item, [longer, exact]);

        Assert.Equal("found", result.Status);
        Assert.Equal(exact.DefinitionName, result.DefinitionName);
    }

    [Fact]
    public void ShortenedTieAndNameOnlyTieRemainAmbiguous()
    {
        SparkyEntityCandidate first = Candidate("def_standard_medium_armor_plate", SparkyEntityKind.Item, 1, 1,
            "Standard medium armor plate");
        SparkyEntityCandidate second = Candidate("def_prototype_medium_plate", SparkyEntityKind.Item, 2, 1,
            "Prototype medium armor plate");

        SparkyEntityResolutionResult shortened = Resolve("medium plate", SparkyEntityKind.Item, [first, second]);
        SparkyEntityResolutionResult nameOnly = Resolve("plate", SparkyEntityKind.Item, [first, second]);

        Assert.Equal("ambiguous", shortened.Status);
        Assert.Null(shortened.DefinitionName);
        Assert.Equal(2, shortened.CandidateDefinitionNames.Count);
        Assert.Equal("ambiguous", nameOnly.Status);
    }

    [Fact]
    public void OriginalAndEnglishQueryBothContributePublicNameTokens()
    {
        SparkyEntityCandidate plate = Candidate("def_medium_armor_plate", SparkyEntityKind.Item, 1, 1,
            "Standard medium armor plate", "Standard medium armor plate localized");

        SparkyEntityResolutionResult result = Resolve(
            "T1 localized medium plate", SparkyEntityKind.Item, [plate], englishQuery: "standard medium armor plate");

        Assert.Equal("found", result.Status);
        Assert.Equal(plate.DefinitionName, result.DefinitionName);
    }

    [Theory]
    [InlineData("CT medium plate")]
    [InlineData("calibration template medium plate")]
    [InlineData("blueprint medium plate")]
    public void CalibrationCategorySelectsOnlyExactCprgCandidates(string query)
    {
        SparkyEntityCandidate calibration = Candidate("def_medium_plate_cprg", SparkyEntityKind.Item, 1, 1,
            "Medium plate calibration template");
        SparkyEntityCandidate ordinary = Candidate("def_medium_plate", SparkyEntityKind.Item, 1, 1,
            "Medium plate");

        SparkyEntityResolutionResult result = Resolve(query, SparkyEntityKind.Item, [ordinary, calibration]);

        Assert.Equal("found", result.Status);
        Assert.Equal(calibration.DefinitionName, result.DefinitionName);
    }

    [Fact]
    public void PartialCprgSuffixDoesNotQualifyCalibrationCandidate()
    {
        SparkyEntityCandidate partial = Candidate("def_medium_plate_cprg_extra", SparkyEntityKind.Item, 1, 1,
            "Medium plate", calibrationTemplate: false);

        SparkyEntityResolutionResult result = Resolve("CT medium plate", SparkyEntityKind.Item, [partial]);

        Assert.Equal("not_found", result.Status);
        Assert.Empty(result.CandidateDefinitionNames);
    }

    [Fact]
    public void CategoryWordInDisplayNameDoesNotQualifyOrdinaryEntity()
    {
        SparkyEntityCandidate ordinary = Candidate("def_blueprint_medium_plate", SparkyEntityKind.Item, 1, 1,
            "Blueprint medium plate", calibrationTemplate: false);

        SparkyEntityResolutionResult result = Resolve("blueprint medium plate", SparkyEntityKind.Item, [ordinary]);

        Assert.Equal("not_found", result.Status);
    }

    [Fact]
    public void DoesNotMatchInternalDefinitionNameOrANameWithNoPublicAlias()
    {
        SparkyEntityCandidate candidate = Candidate("def_secret_scorcher_t2plus", SparkyEntityKind.Item, 3, 2);

        SparkyEntityResolutionResult result = Resolve("T2+ secret scorcher", SparkyEntityKind.Item, [candidate]);

        Assert.Equal("not_found", result.Status);
    }

    [Fact]
    public void UnmappedNumericTierPairCannotMatchAReviewedShorthand()
    {
        SparkyEntityCandidate unknown = Candidate("def_unknown_tier_scorcher", SparkyEntityKind.Item, 99, 99, "Scorcher");

        SparkyEntityResolutionResult result = Resolve("T2+ Scorcher", SparkyEntityKind.Item, [unknown]);

        Assert.Equal("not_found", result.Status);
        Assert.Null(result.DefinitionName);
    }

    [Theory]
    [InlineData("T9 Scorcher")]
    [InlineData("tier 9 Scorcher")]
    [InlineData("T5 prototype sensor amplifier")]
    public void UnsupportedTierSyntaxCannotFallBackToAnotherKnownTier(string query)
    {
        SparkyEntityCandidate common = Candidate("def_sensor_common_t5", SparkyEntityKind.Item, 1, 5, "Sensor amplifier");
        SparkyEntityCandidate prototype = Candidate("def_sensor_prototype_t4", SparkyEntityKind.Item, 2, 4, "Sensor amplifier");

        SparkyEntityResolutionResult result = Resolve(query, SparkyEntityKind.Item,
            [common with { PublicAliases = ["Scorcher", "Sensor amplifier"] }, prototype]);

        Assert.Equal("not_found", result.Status);
        Assert.Null(result.DefinitionName);
    }

    private static SparkyEntityResolutionResult Resolve(
        string question,
        SparkyEntityKind kind,
        IReadOnlyList<SparkyEntityCandidate> candidates,
        string? englishQuery = null,
        int languageId = 0) =>
        SparkyTierAwareEntityResolver.Resolve(question, englishQuery, kind, candidates, Rules, languageId);

    private static SparkyEntityCandidate Candidate(
        string definitionName,
        SparkyEntityKind kind,
        int? tierType,
        int? tierLevel,
        params string[] publicAliases) => new()
    {
        DefinitionName = definitionName,
        EntityKind = kind,
        TierMetadata = new SparkyEntityTierMetadata { TierType = tierType, TierLevel = tierLevel },
        PublicAliases = publicAliases,
        IsCalibrationTemplate = definitionName.EndsWith("_cprg", StringComparison.OrdinalIgnoreCase)
    };

    private static SparkyEntityCandidate Candidate(
        string definitionName,
        SparkyEntityKind kind,
        int? tierType,
        int? tierLevel,
        string publicAlias,
        bool calibrationTemplate) => new()
    {
        DefinitionName = definitionName,
        EntityKind = kind,
        TierMetadata = new SparkyEntityTierMetadata { TierType = tierType, TierLevel = tierLevel },
        PublicAliases = [publicAlias],
        IsCalibrationTemplate = calibrationTemplate
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
