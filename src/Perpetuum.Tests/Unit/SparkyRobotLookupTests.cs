using System.Text.Json;
using Perpetuum.ExportedTypes;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyRobotLookupTests
{
    private static readonly SparkyEntityResolutionRules TierRules = SparkyEntityResolutionRules.Load(TestRepositoryRoot);

    [Theory]
    [InlineData("What are stats for Sample robot?", "stats for Sample robot", "Stats")]
    [InlineData("What bonuses does Sample robot have?", "Sample robot bonuses", "ChassisBonus")]
    [InlineData("Would Standard Heavy Armor Plate fit on Sample robot?", "would Standard Heavy Armor Plate fit on Sample robot", "ModuleCompatibility")]
    public void StructuredItemClassificationIsCorrectedForExplicitRobotQuestions(
        string question,
        string englishSearchQuery,
        string expectedFocus)
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.StructuredItem,
            englishSearchQuery,
            MentionedEntities: expectedFocus == "ModuleCompatibility"
                ? ["Standard Heavy Armor Plate", "Sample robot"]
                : ["Sample robot"]);

        SparkyQuestionInterpretation corrected = InvokeRobotIntentCorrection(question, interpretation, bundle);

        Assert.Equal(SparkyIntent.StructuredRobot, corrected.Intent);
        Assert.Equal(expectedFocus, corrected.RobotFocus?.ToString());
    }

    [Fact]
    public void BroadFastestRobotQuestionUsesStructuredRobotRoute()
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.MechanicsLore,
            "which robot is fastest",
            MentionedEntities: ["robot"]);

        SparkyQuestionInterpretation corrected = SparkyRobotIntentResolver.CorrectStructuredItemQuestion(
            "What is the fastest robot?",
            interpretation,
            bundle);

        Assert.Equal("StructuredRobot", corrected.Intent.ToString());
        Assert.Equal("Fastest", corrected.RobotFocus?.ToString());
    }

    [Fact]
    public void ContextualComparisonIsNotConvertedToGlobalFastestRanking()
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.StructuredRobot,
            "which recent robot has higher maximum speed",
            RobotFocus: SparkyRobotQuestionFocus.Fastest,
            Scope: SparkyConversationScope.Game,
            Comparison: new SparkyComparisonIntent(
                true, true, [], "maximum speed", SparkyComparisonDirection.Higher, false));

        SparkyQuestionInterpretation corrected = SparkyRobotIntentResolver.CorrectStructuredItemQuestion(
            "Which one is faster?", interpretation, bundle);

        Assert.Equal(SparkyIntent.StructuredRobot, corrected.Intent);
        Assert.Equal(SparkyRobotQuestionFocus.CompareStats, corrected.RobotFocus);
    }

    [Fact]
    public void MultiRobotStatComparisonUsesDedicatedStructuredRoute()
    {
        SparkyKnowledgeBundle bundle = CreateBundle() with
        {
            Robots =
            [
                ..CreateBundle().Robots,
                CreateBundle().Robots[0] with
                {
                    DefinitionName = "def_other_robot",
                    LocalizedViews = new Dictionary<int, SparkyLocalizedRobotView>
                    {
                        [0] = new("Other robot", [new AnswerStat { FieldId = 2, Label = "Mass", Value = "150", Unit = "kg" }], [], []),
                        [2] = new("Anderer Roboter", [new AnswerStat { FieldId = 2, Label = "Masse", Value = "150", Unit = "kg" }], [], []),
                        [3] = new("Outro robô", [new AnswerStat { FieldId = 2, Label = "Massa", Value = "150", Unit = "kg" }], [], []),
                        [4] = new("Другой робот", [new AnswerStat { FieldId = 2, Label = "Масса", Value = "150", Unit = "кг" }], [], [])
                    }
                }
            ]
        };
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.StructuredItem,
            "compare stats for Sample robot and Other robot",
            MentionedEntities: ["Sample robot", "Other robot"]);

        SparkyQuestionInterpretation corrected = SparkyRobotIntentResolver.CorrectStructuredItemQuestion(
            "Compare stats for Sample robot and Other robot",
            interpretation,
            bundle);

        Assert.Equal("StructuredRobot", corrected.Intent.ToString());
        Assert.Equal("CompareStats", corrected.RobotFocus?.ToString());
    }

    [Theory]
    [InlineData("What laser modules do you know?", "what laser modules do you know")]
    [InlineData("List all combat lasers", "list all combat lasers")]
    [InlineData("What lasers do you know?", "what lasers do you know")]
    public void LaserCatalogQuestionUsesStructuredModuleCatalogRoute(string question, string englishSearchQuery)
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.MechanicsLore,
            englishSearchQuery,
            MentionedEntities: []);

        SparkyQuestionInterpretation corrected = SparkyRobotIntentResolver.CorrectStructuredItemQuestion(
            question,
            interpretation,
            bundle);

        Assert.Equal("StructuredModuleCatalog", corrected.Intent.ToString());
        Assert.Equal("Lasers", corrected.GetType().GetProperty("CatalogFocus")?.GetValue(corrected)?.ToString());
    }

    [Fact]
    public void LaserModuleDefinitionQuestionIsNotRoutedAsCatalogRequest()
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.MechanicsLore,
            "what is a laser module",
            MentionedEntities: []);

        SparkyQuestionInterpretation corrected = SparkyRobotIntentResolver.CorrectStructuredItemQuestion(
            "What is a laser module?",
            interpretation,
            bundle);

        Assert.Equal(SparkyIntent.MechanicsLore, corrected.Intent);
        Assert.Null(corrected.CatalogFocus);
    }

    [Theory]
    [InlineData("Which ammo does Standard small laser use?", "which ammo does standard small laser use")]
    [InlineData("Do you know the stats of Standard small laser?", "do you know the stats of standard small laser")]
    public void LaserQuestionWithSpecificNonCatalogIntentIsNotReclassified(
        string question,
        string englishSearchQuery)
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.MechanicsLore,
            englishSearchQuery,
            MentionedEntities: []);

        SparkyQuestionInterpretation corrected = SparkyRobotIntentResolver.CorrectStructuredItemQuestion(
            question,
            interpretation,
            bundle);

        Assert.Equal(SparkyIntent.MechanicsLore, corrected.Intent);
        Assert.Null(corrected.CatalogFocus);
    }

    [Fact]
    public void NamedRobotMaximumSpeedQuestionUsesThatRobotsStatsRoute()
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.MechanicsLore,
            "what is the maximum speed of Sample robot",
            MentionedEntities: ["Sample robot"]);

        SparkyQuestionInterpretation corrected = SparkyRobotIntentResolver.CorrectStructuredItemQuestion(
            "What is the maximum speed of Sample robot?",
            interpretation,
            bundle);

        Assert.Equal(SparkyIntent.StructuredRobot, corrected.Intent);
        Assert.Equal(SparkyRobotQuestionFocus.Stats, corrected.RobotFocus);
    }

    [Fact]
    public void NamedRobotTopSpeedComparisonUsesComparisonRoute()
    {
        SparkyKnowledgeBundle bundle = CreateBundleWithOtherRobot();
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.MechanicsLore,
            "compare the top speed of Sample robot and Other robot",
            MentionedEntities: ["Sample robot", "Other robot"]);

        SparkyQuestionInterpretation corrected = SparkyRobotIntentResolver.CorrectStructuredItemQuestion(
            "Compare the top speed of Sample robot and Other robot",
            interpretation,
            bundle);

        Assert.Equal(SparkyIntent.StructuredRobot, corrected.Intent);
        Assert.Equal(SparkyRobotQuestionFocus.CompareStats, corrected.RobotFocus);
    }

    [Theory]
    [InlineData("Compare the recipes for Sample robot and Other robot", "StructuredItem", "")]
    [InlineData("Compare the chassis bonuses of Sample robot and Other robot", "StructuredRobot", "ChassisBonus")]
    public void RobotComparisonPreservesNonStatIntent(
        string question,
        string originalIntentName,
        string originalFocusName)
    {
        SparkyIntent originalIntent = Enum.Parse<SparkyIntent>(originalIntentName);
        SparkyRobotQuestionFocus? originalFocus = string.IsNullOrEmpty(originalFocusName)
            ? null
            : Enum.Parse<SparkyRobotQuestionFocus>(originalFocusName);
        SparkyKnowledgeBundle bundle = CreateBundleWithOtherRobot();
        var interpretation = new SparkyQuestionInterpretation(
            0,
            originalIntent,
            question,
            MentionedEntities: ["Sample robot", "Other robot"],
            RobotFocus: originalFocus);

        SparkyQuestionInterpretation corrected = SparkyRobotIntentResolver.CorrectStructuredItemQuestion(
            question,
            interpretation,
            bundle);

        Assert.Equal(originalIntent, corrected.Intent);
        Assert.Equal(originalFocus, corrected.RobotFocus);
    }

    [Fact]
    public void FastestRobotIsRankedFromAssembledSpeedStat()
    {
        SparkyKnowledgeBundle baseBundle = CreateBundle();
        SparkyRobotRecord slow = WithSpeed(baseBundle.Robots[0], "Sample robot", "25");
        SparkyRobotRecord fast = WithSpeed(baseBundle.Robots[0] with { DefinitionName = "def_fast_robot" }, "Quick robot", "40");
        SparkyKnowledgeBundle bundle = baseBundle with { Robots = [slow, fast] };

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "What is the fastest robot?",
            "fastest robot",
            bundle,
            0,
            null,
            SparkyRobotQuestionFocus.Fastest);

        Assert.Equal("found", result.Status);
        Assert.Equal("Quick robot", Assert.Single(result.Facts!.SpeedRanking!).RobotName);
    }

    [Fact]
    public void FastestRobotRankingDoesNotSubstituteSlowerLocalizedRobotWhenWinnerNameIsMissing()
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        SparkyRobotRecord fastest = WithSpeed(bundle.Robots.Single(), "Fast robot", "50");
        fastest = fastest with
        {
            LocalizedViews = fastest.LocalizedViews.ToDictionary(
                entry => entry.Key,
                entry => entry.Key == 4 ? entry.Value with { DisplayName = null } : entry.Value)
        };
        SparkyRobotRecord slower = WithSpeed(
            bundle.Robots.Single() with { DefinitionName = "def_slower_robot" },
            "Slower robot",
            "25");
        bundle = bundle with { Robots = [fastest, slower] };

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "What is the fastest robot?",
            "which robot is fastest",
            bundle,
            4,
            null,
            SparkyRobotQuestionFocus.Fastest);

        Assert.Equal("not_found", result.Status);
        Assert.Null(result.Facts);
    }

    [Fact]
    public void RobotComparisonUsesFieldIdsAndComputesSecondMinusFirst()
    {
        SparkyKnowledgeBundle baseBundle = CreateBundle();
        SparkyRobotRecord first = WithSpeed(baseBundle.Robots[0], "Sample robot", "25");
        SparkyRobotRecord second = WithSpeed(baseBundle.Robots[0] with { DefinitionName = "def_other_robot" }, "Other robot", "40", "150");
        SparkyKnowledgeBundle bundle = baseBundle with { Robots = [first, second] };

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "Compare stats for Sample robot and Other robot",
            "compare stats for Sample robot and Other robot",
            bundle,
            0,
            null,
            SparkyRobotQuestionFocus.CompareStats);

        SparkyRobotStatComparison comparison = Assert.Single(
            Assert.Single(result.Facts!.Comparisons!).Stats,
            stat => stat.Label == "Mass");
        Assert.Equal("Mass", comparison.Label);
        Assert.Equal("250", comparison.FirstValue);
        Assert.Equal("150", comparison.SecondValue);
        Assert.Equal("-100", comparison.Difference);
    }

    [Fact]
    public void LaserCatalogUsesOnlyModulesExportedWithLaserCategory()
    {
        SparkyKnowledgeBundle bundle = CreateBundle() with
        {
            RobotModules =
            [
                CreateModule("def_laser_alpha", "Alpha laser", false, "Alpha-Laser", "Laser Alpha", "Лазер Альфа") with { IsLaser = true },
                CreateModule("def_other_weapon", "Other weapon", false, "Andere Waffe", "Outra arma", "Другое оружие")
            ]
        };

        string answer = SparkyRobotAnswerFormatter.FormatLaserCatalog(bundle, 0, 2000)!;

        Assert.Contains("Alpha laser", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Other weapon", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RouterAnswersLaserCatalogFromVerifiedModuleCategory()
    {
        SparkyKnowledgeBundle bundle = CreateBundle() with
        {
            RobotModules =
            [
                CreateModule("def_laser_alpha", "Alpha laser", false, "Alpha-Laser", "Laser Alpha", "Лазер Альфа") with { IsLaser = true },
                CreateModule("def_other_weapon", "Other weapon", false, "Andere Waffe", "Outra arma", "Другое оружие")
            ]
        };
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.StructuredModuleCatalog,
            "list laser modules",
            MentionedEntities: [],
            CatalogFocus: SparkyModuleCatalogFocus.Lasers,
            Scope: SparkyConversationScope.Game);

        SparkyAnswerResult result = await new SparkyAnswerRouter(CreateOptions()).AnswerAsync(
            "What laser modules do you know?",
            interpretation,
            bundle,
            null,
            null,
            CancellationToken.None);

        Assert.Equal("answered", result.Status);
        Assert.Contains("Alpha laser", result.Answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Other weapon", result.Answer, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Welche Werte hat Beispielroboter?", "what are the stats for Sample robot", 2, "Beispielroboter", "Stats")]
    [InlineData("Какие бонусы у Тестовый робот?", "what bonuses does Sample robot have", 4, "Тестовый робот", "ChassisBonus")]
    public void LocalizedRobotQuestionsAreCorrectedUsingPlayerAndEnglishText(
        string question,
        string englishSearchQuery,
        int languageId,
        string localizedRobotName,
        string expectedFocus)
    {
        SparkyKnowledgeBundle bundle = CreateBundle(germanRobotName: "Beispielroboter");
        var interpretation = new SparkyQuestionInterpretation(
            languageId,
            SparkyIntent.StructuredItem,
            englishSearchQuery,
            MentionedEntities: [localizedRobotName]);

        SparkyQuestionInterpretation corrected = SparkyRobotIntentResolver.CorrectStructuredItemQuestion(
            question,
            interpretation,
            bundle);

        Assert.Equal(SparkyIntent.StructuredRobot, corrected.Intent);
        Assert.Equal(expectedFocus, corrected.RobotFocus?.ToString());
    }

    [Fact]
    public void RecipeQuestionForRobotNameRemainsOnItemRoute()
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.StructuredItem,
            "full recipe for Sample robot",
            MentionedEntities: ["Sample robot"]);

        SparkyQuestionInterpretation corrected = InvokeRobotIntentCorrection(
            "What is the full recipe for Sample robot?",
            interpretation,
            bundle);

        Assert.Equal(SparkyIntent.StructuredItem, corrected.Intent);
        Assert.Null(corrected.RobotFocus);
    }

    [Fact]
    public async Task RouterFallsBackToExactCatalogCandidatesWhenModelUnavailable()
    {
        SparkyKnowledgeBundle bundle = CreateBundle() with
        {
            RobotModules =
            [
                CreateModule("def_standard_large_armor_plate", "Standard large armor plate", false, "Standard große Panzerplatte", "Placa de blindagem grande padrão", "Стандартная большая бронеплита"),
                CreateModule("def_standard_medium_armor_plate", "Standard medium armor plate", false, "Standard mittlere Panzerplatte", "Placa de blindagem média padrão", "Стандартная средняя бронеплита"),
                CreateModule("def_standard_light_armor_plate", "Standard light armor plate", false, "Standard leichte Panzerplatte", "Placa de blindagem leve padrão", "Стандартная лёгкая бронеплита"),
                CreateModule("def_standard_large_armor_plate_prototype", "Standard large armor plate prototype", false, "Standard große Panzerplatte Prototyp", "Protótipo de placa de blindagem grande padrão", "Прототип стандартной большой бронеплиты"),
                CreateModule("def_halc_heavy_armor_plate", "Halc heavy armor plate", false, "Halc schwere Panzerplatte", "Placa de blindagem pesada Halc", "Тяжёлая бронеплита Halc"),
                CreateModule("def_invigor_heavy_armor_plate", "Invigor III heavy armor plate", false, "Invigor III schwere Panzerplatte", "Placa de blindagem pesada Invigor III", "Тяжёлая бронеплита Invigor III")
            ]
        };
        var router = new SparkyAnswerRouter(CreateOptions());
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.StructuredItem,
            "would Standard Heavy Armor Plate fit on Sample robot",
            MentionedEntities: ["Standard Heavy Armor Plate", "Sample robot"],
            Scope: SparkyConversationScope.Game);

        SparkyAnswerResult result = await router.AnswerAsync(
            "Would Standard Heavy Armor Plate fit on Sample robot?",
            interpretation,
            bundle,
            null,
            null,
            CancellationToken.None);

        Assert.Equal("not_found", result.Status);
        Assert.Equal("fallback", result.FinishStatus);
        Assert.Contains("I can't match that module name", result.Answer, StringComparison.Ordinal);
        Assert.Contains("Standard large armor plate", result.Answer, StringComparison.Ordinal);
        Assert.Contains("Standard medium armor plate", result.Answer, StringComparison.Ordinal);
        Assert.Contains("Standard light armor plate", result.Answer, StringComparison.Ordinal);
        Assert.Equal(
            new[] { "Standard large armor plate", "Standard light armor plate", "Standard medium armor plate" },
            result.Candidates);
    }

    [Fact]
    public async Task ExplicitItemStatsMissDoesNotFallThroughToLoreRetrieval()
    {
        SparkyKnowledgeBundle bundle = CreateBundle() with
        {
            KnowledgePassages =
            [
                new SparkyKnowledgePassage(
                    "unrelated-laser-lore",
                    "lore",
                    "test",
                    new string('A', 64),
                    "A field memo mentions a laser, its damage, and its statistics.")
            ],
            SearchIndex = new SparkySearchIndex(
                new Dictionary<string, IReadOnlyList<int>>
                {
                    ["laser"] = [0],
                    ["statistics"] = [0]
                },
                [])
        };
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.StructuredItem,
            "what are the stats for T4 medium LCL laser",
            MentionedEntities: ["T4 medium LCL laser"],
            Scope: SparkyConversationScope.Game);

        SparkyAnswerResult result = await new SparkyAnswerRouter(CreateOptions()).AnswerAsync(
            "What are the stats for T4 medium LCL laser?",
            interpretation,
            bundle,
            null,
            null,
            CancellationToken.None);

        Assert.Equal("not_found", result.Status);
        Assert.Empty(result.SelectedSourceIds ?? []);
    }

    [Fact]
    public void ClarificationPromptTreatsPlayerTermsAsUnverifiedAndForbidsSubjectClaims()
    {
        string guidance = LocalModelAnswerer.BuildClarificationGuidance();

        Assert.Contains("RequestedTerm and MissingEntities came from player wording and are not verified facts", guidance, StringComparison.Ordinal);
        Assert.Contains("Do not describe the subject or infer why it is missing", guidance, StringComparison.Ordinal);
        Assert.Contains("ask one useful clarification", guidance, StringComparison.Ordinal);
        Assert.Contains("provide its English name while keeping your reply in their language", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownModuleCompatibilityReturnsExactLocalizedCatalogCandidates()
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        bundle = bundle with
        {
            RobotModules =
            [
                CreateModule("def_standard_large_armor_plate", "Standard large armor plate", false, "Standard große Panzerplatte", "Placa de blindagem grande padrão", "Стандартная большая бронеплита"),
                CreateModule("def_standard_medium_armor_plate", "Standard medium armor plate", false, "Standard mittlere Panzerplatte", "Placa de blindagem média padrão", "Стандартная средняя бронеплита"),
                CreateModule("def_standard_light_armor_plate", "Standard light armor plate", false, "Standard leichte Panzerplatte", "Placa de blindagem leve padrão", "Стандартная лёгкая бронеплита")
            ]
        };

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "Would Standard Heavy Armor Plate fit on Sample robot?",
            "would Standard Heavy Armor Plate fit on Sample robot",
            bundle,
            0,
            null,
            SparkyRobotQuestionFocus.ModuleCompatibility);

        Assert.Equal("module_not_found", result.Status);
        Assert.Equal(
            new[] { "Standard large armor plate", "Standard light armor plate", "Standard medium armor plate" },
            result.Candidates);
    }

    [Fact]
    public void StatsFocusReturnsOnlyLocalizedAssembledStats()
    {
        SparkyKnowledgeBundle bundle = CreateBundle();

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "What are the stats for Sample robot?",
            "Sample robot stats",
            bundle,
            languageId: 0,
            contextItem: null,
            SparkyRobotQuestionFocus.Stats);

        Assert.Equal("found", result.Status);
        Assert.NotNull(result.Facts);
        Assert.Equal("Sample robot", result.Facts.RobotName);
        Assert.Single(result.Facts.AssembledStats);
        Assert.Empty(result.Facts.Components);
        Assert.Empty(result.Facts.ChassisBonuses);
        Assert.Null(result.Facts.Module);
        string promptFacts = JsonSerializer.Serialize(result.Facts);
        Assert.DoesNotContain("def_", promptFacts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FieldId", promptFacts, StringComparison.Ordinal);
    }

    [Fact]
    public void ComponentFocusReturnsAssembledAndLocalizedPartStats()
    {
        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "Show the components of Sample robot",
            "Sample robot component breakdown",
            CreateBundle(),
            0,
            null,
            SparkyRobotQuestionFocus.Components);

        Assert.Equal("found", result.Status);
        Assert.NotNull(result.Facts);
        Assert.Single(result.Facts.AssembledStats);
        Assert.Single(result.Facts.Components);
        Assert.Equal("Robot head", result.Facts.Components[0].DisplayName);
        Assert.Single(result.Facts.Components[0].Stats);
        Assert.Equal(2, result.Facts.Components[0].SlotCount);
    }

    [Fact]
    public void MissingComponentTranslationIsOmittedFromLocalizedFacts()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(germanRobotName: "Beispielroboter");
        SparkyRobotRecord robot = bundle.Robots.Single();
        var localizedViews = robot.LocalizedViews.ToDictionary(entry => entry.Key, entry => entry.Value);
        localizedViews[2] = localizedViews[2] with { Components = [] };
        bundle = bundle with { Robots = [robot with { LocalizedViews = localizedViews }] };

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "Welche Komponenten hat Beispielroboter?",
            "What components does Sample robot have?",
            bundle,
            2,
            null,
            SparkyRobotQuestionFocus.Components);

        Assert.Equal("found", result.Status);
        Assert.Empty(result.Facts!.Components);
        string promptFacts = JsonSerializer.Serialize(result.Facts);
        Assert.DoesNotContain("def_sample_robot_head", promptFacts, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownRobotDoesNotResolveToAnyBundleFacts()
    {
        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "Who is Unknown machine?",
            "Unknown machine",
            CreateBundle(),
            0,
            null,
            SparkyRobotQuestionFocus.General);

        Assert.Equal("not_found", result.Status);
        Assert.Null(result.Facts);
    }

    [Fact]
    public void Mk2RobotAliasSelectsTheExactTierAndMentionHelpersAgree()
    {
        SparkyKnowledgeBundle source = CreateBundle();
        SparkyRobotRecord template = source.Robots.Single();
        SparkyKnowledgeBundle bundle = CreateTierAwareBundle(
            RobotVariant(template, "def_arbalest_mk1", "Arbalest", 1, 1),
            RobotVariant(template, "def_arbalest_mk2", "Arbalest", 1, 2));

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "What are the stats for Mk2 Arbalest?",
            "stats for Mark 2 Arbalest",
            bundle,
            0,
            null,
            SparkyRobotQuestionFocus.Stats);

        Assert.Equal("found", result.Status);
        Assert.Equal("Arbalest", result.Facts!.RobotName);
        Assert.True(SparkyRobotLookup.ContainsRobotMention("Mk2 Arbalest", null, bundle, 0));
        Assert.Equal("def_arbalest_mk2", Assert.Single(
            SparkyRobotLookup.FindMentionedRobots("Mk2 Arbalest", null, bundle, 0)).DefinitionName);
    }

    [Fact]
    public void Mk3WeaselAndUnqualifiedUniqueWeaselResolve()
    {
        SparkyRobotRecord template = CreateBundle().Robots.Single();
        SparkyKnowledgeBundle bundle = CreateTierAwareBundle(
            RobotVariant(template, "def_weasel_mk3", "Weasel", 1, 3));

        SparkyRobotLookupResult tiered = SparkyRobotLookup.Resolve(
            "Tell me about Mk3 Weasel", "Mk3 Weasel", bundle, 0, null, SparkyRobotQuestionFocus.General);
        SparkyRobotLookupResult unqualified = SparkyRobotLookup.Resolve(
            "Tell me about Weasel", "Weasel", bundle, 0, null, SparkyRobotQuestionFocus.General);

        Assert.Equal("found", tiered.Status);
        Assert.Equal("Weasel", tiered.Facts!.RobotName);
        Assert.Equal("found", unqualified.Status);
        Assert.Equal("Weasel", unqualified.Facts!.RobotName);
    }

    [Fact]
    public void ShortenedTieredRobotNameMatchesPublicNameTokens()
    {
        SparkyRobotRecord template = CreateBundle().Robots.Single();
        SparkyKnowledgeBundle bundle = CreateTierAwareBundle(
            RobotVariant(template, "def_arbalest_mk2", "Standard Medium Arbalest", 1, 2));

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "What are the stats for Mk2 medium Arbalest?", "Mk2 medium Arbalest stats", bundle, 0, null,
            SparkyRobotQuestionFocus.Stats);

        Assert.Equal("found", result.Status);
        Assert.Equal("Standard Medium Arbalest", result.Facts!.RobotName);
    }

    [Fact]
    public void PrototypeAliasMatchesRobotTypeTwoWithNullTierLevel()
    {
        SparkyRobotRecord template = CreateBundle().Robots.Single();
        SparkyKnowledgeBundle bundle = CreateTierAwareBundle(
            RobotVariant(template, "def_weasel_prototype", "Weasel", 2, null),
            RobotVariant(template, "def_weasel_mk2", "Weasel", 1, 2));

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "What are the stats for P Weasel?", "prototype Weasel stats", bundle, 0, null,
            SparkyRobotQuestionFocus.Stats);

        Assert.Equal("found", result.Status);
        Assert.Equal("Weasel", result.Facts!.RobotName);
    }

    [Fact]
    public void TieredModuleCompatibilityUsesItemTierRulesAlongsideRobotTierRules()
    {
        SparkyKnowledgeBundle source = CreateBundle();
        SparkyRobotRecord robot = RobotVariant(source.Robots.Single(), "def_arbalest_mk2", "Arbalest", 1, 2);
        SparkyRobotModuleRecord t4 = CreateModule("def_sensor_amplifier_t4", "Sensor amplifier", false,
            "Sensor amplifier", "Amplificador de sensor", "Усилитель сенсора") with
        {
            TierMetadata = Tier(1, 4)
        };
        SparkyRobotModuleRecord t4Prototype = CreateModule("def_sensor_amplifier_t4p", "Sensor amplifier", false,
            "Sensor amplifier", "Amplificador de sensor", "Усилитель сенсора") with
        {
            TierMetadata = Tier(2, 4)
        };
        SparkyKnowledgeBundle bundle = CreateTierAwareBundle([robot], [t4, t4Prototype]);

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "Would T4 prototype sensor amplifier fit Mk2 Arbalest?",
            "Would T4 prototype sensor amplifier fit Mark 2 Arbalest?",
            bundle,
            0,
            null,
            SparkyRobotQuestionFocus.ModuleCompatibility);

        Assert.Equal("found", result.Status);
        Assert.NotNull(result.Facts!.Module);
        Assert.Equal("Sensor amplifier", result.Facts.Module.DisplayName);
    }

    [Fact]
    public void ShortenedTieredModuleNameResolvesWithinRobotCompatibilityQuestion()
    {
        SparkyKnowledgeBundle source = CreateBundle();
        SparkyRobotRecord robot = RobotVariant(source.Robots.Single(), "def_arbalest_mk2", "Arbalest", 1, 2);
        SparkyRobotModuleRecord module = CreateModule("def_medium_armor_plate_t4", "Standard medium armor plate", false,
            "Standard medium armor plate", "Standard mittlere Panzerplatte", "Placa média padrão") with
        {
            TierMetadata = Tier(1, 4)
        };
        SparkyKnowledgeBundle bundle = CreateTierAwareBundle([robot], [module]);

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "Would T4 medium plate fit Mk2 Arbalest?", "Would T4 medium plate fit Mark 2 Arbalest?",
            bundle, 0, null, SparkyRobotQuestionFocus.ModuleCompatibility);

        Assert.Equal("found", result.Status);
        Assert.NotNull(result.Facts!.Module);
        Assert.Equal("Standard medium armor plate", result.Facts.Module.DisplayName);
    }

    [Fact]
    public void UnqualifiedRobotNameCollisionAcrossTiersIsAmbiguous()
    {
        SparkyRobotRecord template = CreateBundle().Robots.Single();
        SparkyKnowledgeBundle bundle = CreateTierAwareBundle(
            RobotVariant(template, "def_arbalest_mk1", "Arbalest", 1, 1),
            RobotVariant(template, "def_arbalest_mk2", "Arbalest", 1, 2));

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "What are the stats for Arbalest?", "Arbalest stats", bundle, 0, null,
            SparkyRobotQuestionFocus.Stats);

        Assert.Equal("ambiguous", result.Status);
        Assert.Null(result.Facts);
    }

    [Fact]
    public void UnsupportedRobotTierDoesNotResolveByNameOrPriorContext()
    {
        SparkyRobotRecord template = CreateBundle().Robots.Single();
        SparkyKnowledgeBundle bundle = CreateTierAwareBundle(
            RobotVariant(template, "def_arbalest_mk2", "Arbalest", 1, 2));

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "What about Mk4 Arbalest?", "Mk4 Arbalest", bundle, 0, "Arbalest",
            SparkyRobotQuestionFocus.Stats);

        Assert.Equal("not_found", result.Status);
        Assert.Null(result.Facts);
    }

    [Fact]
    public void ChassisBonusFocusReturnsTranslatedLevelOneResults()
    {
        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "What chassis bonuses does Sample robot have?",
            "Sample robot chassis bonuses",
            CreateBundle(),
            0,
            null,
            SparkyRobotQuestionFocus.ChassisBonus);

        Assert.Equal("found", result.Status);
        Assert.NotNull(result.Facts);
        Assert.Empty(result.Facts.AssembledStats);
        Assert.Single(result.Facts.ChassisBonuses);
        Assert.Equal(1, result.Facts.ChassisBonuses[0].ExtensionLevel);
        Assert.Equal("Robot specialist", result.Facts.ChassisBonuses[0].ExtensionName);
    }

    [Theory]
    [InlineData("Good module", true, 2)]
    [InlineData("Bad module", false, 0)]
    public void ModuleCompatibilityReturnsOnlyTheSelectedModuleAndEligibleSlots(
        string moduleName,
        bool compatible,
        int expectedSlotCount)
    {
        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            $"Can {moduleName} fit Sample robot?",
            $"Can {moduleName} fit Sample robot?",
            CreateBundle(),
            0,
            null,
            SparkyRobotQuestionFocus.ModuleCompatibility);

        Assert.Equal("found", result.Status);
        Assert.NotNull(result.Facts);
        Assert.Equal(moduleName, result.Facts.Module!.DisplayName);
        Assert.Equal(compatible, result.Facts.Module.HasStaticSlotEligibility);
        Assert.False(result.Facts.Module.CurrentLoadoutFitEvaluated);
        Assert.Contains("static slot eligibility only", result.Facts.Module.CompatibilityScope, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedSlotCount, result.Facts.Components.Sum(component => component.EligibleSlots.Count));
        Assert.Empty(result.Facts.AssembledStats);
        Assert.Empty(result.Facts.ChassisBonuses);
        string promptFacts = JsonSerializer.Serialize(result.Facts);
        Assert.Contains(
            $"\"HasStaticSlotEligibility\":{compatible.ToString().ToLowerInvariant()}",
            promptFacts,
            StringComparison.Ordinal);
        Assert.Contains("\"CurrentLoadoutFitEvaluated\":false", promptFacts, StringComparison.Ordinal);
        Assert.Contains("static slot eligibility only", promptFacts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("def_", promptFacts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ModuleDefinitionName", promptFacts, StringComparison.Ordinal);
    }

    [Fact]
    public void DistinctRobotMentionsRemainAmbiguousWhenOneNameContainsAnother()
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        SparkyRobotRecord sample = bundle.Robots.Single();
        string[] names = ["Castel", "Kain", "Kain Mk2"];
        SparkyRobotRecord[] robots = names.Select((name, index) => sample with
        {
            DefinitionName = $"def_robot_{index}",
            LocalizedViews = sample.LocalizedViews.ToDictionary(
                entry => entry.Key,
                entry => entry.Value with { DisplayName = entry.Key == 0 ? name : $"{name}-{entry.Key}" })
        }).ToArray();
        SparkyKnowledgeBundle withRobots = bundle with
        {
            Robots = robots,
            SearchIndex = new SparkySearchIndex(
                new Dictionary<string, IReadOnlyList<int>>(),
                SparkyAliasCatalog.Build(bundle.Languages, [], [], robots, bundle.RobotModules))
        };

        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "Compare Castel and Kain Mk2 stats",
            "Compare Castel and Kain Mk2 stats",
            withRobots,
            0,
            null,
            SparkyRobotQuestionFocus.Stats);

        Assert.Equal("ambiguous", result.Status);
        Assert.Equal(new[] { "Castel", "Kain Mk2" }, result.Candidates);
    }

    [Fact]
    public void LocalizedFollowUpCanReuseContextWhenEnglishInterpretationResolvesPronoun()
    {
        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "А какие у него компоненты?",
            "What components does it have?",
            CreateBundle(),
            4,
            "Sample robot",
            SparkyRobotQuestionFocus.Components);

        Assert.Equal("found", result.Status);
        Assert.Equal("Тестовый робот", result.Facts!.RobotName);
        Assert.Single(result.Facts.Components);
    }

    [Fact]
    public void EnglishAliasCanResolveButDoesNotBecomeAnEnglishFallbackName()
    {
        SparkyRobotLookupResult translated = SparkyRobotLookup.Resolve(
            "What are the stats for Sample robot?",
            "Sample robot stats",
            CreateBundle(germanRobotName: "Beispielroboter"),
            2,
            null,
            SparkyRobotQuestionFocus.Stats);
        SparkyRobotLookupResult missingTranslation = SparkyRobotLookup.Resolve(
            "What are the stats for Sample robot?",
            "Sample robot stats",
            CreateBundle(germanRobotName: null),
            2,
            null,
            SparkyRobotQuestionFocus.Stats);

        Assert.Equal("found", translated.Status);
        Assert.Equal("Beispielroboter", translated.Facts!.RobotName);
        Assert.Equal("not_found", missingTranslation.Status);
        Assert.Null(missingTranslation.Facts);
    }

    [Theory]
    [InlineData(0, "Sample robot", "Robot head", "Robot specialist", "Good module")]
    [InlineData(2, "Beispielroboter", "Roboterkopf", "Roboter-Spezialist", "Gutes Modul")]
    [InlineData(3, "Robô de amostra", "Cabeça do robô", "Especialista em robôs", "Módulo bom")]
    [InlineData(4, "Тестовый робот", "Голова робота", "Специалист по роботам", "Хороший модуль")]
    public void LocalizedFactsResolveInEveryIncludedDictionary(
        int languageId,
        string robotName,
        string componentName,
        string bonusName,
        string moduleName)
    {
        SparkyKnowledgeBundle bundle = CreateBundle(germanRobotName: "Beispielroboter");
        SparkyRobotLookupResult stats = SparkyRobotLookup.Resolve(
            $"Robot stats for {robotName}", "Sample robot stats", bundle, languageId, null, SparkyRobotQuestionFocus.Stats);
        SparkyRobotLookupResult components = SparkyRobotLookup.Resolve(
            $"Show parts of {robotName}", "Sample robot component breakdown", bundle, languageId, null, SparkyRobotQuestionFocus.Components);
        SparkyRobotLookupResult bonuses = SparkyRobotLookup.Resolve(
            $"What chassis bonuses does {robotName} have?", "Sample robot chassis bonuses", bundle, languageId, null, SparkyRobotQuestionFocus.ChassisBonus);
        SparkyRobotLookupResult compatibility = SparkyRobotLookup.Resolve(
            $"Can {moduleName} fit {robotName}?", "Can Good module fit Sample robot?", bundle, languageId, null, SparkyRobotQuestionFocus.ModuleCompatibility);

        Assert.Equal("found", stats.Status);
        Assert.Equal(robotName, stats.Facts!.RobotName);
        Assert.Equal("found", components.Status);
        Assert.Equal(componentName, components.Facts!.Components.Single().DisplayName);
        Assert.Equal("found", bonuses.Status);
        Assert.Equal(bonusName, bonuses.Facts!.ChassisBonuses.Single().ExtensionName);
        Assert.Equal("found", compatibility.Status);
        Assert.Equal(moduleName, compatibility.Facts!.Module!.DisplayName);
    }

    [Fact]
    public void ShortFollowUpUsesOnlyAUniquePublicContextRobot()
    {
        SparkyRobotLookupResult followUp = SparkyRobotLookup.Resolve(
            "And what are its components?",
            "components",
            CreateBundle(),
            0,
            "Sample robot",
            SparkyRobotQuestionFocus.Components);
        SparkyRobotLookupResult unrelated = SparkyRobotLookup.Resolve(
            "Tell me about an unknown robot",
            "unknown robot",
            CreateBundle(),
            0,
            "Sample robot",
            SparkyRobotQuestionFocus.General);

        Assert.Equal("found", followUp.Status);
        Assert.Equal("Sample robot", followUp.Facts!.RobotName);
        Assert.Equal("not_found", unrelated.Status);
    }

    [Fact]
    public void AmbiguousRobotAliasReturnsLocalizedCandidates()
    {
        SparkyRobotLookupResult result = SparkyRobotLookup.Resolve(
            "What are the stats for Sample robot?",
            "Sample robot stats",
            CreateBundle(includeAliasCollision: true),
            0,
            null,
            SparkyRobotQuestionFocus.Stats);

        Assert.Equal("ambiguous", result.Status);
        Assert.Null(result.Facts);
        Assert.Equal(new[] { "Sample robot", "Sample-robot" }, result.Candidates);
        Assert.DoesNotContain(result.Candidates, candidate => candidate.StartsWith("def_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RouterUsesLocalizedMissingTermTemplateWhenRobotTranslationIsAbsent()
    {
        var router = new SparkyAnswerRouter(CreateOptions());
        SparkyQuestionInterpretation interpretation = new(
            2,
            SparkyIntent.StructuredRobot,
            "Sample robot stats",
            RobotFocus: SparkyRobotQuestionFocus.Stats,
            Scope: SparkyConversationScope.Game);

        SparkyAnswerResult result = await router.AnswerAsync(
            "What are the stats for Sample robot?",
            interpretation,
            CreateBundle(germanRobotName: null),
            null,
            null,
            CancellationToken.None);

        Assert.Equal("not_found", result.Status);
        Assert.Contains("englischen Namen", result.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RouterReturnsLocalizedAmbiguityBeforeInference()
    {
        var router = new SparkyAnswerRouter(CreateOptions());
        SparkyQuestionInterpretation interpretation = new(
            0,
            SparkyIntent.StructuredRobot,
            "Sample robot stats",
            RobotFocus: SparkyRobotQuestionFocus.Stats,
            Scope: SparkyConversationScope.Game);

        SparkyAnswerResult result = await router.AnswerAsync(
            "What are the stats for Sample robot?",
            interpretation,
            CreateBundle(includeAliasCollision: true),
            null,
            null,
            CancellationToken.None);

        Assert.Equal("ambiguous", result.Status);
        Assert.Contains("possible matches", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("items", result.Answer, StringComparison.OrdinalIgnoreCase);
    }

    private static SparkyKnowledgeBundle CreateTierAwareBundle(params SparkyRobotRecord[] robots)
    {
        SparkyRobotModuleRecord[] modules = CreateBundle().RobotModules
            .Select(module => module with { TierMetadata = Tier(null, null) })
            .ToArray();
        return CreateTierAwareBundle(robots, modules);
    }

    private static SparkyKnowledgeBundle CreateTierAwareBundle(
        IReadOnlyList<SparkyRobotRecord> robots,
        IReadOnlyList<SparkyRobotModuleRecord> modules)
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        return bundle with
        {
            SchemaVersion = 4,
            EntityResolutionRules = TierRules,
            Robots = robots,
            RobotModules = modules
        };
    }

    private static SparkyRobotRecord RobotVariant(
        SparkyRobotRecord template,
        string definitionName,
        string displayName,
        int? tierType,
        int? tierLevel) => template with
    {
        DefinitionName = definitionName,
        TierMetadata = Tier(tierType, tierLevel),
        LocalizedViews = template.LocalizedViews.ToDictionary(
            entry => entry.Key,
            entry => entry.Value with { DisplayName = displayName })
    };

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

    private static SparkyKnowledgeBundle CreateBundle(string? germanRobotName = "Sample robot", bool includeAliasCollision = false)
    {
        var englishTranslations = new Dictionary<string, string>(StringComparer.Ordinal);
        var germanTranslations = new Dictionary<string, string>(StringComparer.Ordinal);
        var languages = new[]
        {
            CreateLanguage(0, "English", englishTranslations),
            CreateLanguage(2, "German", germanTranslations),
            CreateLanguage(3, "Portuguese", new Dictionary<string, string>(StringComparer.Ordinal)),
            CreateLanguage(4, "Russian", new Dictionary<string, string>(StringComparer.Ordinal))
        };
        var component = new SparkyLocalizedRobotComponentView(
            "def_sample_robot_head",
            "Robot head",
            [new AnswerStat { FieldId = 1, Label = "Armor max", Value = "100", Unit = "HP" }],
            2);
        var englishView = new SparkyLocalizedRobotView(
            "Sample robot",
            [new AnswerStat { FieldId = 2, Label = "Mass", Value = "250", Unit = "kg" }],
            [component],
            [new SparkyLocalizedChassisBonus("Robot specialist", "Armor modifier", "5", "%", 1, ["Robot chassis"])]);
        var germanView = new SparkyLocalizedRobotView(
            germanRobotName,
            germanRobotName is null ? [] : englishView.AssembledStats,
            germanRobotName is null ? [] : [component with { DisplayName = "Roboterkopf" }],
            germanRobotName is null ? [] : [new SparkyLocalizedChassisBonus("Roboter-Spezialist", "Panzermodifikator", "5", "%", 1, ["Roboterchassis"])]);
        var portugueseView = new SparkyLocalizedRobotView(
            "Robô de amostra",
            englishView.AssembledStats,
            [component with { DisplayName = "Cabeça do robô" }],
            [new SparkyLocalizedChassisBonus("Especialista em robôs", "Modificador de armadura", "5", "%", 1, ["Chassi do robô"])]);
        var russianView = new SparkyLocalizedRobotView(
            "Тестовый робот",
            englishView.AssembledStats,
            [component with { DisplayName = "Голова робота" }],
            [new SparkyLocalizedChassisBonus("Специалист по роботам", "Модификатор брони", "5", "%", 1, ["Шасси робота"])]);
        var robot = new SparkyRobotRecord(
            "def_sample_robot",
            ["def_sample_robot_head"],
            [new SparkyRobotCompatibility("def_sample_robot_head", "def_good_module", [1, 2])],
            [],
            new Dictionary<int, SparkyLocalizedRobotView> { [0] = englishView, [2] = germanView, [3] = portugueseView, [4] = russianView });
        var robots = includeAliasCollision
            ? new[] { robot, robot with { DefinitionName = "def_another_robot", LocalizedViews = new Dictionary<int, SparkyLocalizedRobotView> { [0] = englishView with { DisplayName = "Sample-robot" }, [2] = germanView with { DisplayName = "Anderes robot" }, [3] = portugueseView with { DisplayName = "Outro robô" }, [4] = russianView with { DisplayName = "Другой робот" } } } }
            : [robot];
        var modules = new[]
        {
            CreateModule("def_good_module", "Good module", true, "Gutes Modul", "Módulo bom", "Хороший модуль"),
            CreateModule("def_bad_module", "Bad module", false, "Schlechtes Modul", "Módulo ruim", "Плохой модуль")
        };
        var searchIndex = new SparkySearchIndex(
            new Dictionary<string, IReadOnlyList<int>>(),
            SparkyAliasCatalog.Build(languages, [], [], robots, modules));
        var manifest = new SparkyBundleManifest(
            "bundle-version",
            new string('B', 64),
            DateTimeOffset.UtcNow,
            languages.ToDictionary(language => language.LanguageId, language => language.DictionaryHash),
            new Dictionary<string, string> { ["fixture-source"] = new string('A', 40) });
        return new SparkyKnowledgeBundle(2, manifest, languages, [], [], [], searchIndex)
        {
            Robots = robots,
            RobotModules = modules
        };
    }

    private static SparkyKnowledgeBundle CreateBundleWithOtherRobot()
    {
        SparkyKnowledgeBundle bundle = CreateBundle();
        SparkyRobotRecord sample = bundle.Robots.Single();
        SparkyRobotRecord other = sample with
        {
            DefinitionName = "def_other_robot",
            LocalizedViews = sample.LocalizedViews.ToDictionary(
                entry => entry.Key,
                entry => entry.Value with
                {
                    DisplayName = entry.Key switch
                    {
                        0 => "Other robot",
                        2 => "Anderer Roboter",
                        3 => "Outro robô",
                        4 => "Другой робот",
                        _ => entry.Value.DisplayName
                    }
                })
        };
        return bundle with { Robots = [sample, other] };
    }

    private static SparkySupportedLanguage CreateLanguage(int id, string name, IReadOnlyDictionary<string, string> translations)
    {
        return new SparkySupportedLanguage(id, name, SparkyDictionaryCatalog.ComputeDictionaryHash(translations), translations);
    }

    private static SparkyServiceOptions CreateOptions() => new(
        "bundle.json",
        new Uri("https://127.0.0.1:5080"),
        null!,
        "test-token",
        "http://localhost:1234/v1",
        "test-model",
        null,
        8192,
        2000,
        2000,
        700,
        TimeSpan.FromMilliseconds(100),
        1,
        null);

    private static SparkyRobotModuleRecord CreateModule(
        string definition,
        string name,
        bool unique,
        string germanName,
        string portugueseName,
        string russianName)
    {
        SparkyLocalizedRobotModuleView MakeView(string? displayName) => new(
            displayName,
            displayName is null ? [] : [new AnswerStat { FieldId = 3, Label = "CPU usage", Value = "10", Unit = "TF" }],
            unique);
        return new SparkyRobotModuleRecord(
            definition,
            new Dictionary<int, SparkyLocalizedRobotModuleView>
            {
                [0] = MakeView(name),
                [2] = MakeView(germanName),
                [3] = MakeView(portugueseName),
                [4] = MakeView(russianName)
            });
    }

    private static SparkyRobotRecord WithSpeed(SparkyRobotRecord robot, string name, string speed, string mass = "250")
    {
        var views = robot.LocalizedViews.ToDictionary(
            entry => entry.Key,
            entry => entry.Value with
            {
                DisplayName = entry.Key switch
                {
                    0 => name,
                    2 => name,
                    3 => name,
                    4 => name,
                    _ => entry.Value.DisplayName
                },
                AssembledStats =
                [
                    ..entry.Value.AssembledStats.Where(stat =>
                        stat.FieldId != (int)AggregateField.speed_max && stat.FieldId != 2),
                    new AnswerStat { FieldId = 2, Label = "Mass", Value = mass, Unit = "kg" },
                    new AnswerStat { FieldId = (int)AggregateField.speed_max, Label = "Max speed", Value = speed, Unit = "m/s" }
                ]
            });
        return robot with { LocalizedViews = views };
    }

    private static SparkyQuestionInterpretation InvokeRobotIntentCorrection(
        string question,
        SparkyQuestionInterpretation interpretation,
        SparkyKnowledgeBundle bundle)
    {
        return SparkyRobotIntentResolver.CorrectStructuredItemQuestion(question, interpretation, bundle);
    }

}
