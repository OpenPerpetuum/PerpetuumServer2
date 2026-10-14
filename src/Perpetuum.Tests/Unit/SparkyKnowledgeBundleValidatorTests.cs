using System.Text.Json;
using System.Text.Json.Nodes;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyKnowledgeBundleValidatorTests
{
    [Fact]
    public void SchemaOneWithEmptyRobotSections_RemainsValidForRollback()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(schemaVersion: 1);

        SparkyKnowledgeBundleValidator.Validate(bundle);
    }

    [Fact]
    public void SchemaOneJsonWithoutRobotProperties_DeserializesForRollback()
    {
        using JsonDocument original = JsonDocument.Parse(JsonSerializer.Serialize(CreateBundle(schemaVersion: 1)));
        JsonObject legacyJson = JsonNode.Parse(original.RootElement.GetRawText())!.AsObject();
        legacyJson.Remove("Robots");
        legacyJson.Remove("RobotModules");
        legacyJson.Remove("SkippedRobotCount");

        SparkyKnowledgeBundle bundle = JsonSerializer.Deserialize<SparkyKnowledgeBundle>(legacyJson.ToJsonString())!;

        Assert.Empty(bundle.Robots);
        Assert.Empty(bundle.RobotModules);
        SparkyKnowledgeBundleValidator.Validate(bundle);
    }

    [Fact]
    public void SchemaOneWithRobotData_IsRejected()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(schemaVersion: 1) with
        {
            Robots = [CreateRobot("def_robot", "Sample robot")]
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => SparkyKnowledgeBundleValidator.Validate(bundle));

        Assert.Contains("schema 1", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SchemaTwoWithRobotAndModuleData_IsValid()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(schemaVersion: 2) with
        {
            Robots = [CreateRobot("def_robot", "Sample robot")],
            RobotModules = [CreateModule("def_module", "Sample module")]
        };

        SparkyKnowledgeBundleValidator.Validate(bundle);
    }

    [Fact]
    public void SchemaThreePersistsLaserModuleCategory()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(schemaVersion: 3) with
        {
            RobotModules = [CreateModule("def_laser", "Sample laser") with { IsLaser = true }]
        };

        SparkyKnowledgeBundleValidator.Validate(bundle);
        SparkyKnowledgeBundle roundTripped = JsonSerializer.Deserialize<SparkyKnowledgeBundle>(
            JsonSerializer.Serialize(bundle))!;

        Assert.Equal(true, roundTripped.RobotModules[0].IsLaser);
        SparkyKnowledgeBundleValidator.Validate(roundTripped);
    }

    [Fact]
    public void SchemaThreeRequiresExplicitModuleCategory()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(schemaVersion: 3) with
        {
            RobotModules = [CreateModule("def_module", "Sample module")]
        };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void SchemaTwoWithDanglingCompatibilityReference_IsRejected()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(schemaVersion: 2) with
        {
            Robots = [CreateRobot("def_robot", "Sample robot", compatibilityModule: "def_missing_module")]
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => SparkyKnowledgeBundleValidator.Validate(bundle));

        Assert.Contains("compatibility", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RobotAndModuleAliasCollision_MustBePersisted()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(schemaVersion: 2) with
        {
            Robots = [CreateRobot("def_robot", "Shared name")],
            RobotModules = [CreateModule("def_module", "Shared name")]
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => SparkyKnowledgeBundleValidator.Validate(bundle));

        Assert.Contains("alias", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DuplicateRobotDefinition_IsRejected()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(schemaVersion: 2) with
        {
            Robots = [CreateRobot("def_robot", "Sample robot"), CreateRobot("DEF_ROBOT", "Other robot")]
        };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void MissingLocalizedRobotLanguageView_IsRejected()
    {
        SparkyRobotRecord robot = CreateRobot("def_robot", "Sample robot") with
        {
            LocalizedViews = new Dictionary<int, SparkyLocalizedRobotView>()
        };
        SparkyKnowledgeBundle bundle = CreateBundle(schemaVersion: 2) with { Robots = [robot] };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void NonFiniteAssembledStat_IsRejected()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(schemaVersion: 2) with
        {
            Robots = [CreateRobot("def_robot", "Sample robot", assembledStatValue: "NaN")]
        };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void DuplicateCompatibilitySlots_AreRejected()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(schemaVersion: 2) with
        {
            Robots = [CreateRobot("def_robot", "Sample robot", "def_module", [1, 1])],
            RobotModules = [CreateModule("def_module", "Sample module")]
        };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void RawDefinitionKeyInLocalizedRobotName_IsRejected()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(schemaVersion: 2) with
        {
            Robots = [CreateRobot("def_robot", "def_robot")]
        };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void GameContentRevision_ChangesWhenRobotKnowledgeChanges()
    {
        var export = new ExportDocument();
        var emptyRobotExport = new SparkyRobotKnowledgeExport([], [], 0, []);
        var populatedRobotExport = emptyRobotExport with
        {
            Robots = [CreateRobot("def_robot", "Sample robot")]
        };

        string before = Program.ComputeGameContentRevision(export, emptyRobotExport);
        string after = Program.ComputeGameContentRevision(export, populatedRobotExport);

        Assert.NotEqual(before, after);
    }

    private static SparkyKnowledgeBundle CreateBundle(int schemaVersion)
    {
        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        string dictionaryHash = SparkyDictionaryCatalog.ComputeDictionaryHash(translations);
        var language = new SparkySupportedLanguage(0, "English", dictionaryHash, translations);
        var manifest = new SparkyBundleManifest(
            "bundle-version",
            new string('B', 64),
            DateTimeOffset.UtcNow,
            new Dictionary<int, string> { [0] = dictionaryHash },
            new Dictionary<string, string> { ["fixture-source"] = new string('A', 40) });
        return new SparkyKnowledgeBundle(
            schemaVersion,
            manifest,
            [language],
            [],
            [],
            [],
            new SparkySearchIndex(
                new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal),
                []));
    }

    private static SparkyRobotRecord CreateRobot(
        string definitionName,
        string displayName,
        string? compatibilityModule = null,
        IReadOnlyList<int>? eligibleSlots = null,
        string? assembledStatValue = null)
    {
        var component = new SparkyLocalizedRobotComponentView(
            "def_robot_head",
            "Robot head",
            [],
            1);
        var localizedView = new SparkyLocalizedRobotView(
            displayName,
            assembledStatValue is null
                ? []
                : [new AnswerStat { FieldId = 10, Label = "Mass", Value = assembledStatValue, Unit = "kg" }],
            [component],
            []);
        IReadOnlyList<SparkyRobotCompatibility> compatibility = compatibilityModule is null
            ? []
            : [new SparkyRobotCompatibility("def_robot_head", compatibilityModule, eligibleSlots ?? [1])];
        return new SparkyRobotRecord(
            definitionName,
            ["def_robot_head"],
            compatibility,
            [],
            new Dictionary<int, SparkyLocalizedRobotView> { [0] = localizedView });
    }

    private static SparkyRobotModuleRecord CreateModule(string definitionName, string displayName)
    {
        return new SparkyRobotModuleRecord(
            definitionName,
            new Dictionary<int, SparkyLocalizedRobotModuleView>
            {
                [0] = new SparkyLocalizedRobotModuleView(displayName, [], false)
            });
    }
}
