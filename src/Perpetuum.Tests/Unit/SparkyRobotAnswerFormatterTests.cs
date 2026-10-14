using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyRobotAnswerFormatterTests
{
    [Theory]
    [InlineData(0, "I can't match that module name", "Did you mean one of these")]
    [InlineData(2, "Modulnamen", "Meintest du vielleicht")]
    [InlineData(3, "Não encontrei", "Você quis dizer")]
    [InlineData(4, "Не нашёл модуль", "возможно, вы имели в виду")]
    public void UnknownModuleResponseIsLocalizedAndShowsExactCandidates(
        int languageId,
        string expectedOpening,
        string expectedQuestion)
    {
        string answer = SparkyRobotAnswerFormatter.FormatUnknownModuleCandidates(
            languageId,
            ["Standard large armor plate", "Standard medium armor plate"],
            2000);

        Assert.Contains(expectedOpening, answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedQuestion, answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Standard large armor plate", answer, StringComparison.Ordinal);
        Assert.Contains("Standard medium armor plate", answer, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "static slot eligibility only", "current free slots")]
    [InlineData(2, "Slot-Eignung", "freie Slots")]
    [InlineData(3, "elegibilidade dos slots", "slots livres")]
    [InlineData(4, "допустимость", "свободные слоты")]
    public void CompatibilityAnswerAlwaysIncludesLocalizedFitBoundary(int languageId, string expectedText, string capacityText)
    {
        string answer = SparkyRobotAnswerFormatter.AppendCompatibilityScope(
            "Good module is eligible for the robot's head slots.",
            languageId,
            2000);

        Assert.Contains(expectedText, answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(capacityText, answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompatibilityBoundaryKeepsAnswerWithinConfiguredLimit()
    {
        string answer = SparkyRobotAnswerFormatter.AppendCompatibilityScope(
            new string('x', 2000),
            0,
            2000);

        Assert.True(answer.Length <= 2000);
        Assert.Contains("current free slots", answer, StringComparison.OrdinalIgnoreCase);
    }
}
