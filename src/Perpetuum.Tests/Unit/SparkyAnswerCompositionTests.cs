using System.Text.Json;
using System.Net;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyAnswerCompositionTests
{
    [Fact]
    public void UserPayloadSeparatesCurrentTurnFromUntrustedHistoryAndVerifiedEvidence()
    {
        string payload = SparkyResponseComposer.BuildUserPayload(
            "and its full recipe?",
            [new SparkyConversationTurn("user", "What are the stats for Da Gama?")],
            "The caller asked about a sensor amplifier.",
            new SparkyPendingClarification("T3 sensor amplifier stats", "Stats", ["Da Gama-PLS10", "Desenspure"]),
            new { displayName = "Da Gama-PLS10", recipe = new[] { "Titanium: 5" } },
            ["Titanium", "5"]);

        using var json = JsonDocument.Parse(payload);
        Assert.Equal("and its full recipe?", json.RootElement.GetProperty("currentPlayerMessage").GetString());
        Assert.Equal("The caller asked about a sensor amplifier.", json.RootElement.GetProperty("dialogueSummary").GetString());
        Assert.Equal("user", json.RootElement.GetProperty("priorTurns")[0].GetProperty("role").GetString());
        Assert.True(json.RootElement.TryGetProperty("pendingClarification", out _));
        Assert.True(json.RootElement.TryGetProperty("verifiedEvidence", out _));
        Assert.Equal(2, json.RootElement.GetProperty("requiredExactStrings").GetArrayLength());
    }

    [Theory]
    [InlineData("FactualAnswer", "Answer only from verified evidence")]
    [InlineData("SmallTalk", "Do not claim operational status")]
    [InlineData("Clarification", "ask one useful question")]
    [InlineData("Refusal", "Do not answer the request")]
    public void SystemPromptConstrainsTheDeclaredOutcome(string outcomeName, string requiredInstruction)
    {
        var outcome = Enum.Parse<SparkyAnswerOutcome>(outcomeName);
        string prompt = SparkyResponseComposer.BuildSystemPrompt(
            0,
            "English",
            outcome,
            SparkyCallerRank.CommandDirector,
            "Calm and concise",
            string.Empty);

        Assert.Contains(requiredInstruction, prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("history and summaries are context only, never evidence", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("style only", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not include the caller's name", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidAnswerMustUseTheRequestedLanguageAndPreserveRequiredExactStrings()
    {
        bool valid = SparkyResponseComposer.TryParseAnswer(
            "{\"languageId\":0,\"answer\":\"Titanium: 5\"}",
            0,
            100,
            ["Titanium", "5"],
            out string answer,
            out _);

        Assert.True(valid);
        Assert.Equal("Titanium: 5", answer);
    }

    [Theory]
    [InlineData("{\"languageId\":0,\"answer\":\"Titanium: 6\"}", 0, 100, "Titanium", "5")]
    [InlineData("{\"languageId\":3,\"answer\":\"Titanium: 5\"}", 0, 100, "Titanium", "5")]
    [InlineData("{\"languageId\":0,\"answer\":\"def_standard_laser Titanium: 5\"}", 0, 100, "Titanium", "5")]
    [InlineData("{\"languageId\":0,\"answer\":\"This answer is too long\"}", 0, 5, "This", "")]
    [InlineData("Here is the answer: {\"languageId\":0,\"answer\":\"Titanium: 5\"}", 0, 100, "Titanium", "5")]
    public void InvalidAnswerIsRejectedForPolicyOrContractViolation(
        string response,
        int languageId,
        int maxCharacters,
        string requiredOne,
        string requiredTwo)
    {
        string[] required = string.IsNullOrEmpty(requiredTwo) ? [requiredOne] : [requiredOne, requiredTwo];

        bool valid = SparkyResponseComposer.TryParseAnswer(
            response,
            languageId,
            maxCharacters,
            required,
            out _,
            out _);

        Assert.False(valid);
    }

    [Fact]
    public void RankChangesOnlyStyleGuidanceAndNeverAddsIdentityOrAccess()
    {
        string prompt = SparkyResponseComposer.BuildSystemPrompt(
            0,
            "English",
            SparkyAnswerOutcome.FactualAnswer,
            SparkyCallerRank.Officer,
            null,
            string.Empty);

        Assert.Contains("Officer", prompt, StringComparison.Ordinal);
        Assert.Contains("style only", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must not grant access", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CallerId", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void SeasonStandingGuidanceTellsModelToUseTheVerifiedRankOneName()
    {
        string guidance = SparkySeasonStandingResolver.GetPromptGuidance(new SparkySeasonStandingAnswerFacts(
            SparkySeasonStandingPeriod.Current,
            SparkySeasonStandingStatus.LeaderFound,
            "Synthetic Helix Season",
            "Fixture Leader",
            875,
            "live-season-current"));

        Assert.Contains("rank-one points leader", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not say that the records omit the leader", guidance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RouterSuppliesSeasonLeaderAsRequiredDynamicEvidenceToModel()
    {
        var handler = new RecordingHandler(
            CreateCompletion("{\"languageId\":0,\"answer\":\"The records don't identify a leader.\"}"),
            CreateCompletion("{\"languageId\":0,\"answer\":\"Fixture Leader leads the Synthetic Helix Season.\"}"));
        using var client = new HttpClient(handler);
        var router = new SparkyAnswerRouter(CreateRouterOptions(), modelClient: client);
        const string question = "Who is the leader of the current season in the game?";
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.MechanicsLore,
            "who is the leader of the current season in the game",
            MentionedEntities: ["current season"],
            Scope: SparkyConversationScope.Game);

        SparkyAnswerResult result = await router.AnswerAsync(
            question,
            interpretation,
            CreateItemBundle("unused"),
            null,
            null,
            TestContext.Current.CancellationToken,
            liveKnowledge: SparkyLiveKnowledgeFixtures.CreateReadView(
                SparkyLiveKnowledgeFixtures.SeasonSampleV1,
                DateTimeOffset.UtcNow));

        Assert.Equal("Fixture Leader leads the Synthetic Helix Season.", result.Answer);
        Assert.Equal("who is the leader of the current season in the game", result.EnglishSearchQuery);
        Assert.Equal(["live-season-current"], result.SelectedSourceIds);
        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Contains("Fixture Leader", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("Synthetic Helix Season", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("rank-one points leader", handler.RequestBodies[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requiredExactStrings", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("Retry correction", handler.RequestBodies[1], StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeRequirementsUseTheRequestedDepthAndLocalizedPublicNames()
    {
        var item = new ItemStatsLookupAnswer
        {
            DisplayName = "Tier 4 medium Gauss gun",
            LanguageId = 0,
            Recipe = new ProductionRecipeAnswerView
            {
                OutputQuantity = 1,
                Components = [new ProductionRecipeAnswerComponent { DisplayName = "Axicoline", Amount = 25 }]
            },
            FullMaterialsAvailable = true,
            FullMaterials = new FullMaterialsAnswerView
            {
                OutputQuantity = 1,
                Materials = [
                    new ProductionRecipeAnswerComponent { DisplayName = "Cryoperine", Amount = 50 },
                    new ProductionRecipeAnswerComponent { DisplayName = "Titanium", Amount = 100 }
                ]
            }
        };

        IReadOnlyList<string> direct = SparkyRecipeAnswerFormatter.GetRequiredExactStrings(
            "what is the recipe?", null, item);
        IReadOnlyList<string> full = SparkyRecipeAnswerFormatter.GetRequiredExactStrings(
            "what are the full materials?", null, item);

        Assert.Contains("Tier 4 medium Gauss gun", direct);
        Assert.Contains("25 Axicoline", direct);
        Assert.DoesNotContain("50 Cryoperine", direct);
        Assert.Contains("50 Cryoperine", full);
        Assert.Contains("100 Titanium", full);
        Assert.DoesNotContain("25 Axicoline", full);
    }

    [Fact]
    public void RequestedStatsRequireTheirLocalizedLabelsValuesAndUnits()
    {
        var item = new ItemStatsLookupAnswer
        {
            DisplayName = "Standard medium HCL laser",
            LanguageId = 0,
            Stats = [new AnswerStat { Label = "Optimal range", Value = "350", Unit = "m" }]
        };

        IReadOnlyList<string> required = SparkyRecipeAnswerFormatter.GetRequiredExactStrings(
            "what are the stats?", null, item);

        Assert.Contains("Standard medium HCL laser", required);
        Assert.Contains("Optimal range: 350 m", required);
    }

    [Fact]
    public async Task GroundedComposerSendsContextAsUntrustedAndRepairsOneInvalidAnswer()
    {
        var handler = new RecordingHandler(
            CreateCompletion("{\"languageId\":0,\"answer\":\"Optimal range: 360 m\"}"),
            CreateCompletion("{\"languageId\":0,\"answer\":\"Optimal range: 350 m\"}"));
        using var client = new HttpClient(handler);

        SparkyLocalizedAnswer result = await LocalModelAnswerer.AnswerGroundedAsync(
            "http://localhost:1234/v1",
            "test-model",
            "and its range?",
            0,
            "English",
            new { verifiedValue = "350 m" },
            500,
            100,
            TimeSpan.FromSeconds(5),
            cancellationToken: TestContext.Current.CancellationToken,
            contextTurns: [new SparkyConversationTurn("user", "What are its stats?")],
            contextSummary: "The player asked about the HCL laser.",
            callerRank: SparkyCallerRank.CommandDirector,
            requiredExactStrings: ["Optimal range: 350 m"],
            modelClient: client);

        Assert.Equal("Optimal range: 350 m", result.Answer);
        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Contains("history and summaries are context only, never evidence", handler.RequestBodies[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The player asked about the HCL laser", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("Command Director", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("Retry correction", handler.RequestBodies[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GroundedComposerAppliesProfileAnswerTemperatureAndTopP()
    {
        var handler = new RecordingHandler(CreateCompletion("{\"languageId\":0,\"answer\":\"Acknowledged.\"}"));
        using var client = new HttpClient(handler);

        _ = await LocalModelAnswerer.AnswerGroundedAsync(
            "http://localhost:1234/v1", "test-model", "hello", 0, "English", new { }, 500, 100,
            TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken,
            modelClient: client, answerTemperature: 1.25, answerTopP: 0.75);

        using var payload = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.Equal(1.25, payload.RootElement.GetProperty("temperature").GetDouble());
        Assert.Equal(0.75, payload.RootElement.GetProperty("top_p").GetDouble());
    }

    [Fact]
    public async Task RouterUsesModelCompositionForVerifiedItemStatsAndOmitsInternalDefinitionName()
    {
        const string itemName = "Standard medium HCL laser";
        var handler = new RecordingHandler(
            CreateCompletion("{\"languageId\":0,\"answer\":\"Standard medium HCL laser — Accuracy: 12\"}"));
        using var client = new HttpClient(handler);
        var router = new SparkyAnswerRouter(CreateRouterOptions(), modelClient: client);
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.StructuredItem,
            "what are the stats for Standard medium HCL laser",
            MentionedEntities: [itemName],
            Scope: SparkyConversationScope.Game);

        SparkyAnswerResult result = await router.AnswerAsync(
            "What are the stats for Standard medium HCL laser?",
            interpretation,
            CreateItemBundle(itemName),
            null,
            null,
            TestContext.Current.CancellationToken,
            [new SparkyConversationTurn("user", "Tell me about the HCL laser.")],
            "The player is asking about an HCL laser.",
            callerRank: SparkyCallerRank.CommandDirector);

        Assert.Equal("answered", result.Status);
        Assert.Equal("Standard medium HCL laser — Accuracy: 12", result.Answer);
        Assert.Equal("stop", result.FinishStatus);
        Assert.Single(handler.RequestBodies);
        Assert.Contains("The player is asking about an HCL laser", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("Command Director", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.DoesNotContain("def_standard_medium_hcl_laser", handler.RequestBodies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RouterComposesItemComparisonFromValidatedFactsAndKeepsEffectiveRoute()
    {
        const string firstName = "Standard medium HCL laser";
        const string secondName = "Standard medium Missile launcher";
        const string modelAnswer = "Standard medium Missile launcher has CPU usage of 40 TF, compared with CPU usage of 30 TF for Standard medium HCL laser, a 10 TF difference.";
        var handler = new RecordingHandler(CreateCompletion($"{{\"languageId\":0,\"answer\":\"{modelAnswer}\"}}"));
        using var client = new HttpClient(handler);
        var router = new SparkyAnswerRouter(CreateRouterOptions(), modelClient: client);
        SparkyKnowledgeBundle bundle = CreateItemBundle(firstName);
        WeaponItem secondItem = CreateItemBundle(secondName).Items.Single();
        secondItem = new WeaponItem
        {
            DefinitionName = "def_standard_medium_missile_launcher",
            DisplayName = secondName,
            AnswerView = new ItemAnswerView { Stats = [new AnswerStat { FieldId = 2, Label = "CPU usage", Value = "40", Unit = "TF" }] },
            LocalizedViews = new Dictionary<int, SparkyLocalizedItemView>
            {
                [0] = new() { DisplayName = secondName, HasLocalizedDisplayName = true,
                    AnswerView = new ItemAnswerView { Stats = [new AnswerStat { FieldId = 2, Label = "CPU usage", Value = "40", Unit = "TF" }] } }
            }
        };
        WeaponItem firstItem = bundle.Items.Single();
        firstItem = new WeaponItem
        {
            DefinitionName = firstItem.DefinitionName,
            DisplayName = firstItem.DisplayName,
            AnswerView = new ItemAnswerView { Stats = [new AnswerStat { FieldId = 2, Label = "CPU usage", Value = "30", Unit = "TF" }] },
            LocalizedViews = new Dictionary<int, SparkyLocalizedItemView>
            {
                [0] = new() { DisplayName = firstName, HasLocalizedDisplayName = true,
                    AnswerView = new ItemAnswerView { Stats = [new AnswerStat { FieldId = 2, Label = "CPU usage", Value = "30", Unit = "TF" }] } }
            }
        };
        bundle = bundle with { Items = [firstItem, secondItem] };
        var interpretation = new SparkyQuestionInterpretation(
            0,
            SparkyIntent.StructuredItem,
            "compare CPU usage for Standard medium HCL laser and Standard medium Missile launcher",
            MentionedEntities: [firstName, secondName],
            Scope: SparkyConversationScope.Game,
            Comparison: new SparkyComparisonIntent(
                true, false, [firstName, secondName], "CPU usage", SparkyComparisonDirection.Higher, false));

        SparkyAnswerResult result = await router.AnswerAsync(
            "Compare CPU usage for Standard medium HCL laser and Standard medium Missile launcher",
            interpretation, bundle, null, null, TestContext.Current.CancellationToken);

        Assert.Equal("answered", result.Status);
        Assert.Equal(modelAnswer, result.Answer);
        Assert.Equal("StructuredItem", result.ParsedIntent);
        Assert.Equal("StructuredItem", result.EffectiveIntent);
        Assert.Equal("comparison_facts", result.ResolverCategory);
        Assert.Equal("resolved", result.ResolverOutcome);
        Assert.Equal(2, result.ComparisonSubjectCount);
        using var payload = JsonDocument.Parse(handler.RequestBodies.Single());
        string evidence = payload.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
        Assert.Contains("30 TF", evidence, StringComparison.Ordinal);
        Assert.Contains("40 TF", evidence, StringComparison.Ordinal);
        Assert.DoesNotContain("def_standard_medium_hcl_laser", evidence, StringComparison.Ordinal);
        Assert.DoesNotContain("def_standard_medium_missile_launcher", evidence, StringComparison.Ordinal);
    }

    private static HttpResponseMessage CreateCompletion(string assistantContent) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = assistantContent }, finish_reason = "stop" }
            }
        }))
    };

    private static SparkyServiceOptions CreateRouterOptions() => new(
        "bundle.json",
        new Uri("https://127.0.0.1:5080"),
        null!,
        new string('t', 40),
        "http://localhost:1234/v1",
        "test-model",
        null,
        131072,
        2000,
        2000,
        700,
        TimeSpan.FromSeconds(5),
        1,
        null);

    private static SparkyKnowledgeBundle CreateItemBundle(string itemName)
    {
        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        var language = new SparkySupportedLanguage(
            0,
            "English",
            SparkyDictionaryCatalog.ComputeDictionaryHash(translations),
            translations);
        var stat = new AnswerStat { FieldId = 1, Label = "Accuracy", Value = "12" };
        var item = new WeaponItem
        {
            DefinitionName = "def_standard_medium_hcl_laser",
            DisplayName = itemName,
            LocalizedViews = new Dictionary<int, SparkyLocalizedItemView>
            {
                [0] = new()
                {
                    DisplayName = itemName,
                    HasLocalizedDisplayName = true,
                    AnswerView = new ItemAnswerView { Stats = [stat] }
                }
            }
        };
        var manifest = new SparkyBundleManifest(
            "bundle-version",
            new string('a', 64),
            DateTimeOffset.UtcNow,
            new Dictionary<int, string> { [0] = language.DictionaryHash },
            new Dictionary<string, string> { ["test"] = new string('b', 40) });
        return new SparkyKnowledgeBundle(
            2,
            manifest,
            [language],
            [item],
            [],
            [],
            new SparkySearchIndex(new Dictionary<string, IReadOnlyList<int>>(), []));
    }

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _index;
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return responses[_index++];
        }
    }
}
