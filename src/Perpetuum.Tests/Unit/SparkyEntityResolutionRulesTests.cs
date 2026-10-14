using System.Text.Json;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyEntityResolutionRulesTests
{
    [Fact]
    public void LoadsEveryReviewedItemAndRobotTierMapping()
    {
        SparkyEntityResolutionRules rules = SparkyEntityResolutionRules.Load(TestRepositoryRoot);

        var actual = rules.TierRules
            .Select(rule => (rule.EntityKind, rule.TierType, rule.TierLevel, rule.CanonicalLabel))
            .OrderBy(rule => rule.EntityKind)
            .ThenBy(rule => rule.TierType)
            .ThenBy(rule => rule.TierLevel)
            .ToArray();

        (SparkyEntityKind EntityKind, int TierType, int? TierLevel, string CanonicalLabel)[] expected =
        [
            (SparkyEntityKind.Item, 1, 0, "T0"),
            (SparkyEntityKind.Item, 1, 1, "T1"),
            (SparkyEntityKind.Item, 1, 2, "T2"),
            (SparkyEntityKind.Item, 1, 3, "T3"),
            (SparkyEntityKind.Item, 1, 4, "T4"),
            (SparkyEntityKind.Item, 1, 5, "T5"),
            (SparkyEntityKind.Item, 2, 1, "T1P"),
            (SparkyEntityKind.Item, 2, 2, "T2P"),
            (SparkyEntityKind.Item, 2, 3, "T3P"),
            (SparkyEntityKind.Item, 2, 4, "T4P"),
            (SparkyEntityKind.Item, 3, 0, "S"),
            (SparkyEntityKind.Item, 3, 2, "T2+"),
            (SparkyEntityKind.Item, 3, 3, "T3-"),
            (SparkyEntityKind.Item, 3, 4, "T4+"),
            (SparkyEntityKind.Robot, 1, 1, "Mk1"),
            (SparkyEntityKind.Robot, 1, 2, "Mk2"),
            (SparkyEntityKind.Robot, 1, 3, "Mk3"),
            (SparkyEntityKind.Robot, 2, null, "P")
        ];

        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], actual[index]);
        }
    }

    [Fact]
    public void LoadsAliasesByLanguageAndCalibrationTemplateCategory()
    {
        SparkyEntityResolutionRules rules = SparkyEntityResolutionRules.Load(TestRepositoryRoot);
        SparkyEntityTierRule t2Plus = Assert.Single(rules.TierRules, rule => rule.CanonicalLabel == "T2+");

        Assert.Contains("tier 2 plus", t2Plus.AliasesByLanguage[0]);
        Assert.Contains("Niani item", Assert.Single(rules.TierRules, rule => rule.CanonicalLabel == "T3-").AliasesByLanguage[0]);
        Assert.Equal("_cprg", rules.CalibrationTemplate.Suffix);
        Assert.Contains("CT", rules.CalibrationTemplate.AliasesByLanguage[0]);
        Assert.Contains("blueprint", rules.CalibrationTemplate.AliasesByLanguage[0]);
    }

    [Fact]
    public void RejectsAliasThatMapsToDifferentPairsWithinOneEntityKind()
    {
        string root = CreateTemporaryRepository(CreateRulesJson(
        [
            Rule("item", 1, 1, "T1", "shared"),
            Rule("item", 1, 2, "T2", "SHARED")
        ]));

        try
        {
            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => SparkyEntityResolutionRules.Load(root));

            Assert.Contains("alias", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RejectsUnknownEntityKind()
    {
        string root = CreateTemporaryRepository(CreateRulesJson([Rule("creature", 1, 1, "T1", "T1")]));

        try
        {
            Assert.Throws<InvalidDataException>(() => SparkyEntityResolutionRules.Load(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RejectsMalformedCalibrationSuffix()
    {
        string root = CreateTemporaryRepository(CreateRulesJson([], suffix: "cprg"));

        try
        {
            Assert.Throws<InvalidDataException>(() => SparkyEntityResolutionRules.Load(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RejectsUnsupportedCatalogVersion()
    {
        string root = CreateTemporaryRepository(CreateRulesJson([], version: 2));

        try
        {
            Assert.Throws<InvalidDataException>(() => SparkyEntityResolutionRules.Load(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string TestRepositoryRoot
    {
        get
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);
            while (current is not null && !File.Exists(Path.Combine(current.FullName, "tools", "sparky-knowledge-sources.json")))
            {
                current = current.Parent;
            }

            return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
        }
    }

    private static string CreateTemporaryRepository(string json)
    {
        string root = Path.Combine(Path.GetTempPath(), "sparky-entity-rules-" + Guid.NewGuid().ToString("N"));
        string tools = Path.Combine(root, "tools");
        Directory.CreateDirectory(tools);
        File.WriteAllText(Path.Combine(tools, "sparky-entity-resolution-rules.json"), json);
        return root;
    }

    private static string CreateRulesJson(
        TierRuleFixture[] tierRules,
        int version = 1,
        string suffix = "_cprg") => JsonSerializer.Serialize(new
        {
            version,
            tierRules,
            calibrationTemplate = new
            {
                suffix,
                aliasesByLanguage = new Dictionary<string, string[]> { ["0"] = ["CT", "blueprint"] }
            }
        });

    private sealed record TierRuleFixture(
        string entityKind,
        int tierType,
        int? tierLevel,
        string canonicalLabel,
        Dictionary<int, string[]> aliasesByLanguage);

    private static TierRuleFixture Rule(string entityKind, int tierType, int? tierLevel, string canonicalLabel, params string[] aliases) =>
        new(entityKind, tierType, tierLevel, canonicalLabel,
            new Dictionary<int, string[]> { [0] = aliases.Append(canonicalLabel).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() });
}
