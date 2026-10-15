using Perpetuum.ExportedTypes;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyComparisonResolverTests
{
    private static readonly SparkyKnowledgeBundle Bundle = CreateBundle();

    [Fact]
    public void Robot_speed_comparison_uses_assembled_verified_values_and_calculates_difference()
    {
        SparkyComparisonResolution result = new SparkyRobotComparisonResolver().Resolve(
            new SparkyComparisonRequest("Robot", "def_kain", "def_tyrannos", "maximum speed",
                SparkyComparisonDirection.Higher, false), Bundle, 0);

        Assert.Equal("resolved", result.Status);
        Assert.Equal(new[] { "Kain", "Tyrannos" }, result.LocalizedSubjectNames);
        Assert.Equal("Maximum speed", result.AttributeLabel);
        Assert.Equal(new[] { "103.32", "93.6" }, result.Values);
        Assert.Equal("kph", result.Unit);
        Assert.Equal("9.72", result.Difference);
        Assert.Equal("Kain", result.LeadingSubjectName);
    }

    [Fact]
    public void Item_comparison_selects_the_requested_shared_field()
    {
        SparkyComparisonResolution result = new SparkyItemComparisonResolver().Resolve(
            new SparkyComparisonRequest("Item", "def_hcl_laser", "def_missile_launcher", "CPU usage",
                SparkyComparisonDirection.Higher, false), Bundle, 0);

        Assert.Equal("resolved", result.Status);
        Assert.Equal("CPU usage", result.AttributeLabel);
        Assert.Equal(new[] { "30", "40" }, result.Values);
        Assert.Equal("TF", result.Unit);
        Assert.Equal("10", result.Difference);
        Assert.Equal("Standard Medium Missile Launcher", result.LeadingSubjectName);
    }

    [Fact]
    public void Broad_item_comparison_returns_only_common_numeric_fields()
    {
        SparkyComparisonResolution result = new SparkyItemComparisonResolver().Resolve(
            new SparkyComparisonRequest("Item", "def_hcl_laser", "def_missile_launcher", null,
                SparkyComparisonDirection.Unspecified, true), Bundle, 0);

        Assert.Equal("resolved", result.Status);
        Assert.Equal(new[] { "Accuracy", "CPU usage" }, result.Facts.Select(fact => fact.AttributeLabel));
        Assert.All(result.Facts, fact => Assert.Equal(2, fact.Values.Count));
        Assert.Null(result.LeadingSubjectName);
    }

    [Fact]
    public void Missing_or_ambiguous_metric_does_not_guess()
    {
        SparkyComparisonResolution missing = new SparkyItemComparisonResolver().Resolve(
            new SparkyComparisonRequest("Item", "def_hcl_laser", "def_missile_launcher", "shield amount",
                SparkyComparisonDirection.Higher, false), Bundle, 0);
        SparkyComparisonResolution ambiguous = new SparkyItemComparisonResolver().Resolve(
            new SparkyComparisonRequest("Item", "def_hcl_laser", "def_missile_launcher", "usage",
                SparkyComparisonDirection.Higher, false), Bundle, 0);

        Assert.Equal("unavailable", missing.Status);
        Assert.Equal("attribute_unavailable", missing.DiagnosticCategory);
        Assert.Equal("ambiguous_attribute", ambiguous.Status);
    }

    [Fact]
    public void Unit_mismatch_is_excluded_from_shared_comparisons()
    {
        SparkyKnowledgeBundle mismatched = Bundle with
        {
            Items = Bundle.Items.Select(item => item.DefinitionName == "def_missile_launcher"
                ? WithStats(item, item.LocalizedViews[0].AnswerView.Stats.Select(stat =>
                    new AnswerStat
                    {
                        FieldId = stat.FieldId,
                        Label = stat.Label,
                        Value = stat.Value,
                        Unit = stat.FieldId == 1 ? "m" : stat.Unit
                    }).ToArray())
                : item).ToArray()
        };

        SparkyComparisonResolution result = new SparkyItemComparisonResolver().Resolve(
            new SparkyComparisonRequest("Item", "def_hcl_laser", "def_missile_launcher", null,
                SparkyComparisonDirection.Unspecified, true), mismatched, 0);

        Assert.DoesNotContain(result.Facts, fact => fact.AttributeLabel == "Accuracy");
        Assert.Contains(result.Facts, fact => fact.AttributeLabel == "CPU usage");
    }

    [Fact]
    public void Coordinator_does_not_dispatch_an_unsupported_domain()
    {
        SparkyComparisonResolution result = SparkyComparisonCoordinator.Resolve(
            new SparkyComparisonRequest("Lore", "a", "b", "age", SparkyComparisonDirection.Higher, false),
            Bundle, 0, [new SparkyItemComparisonResolver(), new SparkyRobotComparisonResolver()]);

        Assert.Equal("unsupported_domain", result.Status);
    }

    private static WeaponItem WithStats(WeaponItem item, AnswerStat[] stats)
    {
        SparkyLocalizedItemView source = item.LocalizedViews[0];
        var localized = new SparkyLocalizedItemView
        {
            DisplayName = source.DisplayName,
            HasLocalizedDisplayName = source.HasLocalizedDisplayName,
            AnswerView = new ItemAnswerView { Stats = [..stats] }
        };
        return new WeaponItem
        {
            DefinitionName = item.DefinitionName,
            DisplayName = item.DisplayName,
            AnswerView = new ItemAnswerView { Stats = [..stats] },
            LocalizedViews = new Dictionary<int, SparkyLocalizedItemView> { [0] = localized }
        };
    }

    private static SparkyKnowledgeBundle CreateBundle()
    {
        var item1Stats = new List<AnswerStat>
        {
            new() { FieldId = 1, Label = "Accuracy", Value = "12", Unit = "points" },
            new() { FieldId = 2, Label = "CPU usage", Value = "30", Unit = "TF" },
            new() { FieldId = 3, Label = "Core usage", Value = "20", Unit = "AP" }
        };
        var item2Stats = new List<AnswerStat>
        {
            new() { FieldId = 1, Label = "Accuracy", Value = "10", Unit = "points" },
            new() { FieldId = 2, Label = "CPU usage", Value = "40", Unit = "TF" },
            new() { FieldId = 4, Label = "Range", Value = "50", Unit = "m" }
        };
        var languages = new[]
        {
            new SparkySupportedLanguage(0, "English", "hash", new Dictionary<string, string>())
        };
        var robots = new[]
        {
            Robot("def_kain", "Kain", "103.32"),
            Robot("def_tyrannos", "Tyrannos", "93.6")
        };
        var items = new[]
        {
            Item("def_hcl_laser", "Standard Medium HCL Laser", item1Stats),
            Item("def_missile_launcher", "Standard Medium Missile Launcher", item2Stats)
        };
        return new SparkyKnowledgeBundle(3,
            new SparkyBundleManifest("test", new string('A', 64), DateTimeOffset.UtcNow,
                new Dictionary<int, string> { [0] = "hash" }, new Dictionary<string, string> { ["test"] = "hash" }),
            languages, items, [], [], new SparkySearchIndex(new Dictionary<string, IReadOnlyList<int>>(), []))
        {
            Robots = robots
        };
    }

    private static WeaponItem Item(string definition, string name, List<AnswerStat> stats) => new()
    {
        DefinitionName = definition,
        DisplayName = name,
        AnswerView = new ItemAnswerView { Stats = stats },
        LocalizedViews = new Dictionary<int, SparkyLocalizedItemView>
        {
            [0] = new() { DisplayName = name, HasLocalizedDisplayName = true, AnswerView = new ItemAnswerView { Stats = stats } }
        }
    };

    private static SparkyRobotRecord Robot(string definition, string name, string speed) => new(
        definition, [], [], [],
        new Dictionary<int, SparkyLocalizedRobotView>
        {
            [0] = new(name,
                [new AnswerStat { FieldId = (int)AggregateField.speed_max, Label = "Maximum speed", Value = speed, Unit = "kph" }], [], [])
        });
}
