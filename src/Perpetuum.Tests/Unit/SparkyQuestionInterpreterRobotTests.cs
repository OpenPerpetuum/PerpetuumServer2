using System.Reflection;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyQuestionInterpreterRobotTests
{
    private static readonly MethodInfo ParseMethod = typeof(SparkyQuestionInterpreter).GetMethod(
        "TryParseInterpretation",
        BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void StructuredRobotIntentRequiresAndPreservesTypedFocus()
    {
        object?[] arguments =
        [
            "{\"languageId\":0,\"intent\":\"StructuredRobot\",\"robotFocus\":\"Components\",\"englishSearchQuery\":\"robot components\",\"smallTalkKind\":null,\"mentionedEntities\":[\"Archer\"]}",
            new[] { new SparkySupportedLanguage(0, "English", "hash", new Dictionary<string, string>()) },
            0,
            null
        ];

        bool parsed = (bool)ParseMethod.Invoke(null, arguments)!;

        Assert.True(parsed);
        object interpretation = Assert.IsAssignableFrom<object>(arguments[3]);
        Assert.Equal("StructuredRobot", interpretation.GetType().GetProperty("Intent")!.GetValue(interpretation)!.ToString());
        Assert.Equal("Components", interpretation.GetType().GetProperty("RobotFocus")!.GetValue(interpretation)!.ToString());
    }

    [Fact]
    public void StructuredModuleCatalogIntentRequiresAndPreservesCategory()
    {
        object?[] arguments =
        [
            "{\"languageId\":0,\"intent\":\"StructuredModuleCatalog\",\"catalogFocus\":\"Lasers\",\"englishSearchQuery\":\"list laser modules\",\"robotFocus\":null,\"smallTalkKind\":null,\"mentionedEntities\":[]}",
            new[] { new SparkySupportedLanguage(0, "English", "hash", new Dictionary<string, string>()) },
            0,
            null
        ];

        bool parsed = (bool)ParseMethod.Invoke(null, arguments)!;

        Assert.True(parsed);
        object interpretation = Assert.IsAssignableFrom<object>(arguments[3]);
        Assert.Equal("StructuredModuleCatalog", interpretation.GetType().GetProperty("Intent")!.GetValue(interpretation)!.ToString());
        Assert.Equal("Lasers", interpretation.GetType().GetProperty("CatalogFocus")!.GetValue(interpretation)!.ToString());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"NotAFocus\"")]
    [InlineData("\"0\"")]
    public void StructuredRobotWithMissingOrInvalidFocusIsUnsupported(string focusJson)
    {
        string focusProperty = focusJson == "null" ? string.Empty : $"\"robotFocus\":{focusJson},";
        object?[] arguments =
        [
            $"{{\"languageId\":0,\"intent\":\"StructuredRobot\",{focusProperty}\"englishSearchQuery\":\"robot details\",\"smallTalkKind\":null,\"mentionedEntities\":[]}}",
            new[] { new SparkySupportedLanguage(0, "English", "hash", new Dictionary<string, string>()) },
            0,
            null
        ];

        bool parsed = (bool)ParseMethod.Invoke(null, arguments)!;

        Assert.False(parsed);
    }
}
