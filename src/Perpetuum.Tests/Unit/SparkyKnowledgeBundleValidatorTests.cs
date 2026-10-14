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
    public void SchemaFourRoundTripPreservesNullableAndUnmappedTierValues()
    {
        SparkyKnowledgeBundle bundle = CreateSchemaFourBundle() with
        {
            Items = [CreateItem(new SparkyEntityTierMetadata { TierType = 99, TierLevel = 99 })],
            Robots = [CreateRobot("def_robot", "Sample robot") with
            {
                TierMetadata = new SparkyEntityTierMetadata { TierType = 2, TierLevel = null }
            }]
        };

        SparkyKnowledgeBundleValidator.Validate(bundle);
        SparkyKnowledgeBundle roundTripped = JsonSerializer.Deserialize<SparkyKnowledgeBundle>(
            JsonSerializer.Serialize(bundle))!;

        Assert.Equal(99, roundTripped.Items[0].TierMetadata!.TierType);
        Assert.Equal(99, roundTripped.Items[0].TierMetadata!.TierLevel);
        Assert.Equal(2, roundTripped.Robots[0].TierMetadata!.TierType);
        Assert.Null(roundTripped.Robots[0].TierMetadata!.TierLevel);
        Assert.Null(roundTripped.EntityResolutionRules!.FindTierRule(SparkyEntityKind.Item, 99, 99));
        SparkyKnowledgeBundleValidator.Validate(roundTripped);
    }

    [Fact]
    public void SchemaFourRequiresEntityResolutionRules()
    {
        SparkyKnowledgeBundle bundle = CreateSchemaFourBundle() with { EntityResolutionRules = null };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void SchemaFourRequiresTierMetadataOnItems()
    {
        SparkyKnowledgeBundle bundle = CreateSchemaFourBundle() with { Items = [CreateItem(null)] };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void SchemaFourRequiresTierMetadataOnRecipes()
    {
        SparkyKnowledgeBundle bundle = CreateSchemaFourBundle() with { ProductionRecipes = [CreateRecipe(null)] };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void SchemaFourRejectsDuplicateRecipeDefinitionNames()
    {
        SparkyKnowledgeBundle bundle = CreateSchemaFourBundle();
        bundle = bundle with
        {
            ProductionRecipes =
            [
                CreateRecipe(new SparkyEntityTierMetadata { TierType = 1, TierLevel = 1 }),
                CreateRecipe(new SparkyEntityTierMetadata { TierType = 1, TierLevel = 1 }, "DEF_SAMPLE_RECIPE")
            ]
        };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void SchemaFourRequiresTierMetadataOnRobots()
    {
        SparkyKnowledgeBundle bundle = CreateSchemaFourBundle() with
        {
            Robots = [CreateRobot("def_robot", "Sample robot")]
        };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void SchemaFourRequiresTierMetadataOnRobotModules()
    {
        SparkyKnowledgeBundle bundle = CreateSchemaFourBundle() with
        {
            RobotModules = [CreateModule("def_module", "Sample module")]
        };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void SchemaFourRejectsAliasesThatMapToDifferentPairs()
    {
        SparkyKnowledgeBundle bundle = CreateSchemaFourBundle();
        SparkyEntityResolutionRules rules = bundle.EntityResolutionRules!;
        SparkyEntityTierRule t1 = Assert.Single(rules.TierRules, rule => rule.CanonicalLabel == "T1");
        var t1Aliases = new Dictionary<int, IReadOnlyList<string>>(t1.AliasesByLanguage)
        {
            [0] = [.. t1.AliasesByLanguage[0], "damaged"]
        };
        bundle = bundle with
        {
            EntityResolutionRules = rules with
            {
                TierRules = rules.TierRules.Select(rule => rule == t1
                    ? rule with { AliasesByLanguage = t1Aliases }
                    : rule).ToArray()
            }
        };

        Assert.Throws<InvalidDataException>(() => SparkyKnowledgeBundleValidator.Validate(bundle));
    }

    [Fact]
    public void SchemaFourJsonWithOmittedNullableTierPropertyFailsAsInvalidBundle()
    {
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(CreateSchemaFourBundle()))!.AsObject();
        json["Items"]!.AsArray()[0]!["TierMetadata"]!.AsObject().Remove("TierLevel");
        string path = Path.Combine(Path.GetTempPath(), "sparky-schema4-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            File.WriteAllText(path, json.ToJsonString());

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Program.ReadKnowledgeBundle(path));

            Assert.Contains("tier", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyFlatExportLoadsAsSchemaThree()
    {
        string path = Path.Combine(Path.GetTempPath(), "sparky-legacy-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new ExportDocument()));

            SparkyKnowledgeBundle bundle = Program.ReadKnowledgeBundleOrLegacy(path);

            Assert.Equal(3, bundle.SchemaVersion);
            Assert.Null(bundle.EntityResolutionRules);
        }
        finally
        {
            File.Delete(path);
        }
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

    private static SparkyKnowledgeBundle CreateSchemaFourBundle()
    {
        SparkyKnowledgeBundle bundle = CreateBundle(4);
        var sourceHashes = new Dictionary<string, string>(bundle.Manifest.SourceHashes, StringComparer.Ordinal)
        {
            ["sparkyEntityResolutionRules"] = new string('C', 64)
        };
        return bundle with
        {
            Manifest = bundle.Manifest with { SourceHashes = sourceHashes },
            Items = [CreateItem(new SparkyEntityTierMetadata { TierType = 1, TierLevel = 1 })],
            ProductionRecipes = [CreateRecipe(new SparkyEntityTierMetadata { TierType = 1, TierLevel = 1 })],
            Robots = [CreateRobot("def_robot", "Sample robot") with
            {
                TierMetadata = new SparkyEntityTierMetadata { TierType = 1, TierLevel = 1 }
            }],
            RobotModules = [CreateModule("def_module", "Sample module") with
            {
                IsLaser = false,
                TierMetadata = new SparkyEntityTierMetadata { TierType = 1, TierLevel = 1 }
            }],
            EntityResolutionRules = SparkyEntityResolutionRules.Load(TestRepositoryRoot)
        };
    }

    private static WeaponItem CreateItem(SparkyEntityTierMetadata? tierMetadata) => new()
    {
        DefinitionName = "def_sample_item",
        DisplayName = "Sample item",
        TierMetadata = tierMetadata,
        LocalizedViews = new Dictionary<int, SparkyLocalizedItemView>
        {
            [0] = new() { DisplayName = "Sample item", HasLocalizedDisplayName = true }
        }
    };

    private static ProductionRecipe CreateRecipe(SparkyEntityTierMetadata? tierMetadata, string definitionName = "def_sample_recipe") => new()
    {
        ProductDefinition = 40,
        DefinitionName = definitionName,
        DisplayName = "Sample recipe",
        TierMetadata = tierMetadata,
        LocalizedViews = new Dictionary<int, SparkyLocalizedRecipeView>
        {
            [0] = new() { DisplayName = "Sample recipe" }
        }
    };

    private static string TestRepositoryRoot
    {
        get
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);
            while (current is not null && !File.Exists(Path.Combine(current.FullName, "tools", "sparky-entity-resolution-rules.json")))
            {
                current = current.Parent;
            }

            return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
        }
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
