using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyComparisonSubjectResolverTests
{
    private static readonly SparkyKnowledgeBundle Bundle = CreateBundle();

    [Fact]
    public void Contextual_comparison_resolves_the_two_recent_user_discussed_robots()
    {
        SparkyComparisonIntent intent = Intent(contextual: true);
        SparkyConversationTurn[] history =
        [
            new("user", "What are stats for Kain?"),
            new("assistant", "Kain has a maximum speed of 103.32 kph."),
            new("user", "And Tyrannos?"),
            new("assistant", "Tyrannos has a maximum speed of 93.6 kph.")
        ];

        SparkyComparisonSubjectResolution result = SparkyComparisonSubjectResolver.Resolve(
            intent, "Which one is faster?", "Which robot has the higher maximum speed?", history, Bundle, 0);

        Assert.Equal("resolved", result.Status);
        Assert.Equal("Robot", result.Domain);
        Assert.Equal(new[] { "def_kain", "def_tyrannos" },
            result.Subjects.Select(subject => subject.DefinitionName));
        Assert.All(result.Subjects, subject => Assert.Equal("conversation_context", subject.ReferenceSource));
    }

    [Fact]
    public void Explicit_item_pair_is_resolved_from_the_active_catalog()
    {
        SparkyComparisonSubjectResolution result = SparkyComparisonSubjectResolver.Resolve(
            Intent(contextual: false, "Standard small armor plate", "Standard medium armor plate"),
            "Compare Standard small armor plate with Standard medium armor plate",
            "compare the armor values of the two plates",
            [], Bundle, 0);

        Assert.Equal("resolved", result.Status);
        Assert.Equal("Item", result.Domain);
        Assert.Equal(new[] { "def_small_plate", "def_medium_plate" },
            result.Subjects.Select(subject => subject.DefinitionName));
    }

    [Fact]
    public void Mixed_current_and_context_subject_references_retain_their_resolution_origin()
    {
        SparkyComparisonSubjectResolution result = SparkyComparisonSubjectResolver.Resolve(
            Intent(contextual: true, "Kain", "Tyrannos"),
            "How does Kain compare to that one?",
            "compare Kain with Tyrannos",
            [new SparkyConversationTurn("user", "And Tyrannos?")],
            Bundle,
            0);

        Assert.Equal("resolved", result.Status);
        Assert.Equal(new[] { "current_question", "conversation_context" },
            result.Subjects.Select(subject => subject.ReferenceSource));
    }

    [Fact]
    public void More_than_two_recent_entities_are_ambiguous()
    {
        SparkyConversationTurn[] history =
        [
            new("user", "Tell me about Kain."),
            new("user", "Tell me about Tyrannos."),
            new("user", "Tell me about Weasel.")
        ];

        SparkyComparisonSubjectResolution result = SparkyComparisonSubjectResolver.Resolve(
            Intent(contextual: true), "Which one is faster?", null, history, Bundle, 0);

        Assert.Equal("ambiguous", result.Status);
        Assert.Equal(3, result.Subjects.Count);
        Assert.Equal(3, result.CandidateNames.Count);
    }

    [Fact]
    public void Non_contextual_comparison_does_not_reuse_old_subjects()
    {
        SparkyConversationTurn[] history =
        [new("user", "What are stats for Kain?"), new("user", "And Tyrannos?")];

        SparkyComparisonSubjectResolution result = SparkyComparisonSubjectResolver.Resolve(
            Intent(contextual: false), "Which one is faster?", null, history, Bundle, 0);

        Assert.Equal("no_subjects", result.Status);
        Assert.Empty(result.Subjects);
    }

    [Fact]
    public void Cross_domain_pair_is_not_resolved()
    {
        SparkyComparisonSubjectResolution result = SparkyComparisonSubjectResolver.Resolve(
            Intent(contextual: false, "Kain", "Standard medium armor plate"),
            "Compare Kain and Standard medium armor plate",
            null, [], Bundle, 0);

        Assert.Equal("cross_domain", result.Status);
        Assert.Equal(2, result.Subjects.Count);
    }

    private static SparkyComparisonIntent Intent(bool contextual, params string[] subjects) => new(
        true, contextual, subjects, "maximum speed", SparkyComparisonDirection.Higher, false);

    private static SparkyKnowledgeBundle CreateBundle()
    {
        var languages = new[]
        {
            new SparkySupportedLanguage(0, "English", "hash", new Dictionary<string, string>())
        };
        var robots = new[]
        {
            Robot("def_kain", "Kain", "103.32"),
            Robot("def_tyrannos", "Tyrannos", "93.6"),
            Robot("def_weasel", "Weasel", "110")
        };
        var items = new[]
        {
            Item("def_small_plate", "Standard small armor plate"),
            Item("def_medium_plate", "Standard medium armor plate")
        };
        return new SparkyKnowledgeBundle(
            3,
            new SparkyBundleManifest("test", new string('A', 64), DateTimeOffset.UtcNow,
                new Dictionary<int, string> { [0] = "hash" }, new Dictionary<string, string> { ["test"] = "hash" }),
            languages,
            items,
            [], [],
            new SparkySearchIndex(new Dictionary<string, IReadOnlyList<int>>(), []))
        {
            Robots = robots
        };
    }

    private static SparkyRobotRecord Robot(string definition, string displayName, string speed) => new(
        definition,
        [], [], [],
        new Dictionary<int, SparkyLocalizedRobotView>
        {
            [0] = new(displayName,
                [new AnswerStat { FieldId = 303, Label = "Maximum speed", Value = speed, Unit = "kph" }], [], [])
        });

    private static WeaponItem Item(string definition, string displayName)
    {
        var stats = new List<AnswerStat>
        {
            new() { FieldId = 10, Label = "Armor max", Value = "100", Unit = "HP" }
        };
        return new WeaponItem
        {
            DefinitionName = definition,
            DisplayName = displayName,
            AnswerView = new ItemAnswerView { Stats = stats },
            LocalizedViews = new Dictionary<int, SparkyLocalizedItemView>
            {
                [0] = new()
                {
                    DisplayName = displayName,
                    HasLocalizedDisplayName = true,
                    AnswerView = new ItemAnswerView { Stats = stats }
                }
            }
        };
    }
}
