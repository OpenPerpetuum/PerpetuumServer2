using System.Text.Json;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyComparisonInterpretationTests
{
    private static readonly IReadOnlyList<SparkySupportedLanguage> Languages =
        [new SparkySupportedLanguage(0, "English", "hash", new Dictionary<string, string>())];

    [Fact]
    public void Parses_contextual_follow_up_comparison_intent()
    {
        const string json = """
            {"languageId":0,"intent":"StructuredRobot","scope":"Game","englishSearchQuery":"compare maximum speed of the two robots","requestedAnswerType":"Stats","selectedCandidate":null,"followUpAction":"Continue","contextSummary":null,"robotFocus":"CompareStats","catalogFocus":null,"smallTalkKind":null,"mentionedEntities":[],"comparison":{"isComparison":true,"isContextual":true,"subjectReferences":[],"metricPhrase":"maximum speed","direction":"Higher","compareAllSharedFields":false}}
            """;

        Assert.True(SparkyQuestionInterpreter.TryParseConversationInterpretation(
            json, Languages, 0, null, out var parsed));
        Assert.NotNull(parsed.Comparison);
        Assert.True(parsed.Comparison.IsComparison);
        Assert.True(parsed.Comparison.IsContextual);
        Assert.Empty(parsed.Comparison.SubjectReferences);
        Assert.Equal("maximum speed", parsed.Comparison.MetricPhrase);
        Assert.Equal(SparkyComparisonDirection.Higher, parsed.Comparison.Direction);
        Assert.False(parsed.Comparison.CompareAllSharedFields);
    }

    [Fact]
    public void Accepts_multiple_proposed_subjects_so_resolver_can_ask_for_clarification()
    {
        const string json = """
            {"languageId":0,"intent":"StructuredRobot","scope":"Game","englishSearchQuery":"compare robot speeds","requestedAnswerType":"Stats","selectedCandidate":null,"followUpAction":"Continue","contextSummary":null,"robotFocus":"CompareStats","catalogFocus":null,"smallTalkKind":null,"mentionedEntities":[],"comparison":{"isComparison":true,"isContextual":true,"subjectReferences":["Kain","Tyrannos","Weasel"],"metricPhrase":"speed","direction":"Higher","compareAllSharedFields":false}}
            """;

        Assert.True(SparkyQuestionInterpreter.TryParseConversationInterpretation(
            json, Languages, 0, null, out var parsed));
        Assert.Equal(new[] { "Kain", "Tyrannos", "Weasel" }, parsed.Comparison!.SubjectReferences);
    }

    [Theory]
    [InlineData("[\"a\",\"b\",\"c\",\"d\",\"e\",\"f\",\"g\",\"h\",\"i\"]", "\"speed\"", "\"Higher\"", "StructuredRobot")]
    [InlineData("[]", "\"123456789012345678901234567890123456789012345678901234567890123456789012345678901\"", "\"Higher\"", "StructuredRobot")]
    [InlineData("[]", "\"speed\"", "\"Sideways\"", "StructuredRobot")]
    [InlineData("[]", "\"speed\"", "\"Higher\"", "SmallTalk")]
    public void Rejects_out_of_contract_comparison_payloads(
        string subjects, string metricPhrase, string direction, string intent)
    {
        string[] subjectNames = JsonSerializer.Deserialize<string[]>(subjects)!;
        string? parsedMetric = JsonSerializer.Deserialize<string?>(metricPhrase);
        string parsedDirection = JsonSerializer.Deserialize<string>(direction)!;
        string? query = intent == "SmallTalk" ? null : "compare things";
        string json = JsonSerializer.Serialize(new
        {
            languageId = 0,
            intent,
            scope = "Game",
            englishSearchQuery = query,
            requestedAnswerType = (string?)null,
            selectedCandidate = (string?)null,
            followUpAction = "None",
            contextSummary = (string?)null,
            robotFocus = intent == "StructuredRobot" ? "CompareStats" : null,
            catalogFocus = (string?)null,
            smallTalkKind = intent == "SmallTalk" ? "Banter" : null,
            mentionedEntities = Array.Empty<string>(),
            comparison = new
            {
                isComparison = true,
                isContextual = true,
                subjectReferences = subjectNames,
                metricPhrase = parsedMetric,
                direction = parsedDirection,
                compareAllSharedFields = false
            }
        });

        Assert.False(SparkyQuestionInterpreter.TryParseConversationInterpretation(
            json, Languages, 0, null, out _));
    }

    [Fact]
    public void Existing_interpretation_without_comparison_remains_supported()
    {
        const string json = """
            {"languageId":0,"intent":"StructuredItem","scope":"Game","englishSearchQuery":"stats for HCL laser","requestedAnswerType":"Stats","selectedCandidate":null,"followUpAction":"None","contextSummary":null,"robotFocus":null,"catalogFocus":null,"smallTalkKind":null,"mentionedEntities":[]}
            """;

        Assert.True(SparkyQuestionInterpreter.TryParseConversationInterpretation(
            json, Languages, 0, null, out var parsed));
        Assert.Null(parsed.Comparison);
    }
}
