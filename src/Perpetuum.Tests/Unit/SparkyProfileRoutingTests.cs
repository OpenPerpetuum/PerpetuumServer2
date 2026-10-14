using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyProfileRoutingTests
{
    [Fact]
    public void Applies_the_highest_priority_literal_alias_for_the_detected_language()
    {
        var interpretation = new SparkyQuestionInterpretation(0, SparkyIntent.Unsupported, null);
        SparkyRouteAlias[] aliases =
        [
            new("full recipe", SparkyIntent.StructuredItem, Priority: 10, LanguageId: 0),
            new("recipe", SparkyIntent.MechanicsLore, Priority: 1, LanguageId: 0),
            new("recipe", SparkyIntent.SmallTalk, Priority: 100, LanguageId: 1)
        ];

        var result = SparkyQuestionInterpreter.ApplyRouteAliases("Could you show the FULL recipe?", interpretation, aliases);

        Assert.Equal(SparkyIntent.StructuredItem, result.Intent);
    }

    [Fact]
    public void Exclusion_phrases_prevent_an_alias_from_overriding_the_interpreter()
    {
        var interpretation = new SparkyQuestionInterpretation(0, SparkyIntent.MechanicsLore, "tell me about the recipe");
        SparkyRouteAlias[] aliases =
        [
            new("recipe", SparkyIntent.StructuredItem, Priority: 5, LanguageId: 0, ExcludedPhrases: ["recipe lore"])
        ];

        var result = SparkyQuestionInterpreter.ApplyRouteAliases("Tell me about recipe lore", interpretation, aliases);

        Assert.Equal(SparkyIntent.MechanicsLore, result.Intent);
    }

    [Fact]
    public void Alias_matching_uses_whole_words_and_can_fill_specialized_focus_defaults()
    {
        var interpretation = new SparkyQuestionInterpretation(0, SparkyIntent.Unsupported, null);
        SparkyRouteAlias[] aliases = [new("robot", SparkyIntent.StructuredRobot)];

        var exact = SparkyQuestionInterpreter.ApplyRouteAliases("robot stats", interpretation, aliases);
        var embedded = SparkyQuestionInterpreter.ApplyRouteAliases("robotics stats", interpretation, aliases);

        Assert.Equal(SparkyIntent.StructuredRobot, exact.Intent);
        Assert.Equal(SparkyRobotQuestionFocus.General, exact.RobotFocus);
        Assert.Equal(SparkyIntent.Unsupported, embedded.Intent);
    }
}
