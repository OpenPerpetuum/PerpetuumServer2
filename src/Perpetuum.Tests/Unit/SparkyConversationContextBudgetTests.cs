using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyConversationContextBudgetTests
{
    [Fact]
    public void Applies_turn_and_character_limits_while_preserving_recent_complete_exchanges()
    {
        var turns = new[]
        {
            new SparkyConversationTurn("user", "old question"),
            new SparkyConversationTurn("assistant", "old answer"),
            new SparkyConversationTurn("user", "new question"),
            new SparkyConversationTurn("assistant", "new answer")
        };

        var bounded = SparkyConversationContextBudget.Apply(turns, "old summary", null, 2, 33);

        Assert.Equal(["new question", "new answer"], bounded.Turns.Select(turn => turn.Content));
        Assert.Equal("old summary", bounded.Summary);
        Assert.True(bounded.Turns.Sum(turn => turn.Content.Length) + bounded.Summary!.Length <= 33);
    }

    [Fact]
    public void Caps_summary_budget_and_does_not_keep_a_partial_pair()
    {
        var turns = new[]
        {
            new SparkyConversationTurn("user", new string('u', 100)),
            new SparkyConversationTurn("assistant", new string('a', 100))
        };

        var bounded = SparkyConversationContextBudget.Apply(turns, new string('s', 700), null, 8, 256);

        Assert.Empty(bounded.Turns);
        Assert.Equal(64, bounded.Summary!.Length);
    }
}
