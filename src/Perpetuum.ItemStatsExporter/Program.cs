using System.Text.Json;
using System.Runtime.Versioning;
using System.Globalization;
using Perpetuum.Bootstrapper;
using Perpetuum.Data;
using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Items.Ammos;
using Perpetuum.Modules.Weapons;
using Perpetuum.Services.ProductionEngine;

[assembly: SupportedOSPlatform("windows")]

namespace Perpetuum.ItemStatsExporter;

internal static class Program
{
    private static readonly CategoryFlags[] WeaponCategories =
    [
        CategoryFlags.cf_small_lasers,
        CategoryFlags.cf_medium_lasers,
        CategoryFlags.cf_large_lasers,
        CategoryFlags.cf_small_railguns,
        CategoryFlags.cf_medium_railguns,
        CategoryFlags.cf_large_railguns,
        CategoryFlags.cf_small_single_projectile,
        CategoryFlags.cf_medium_single_projectile,
        CategoryFlags.cf_hell_cannons,
        CategoryFlags.cf_raven_cannons,
        CategoryFlags.cf_sentry_turret_guns,
        CategoryFlags.cf_small_missile_launchers,
        CategoryFlags.cf_medium_missile_launchers,
        CategoryFlags.cf_large_missile_launchers
    ];

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintUsage();
            return 0;
        }

        if (args.Length != 3 &&
            !(args.Length == 4 && args[0] == "--ask") &&
            !(args.Length == 2 && args[0] == "--serve") &&
            !(args.Length == 4 && args[0] == "--serve" && args[2] == "--config"))
        {
            PrintUsage();
            return 2;
        }

        try
        {
            if (args[0] == "--serve")
            {
                var configurationPath = args.Length == 4 ? args[3] : null;
                using var options = SparkyServiceOptions.Load(args[1], configurationPath);
                var export = JsonSerializer.Deserialize<ExportDocument>(File.ReadAllText(options.ExportPath))
                    ?? throw new InvalidDataException("The input export did not contain a JSON object.");
                ValidateExportDocument(export);
                return await SparkyHttpService.RunAsync(export, options, CancellationToken.None);
            }

            if (args[0] == "--ask")
            {
                var askExport = JsonSerializer.Deserialize<ExportDocument>(File.ReadAllText(args[1]))
                    ?? throw new InvalidDataException("The input export did not contain a JSON object.");
                var lookup = ItemStatsLookup.Resolve(args[3], askExport.Items, askExport.ProductionRecipes);
                if (lookup.Status != "found" || lookup.Item is null)
                {
                    Console.WriteLine(JsonSerializer.Serialize(lookup, JsonOutputOptions));
                    return 0;
                }

                var baseUrl = Environment.GetEnvironmentVariable("SPARKY_LMSTUDIO_BASE_URL") ?? "http://localhost:1234/v1";
                var maxAnswerCharacters = ReadMaxAnswerCharacters();
                var maxCompletionTokens = ReadPositiveIntegerSetting("SPARKY_LMSTUDIO_MAX_COMPLETION_TOKENS", 700);
                var timeoutSeconds = ReadPositiveIntegerSetting("SPARKY_LMSTUDIO_TIMEOUT_SECONDS", 120);
                var answer = await LocalModelAnswerer.AnswerAsync(
                    baseUrl,
                    args[2],
                    args[3],
                    lookup.Item,
                    maxAnswerCharacters,
                    maxCompletionTokens,
                    TimeSpan.FromSeconds(timeoutSeconds),
                    apiToken: Environment.GetEnvironmentVariable("SPARKY_LMSTUDIO_API_TOKEN"));

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    Status = "answered",
                    Query = args[3],
                    Item = lookup.Item.DisplayName,
                    Answer = answer
                }, JsonOutputOptions));
                return 0;
            }

            if (args[0] == "--answer")
            {
                var answerExport = JsonSerializer.Deserialize<ExportDocument>(File.ReadAllText(args[1]))
                    ?? throw new InvalidDataException("The input export did not contain a JSON object.");
                var response = ItemStatsLookup.Resolve(args[2], answerExport.Items, answerExport.ProductionRecipes);
                Console.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            if (args[0] == "--project-existing")
            {
                var existingExport = JsonSerializer.Deserialize<ExportDocument>(File.ReadAllText(args[1]))
                    ?? throw new InvalidDataException("The input export did not contain a JSON object.");

                foreach (var item in existingExport.Items)
                {
                    item.AnswerView = BuildAnswerView(item);
                }

                WriteExport(existingExport, args[2]);
                Console.WriteLine($"Added answer views for {existingExport.Items.Count} weapon modules.");
                return 0;
            }

            var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(args[1]),
                new JsonSerializerOptions { AllowTrailingCommas = true })
                ?? throw new InvalidDataException("The translation dictionary did not contain a JSON object.");

            using var runtime = OfflineContentRuntime.Initialize(args[0]);
            var reader = runtime.Resolve<IEntityDefaultReader>();
            var metadata = LoadAggregateMetadata(translations);
            var data = BuildExport(reader, translations, metadata);

            WriteExport(data, args[2]);
            var outputPath = Path.GetFullPath(args[2]);
            Console.WriteLine($"Exported {data.Items.Count} translated weapon modules and {data.ProductionRecipes.Count} production recipes to {outputPath}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static readonly JsonSerializerOptions JsonOutputOptions = new() { WriteIndented = true };

    private static void ValidateExportDocument(ExportDocument export)
    {
        if (export.Items is null || export.ProductionRecipes is null)
        {
            throw new InvalidDataException("The input export has an invalid structure.");
        }

        var definitionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in export.Items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.DefinitionName) ||
                string.IsNullOrWhiteSpace(item.DisplayName) || !definitionNames.Add(item.DefinitionName) ||
                item.AnswerView is null || item.AnswerView.Stats is null ||
                item.AnswerView.Stats.Any(stat => stat is null || string.IsNullOrWhiteSpace(stat.Label)) ||
                item.AnswerView.AmmoVariants is null ||
                item.AnswerView.AmmoVariants.Any(ammo => ammo is null ||
                    string.IsNullOrWhiteSpace(ammo.DisplayName) || ammo.Damage is null ||
                    ammo.Damage.Any(damage => damage is null)))
            {
                throw new InvalidDataException("The input export has an invalid structure.");
            }
        }

        foreach (var recipe in export.ProductionRecipes)
        {
            if (recipe is null || string.IsNullOrWhiteSpace(recipe.DefinitionName) ||
                (recipe.AnswerView is not null && string.IsNullOrWhiteSpace(recipe.DisplayName)) ||
                (recipe.DisplayName is not null && string.IsNullOrWhiteSpace(recipe.DisplayName)) ||
                (recipe.AnswerView is not null && (recipe.AnswerView.Components is null ||
                    recipe.AnswerView.Components.Any(component => component is null ||
                        string.IsNullOrWhiteSpace(component.DisplayName)))) ||
                (recipe.FullMaterialsAnswerView is not null &&
                    (recipe.FullMaterialsAnswerView.Materials is null ||
                     recipe.FullMaterialsAnswerView.Materials.Any(material => material is null ||
                         string.IsNullOrWhiteSpace(material.DisplayName)))))
            {
                throw new InvalidDataException("The input export has an invalid structure.");
            }
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Perpetuum.ItemStatsExporter <GameRoot> <customDictionary/0.json> <output.json>");
        Console.WriteLine("   or: Perpetuum.ItemStatsExporter --project-existing <input.json> <output.json>");
        Console.WriteLine("   or: Perpetuum.ItemStatsExporter --answer <input.json> <question>");
        Console.WriteLine("   or: Perpetuum.ItemStatsExporter --ask <input.json> <model-id> <question>");
        Console.WriteLine("   or: Perpetuum.ItemStatsExporter --serve <input.json> [--config <settings.json>]");
        Console.WriteLine("Runs offline and does not start the game server.");
        Console.WriteLine("--ask sends only the matched item's verified stats, ammo, recipe, and full-material facts to LM Studio.");
        Console.WriteLine("SPARKY_LMSTUDIO_BASE_URL defaults to http://localhost:1234/v1.");
        Console.WriteLine("SPARKY_LMSTUDIO_MAX_COMPLETION_TOKENS defaults to 700.");
        Console.WriteLine("SPARKY_MAX_ANSWER_CHARACTERS defaults to 2000.");
        Console.WriteLine("--serve requires a 32+ character SPARKY_API_TOKEN and SPARKY_LMSTUDIO_MODEL_ID, supplied by environment or --config.");
        Console.WriteLine("The --config file uses DPAPI-protected secret values; create it with tools/Configure-SparkyServiceSettings.ps1 and protect its NTFS ACL.");
        Console.WriteLine("--serve uses SPARKY_SERVICE_URL (HTTPS loopback only; default https://127.0.0.1:5080). SPARKY_SERVICE_CERT_PATH is required.");
        Console.WriteLine("SPARKY_SERVICE_CERT_PASSWORD optionally supplies the PFX password.");
        Console.WriteLine("Set SPARKY_TUNNEL_ENABLED=true to supervise the configured reverse SSH tunnel.");
        Console.WriteLine("Tunnel settings: SPARKY_SSH_HOST, SPARKY_SSH_PORT, SPARKY_SSH_USER, SPARKY_SSH_PRIVATE_KEY_PATH, SPARKY_SSH_KNOWN_HOSTS_PATH, SPARKY_VDM_FORWARD_PORT.");
        Console.WriteLine("Optional tunnel settings: SPARKY_SSH_CLIENT_PATH, SPARKY_SSH_RECONNECT_MIN_SECONDS, SPARKY_SSH_RECONNECT_MAX_SECONDS.");
        Console.WriteLine("Service limits: SPARKY_MAX_REQUEST_BODY_BYTES, SPARKY_MAX_QUESTION_CHARACTERS, SPARKY_SERVICE_MAX_CONCURRENCY.");
        Console.WriteLine("Inference limits: SPARKY_LMSTUDIO_TIMEOUT_SECONDS and SPARKY_LMSTUDIO_MAX_COMPLETION_TOKENS.");
    }

    private static int ReadMaxAnswerCharacters()
    {
        return ReadPositiveIntegerSetting("SPARKY_MAX_ANSWER_CHARACTERS", 2000);
    }

    private static int ReadPositiveIntegerSetting(string settingName, int defaultValue)
    {
        var configuredValue = Environment.GetEnvironmentVariable(settingName);
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return defaultValue;
        }

        if (!int.TryParse(configuredValue, out var value) || value < 1)
        {
            throw new InvalidDataException($"{settingName} must be a positive integer.");
        }

        return value;
    }

    private static ExportDocument BuildExport(
        IEntityDefaultReader reader,
        IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, AggregateFieldMetadata> metadata)
    {
        var items = new List<WeaponItem>();
        var definitions = reader.GetAll().ToArray();
        var productionRecipes = BuildProductionRecipes(definitions, translations);
        var translatedDefinitions = definitions
            .Where(definition => definition.CategoryFlags.IsAny(WeaponCategories))
            .Where(definition => translations.ContainsKey(definition.Name))
            .ToDictionary(definition => definition.Definition);

        foreach (var definition in translatedDefinitions.Values.OrderBy(definition => translations[definition.Name], StringComparer.OrdinalIgnoreCase))
        {
            if (Entity.Factory.CreateWithRandomEID(definition) is not WeaponModule module)
            {
                continue;
            }

            // GetEntityDefaults initializes the item after the factory has initialized it.
            // Mirror that path so the exported baseline matches the client's default payload.
            module.Initialize();
            var item = new WeaponItem
            {
                DefinitionName = definition.Name,
                DisplayName = translations[definition.Name],
                BaseStats = ReadStats(module.BuildPropertiesDictionary(), translations, metadata)
            };

            foreach (var ammoDefinition in definitions.Where(IsPotentialWeaponAmmo))
            {
                if (!module.CheckLoadableAmmo(ammoDefinition.Definition))
                {
                    continue;
                }

                if (Entity.Factory.CreateWithRandomEID(ammoDefinition) is not WeaponAmmo ammo)
                {
                    continue;
                }

                module.SetAmmo(ammo);
                module.UpdateAllProperties();
                item.AmmoVariants.Add(new AmmoVariant
                {
                    DefinitionName = ammoDefinition.Name,
                    DisplayName = translations.GetValueOrDefault(ammoDefinition.Name),
                    ModuleStats = ReadStats(module.BuildPropertiesDictionary(), translations, metadata),
                    AmmoStats = ReadStats(ammo.BuildPropertiesDictionary(), translations, metadata),
                    Damage = ammo.GetCleanDamages().Select(damage => new DamageValue
                    {
                        Type = damage.type.ToString(),
                        Value = damage.value
                    }).ToList()
                });
            }

            item.AnswerView = BuildAnswerView(item);
            items.Add(item);
        }

        return new ExportDocument
        {
            Source = "Perpetuum entity factory and initialized item properties; production recipes and recursive material totals use ProductionComponent semantics and the production_data visibility filter.",
            Items = items,
            ProductionRecipes = productionRecipes
        };
    }

    private static List<ProductionRecipe> BuildProductionRecipes(
        IReadOnlyCollection<EntityDefault> definitions,
        IReadOnlyDictionary<string, string> translations)
    {
        // Use the documented player-facing production_data view to apply its purchasable,
        // enabled, and visible product filter. The component IDs and amounts still come
        // from components so the server's ProductionComponent rules can calculate quantity.
        const string sql = "select c.definition as productdefinition, c.componentdefinition, c.componentamount " +
                           "from components c where exists " +
                           "(select 1 from production_data pd where pd.itemdefinition = c.definition)";

        var definitionById = definitions.ToDictionary(definition => definition.Definition);
        var groupedComponents = new Dictionary<int, List<ProductionComponent>>();
        foreach (var record in Db.Query().CommandText(sql).Execute())
        {
            var productDefinition = record.GetValue<int>("productdefinition");
            var componentDefinition = record.GetValue<int>("componentdefinition");
            if (!definitionById.TryGetValue(productDefinition, out _) ||
                !definitionById.TryGetValue(componentDefinition, out var componentDefault))
            {
                continue;
            }

            if (!groupedComponents.TryGetValue(productDefinition, out var components))
            {
                components = [];
                groupedComponents.Add(productDefinition, components);
            }

            components.Add(new ProductionComponent(componentDefault, record.GetValue<int>("componentamount")));
        }

        var recipes = new List<ProductionRecipe>();
        foreach (var entry in groupedComponents.OrderBy(entry => entry.Key))
        {
            var product = definitionById[entry.Key];
            var recipe = new ProductionRecipe
            {
                ProductDefinition = product.Definition,
                DefinitionName = product.Name,
                DisplayName = translations.GetValueOrDefault(product.Name),
                OutputQuantity = product.Quantity,
                Components = entry.Value
                    .OrderBy(component => component.EntityDefault.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(component => new ProductionRecipeComponent
                    {
                        Definition = component.EntityDefault.Definition,
                        DefinitionName = component.EntityDefault.Name,
                        DisplayName = translations.GetValueOrDefault(component.EntityDefault.Name),
                        Amount = component.Amount,
                        BaseAmount = component.EffectiveAmount(1, 1)
                    })
                    .ToList()
            };

            var translatedComponents = recipe.Components.All(component => !string.IsNullOrWhiteSpace(component.DisplayName));
            if (!string.IsNullOrWhiteSpace(recipe.DisplayName) && translatedComponents)
            {
                recipe.AnswerView = new ProductionRecipeAnswerView
                {
                    OutputQuantity = recipe.OutputQuantity,
                    Components = recipe.Components.Select(component => new ProductionRecipeAnswerComponent
                    {
                        DisplayName = component.DisplayName!,
                        Amount = component.BaseAmount
                    }).ToList()
                };
            }

            recipes.Add(recipe);
        }

        var recipeByDefinition = recipes.ToDictionary(recipe => recipe.ProductDefinition);
        foreach (var recipe in recipes.Where(recipe => recipe.AnswerView is not null))
        {
            var materials = new Dictionary<int, ExpandedMaterial>();
            if (TryExpandRecipe(
                    recipe,
                    1,
                    recipeByDefinition,
                    definitionById,
                    new HashSet<int>(),
                    materials))
            {
                recipe.FullMaterialsAnswerView = new FullMaterialsAnswerView
                {
                    OutputQuantity = recipe.OutputQuantity,
                    Materials = materials.Values
                        .OrderBy(material => material.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .Select(material => new ProductionRecipeAnswerComponent
                        {
                            DisplayName = material.DisplayName,
                            Amount = material.Amount
                        })
                        .ToList()
                };
            }
        }

        return recipes;
    }

    private static bool TryExpandRecipe(
        ProductionRecipe recipe,
        int productionCycles,
        IReadOnlyDictionary<int, ProductionRecipe> recipeByDefinition,
        IReadOnlyDictionary<int, EntityDefault> definitionById,
        HashSet<int> expansionPath,
        IDictionary<int, ExpandedMaterial> materials)
    {
        if (recipe.OutputQuantity < 1 || recipe.AnswerView is null || !expansionPath.Add(recipe.ProductDefinition))
        {
            return false;
        }

        foreach (var component in recipe.Components)
        {
            if (!definitionById.TryGetValue(component.Definition, out var componentDefault))
            {
                expansionPath.Remove(recipe.ProductDefinition);
                return false;
            }

            var requiredAmount = new ProductionComponent(componentDefault, component.Amount)
                .EffectiveAmount(productionCycles, 1);

            if (recipeByDefinition.TryGetValue(component.Definition, out var componentRecipe))
            {
                if (componentRecipe.OutputQuantity < 1 || componentRecipe.AnswerView is null)
                {
                    expansionPath.Remove(recipe.ProductDefinition);
                    return false;
                }

                var componentCycles = (int)(((long)requiredAmount + componentRecipe.OutputQuantity - 1) /
                                            componentRecipe.OutputQuantity);
                if (!TryExpandRecipe(
                        componentRecipe,
                        componentCycles,
                        recipeByDefinition,
                        definitionById,
                        expansionPath,
                        materials))
                {
                    expansionPath.Remove(recipe.ProductDefinition);
                    return false;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(component.DisplayName))
            {
                expansionPath.Remove(recipe.ProductDefinition);
                return false;
            }

            if (materials.TryGetValue(component.Definition, out var existing))
            {
                materials[component.Definition] = existing with { Amount = checked(existing.Amount + requiredAmount) };
            }
            else
            {
                materials.Add(component.Definition, new ExpandedMaterial(component.DisplayName, requiredAmount));
            }
        }

        expansionPath.Remove(recipe.ProductDefinition);
        return true;
    }

    private static bool IsPotentialWeaponAmmo(EntityDefault definition)
    {
        return definition.CategoryFlags.IsCategory(CategoryFlags.cf_ammo);
    }

    private static Dictionary<int, AggregateFieldMetadata> LoadAggregateMetadata(IReadOnlyDictionary<string, string> translations)
    {
        const string sql = "select id, name, formula, measurementunit, measurementmultiplier, measurementoffset, category, digits from aggregatefields";
        return Db.Query().CommandText(sql).Execute().ToDictionary(
            record => record.GetValue<int>("id"),
            record =>
            {
                var id = record.GetValue<int>("id");
                var unitKey = record.GetValue<string>("measurementunit");
                return new AggregateFieldMetadata
                {
                    Id = id,
                    EnumName = Enum.IsDefined(typeof(AggregateField), id) ? Enum.GetName(typeof(AggregateField), id) : null,
                    DatabaseName = record.GetValue<string>("name"),
                    LabelTranslationKey = $"Aggregate_unknown_{id}",
                    Label = translations.GetValueOrDefault($"Aggregate_unknown_{id}"),
                    Formula = record.GetValue<int>("formula"),
                    FormulaName = Enum.GetName(typeof(AggregateFormula), record.GetValue<int>("formula")),
                    MeasurementUnitKey = unitKey,
                    MeasurementUnit = translations.GetValueOrDefault(unitKey),
                    MeasurementMultiplier = record.GetValue<double>("measurementmultiplier"),
                    MeasurementOffset = record.GetValue<double>("measurementoffset"),
                    Category = record.GetValue<int>("category"),
                    Digits = record.GetValue<int>("digits")
                };
            });
    }

    private static List<StatValue> ReadStats(
        IDictionary<string, object> properties,
        IReadOnlyDictionary<string, string> translations,
        IReadOnlyDictionary<int, AggregateFieldMetadata> metadata)
    {
        var result = new List<StatValue>();
        foreach (var entry in properties)
        {
            if (entry.Value is not IDictionary<string, object> field ||
                !field.TryGetValue("ID", out var idValue) ||
                !field.TryGetValue("value", out var statValue))
            {
                continue;
            }

            var id = Convert.ToInt32(idValue);
            metadata.TryGetValue(id, out var fieldMetadata);
            result.Add(new StatValue
            {
                FieldId = id,
                FieldEnumName = fieldMetadata?.EnumName,
                LabelTranslationKey = fieldMetadata?.LabelTranslationKey ?? $"Aggregate_unknown_{id}",
                Label = fieldMetadata?.Label,
                Value = Convert.ToDouble(statValue),
                Metadata = fieldMetadata
            });
        }

        return result.OrderBy(stat => stat.FieldId).ToList();
    }

    private static ItemAnswerView BuildAnswerView(WeaponItem item)
    {
        return new ItemAnswerView
        {
            Stats = item.BaseStats
                .Select(BuildAnswerStat)
                .Where(stat => stat is not null)
                .Cast<AnswerStat>()
                .ToList(),
            AmmoVariants = item.AmmoVariants
                .Where(ammo => !string.IsNullOrWhiteSpace(ammo.DisplayName))
                .Select(ammo => new AmmoAnswerView
                {
                    DisplayName = ammo.DisplayName!,
                    Damage = ammo.Damage.Select(damage => new AnswerDamage
                    {
                        Type = damage.Type,
                        Value = FormatNumber(damage.Value, 2)
                    }).ToList()
                })
                .ToList()
        };
    }

    private static AnswerStat? BuildAnswerStat(StatValue stat)
    {
        var metadata = stat.Metadata;
        var label = stat.Label;
        if (string.IsNullOrWhiteSpace(label) && stat.FieldEnumName is not null)
        {
            label = HumanizeAggregateName(stat.FieldEnumName);

            // Enum names provide a useful English fallback for known fields. If neither a
            // translated unit nor a translated field label exists, avoid emitting a guess.
            if (string.IsNullOrWhiteSpace(metadata?.MeasurementUnit))
            {
                return null;
            }
        }

        if (string.IsNullOrWhiteSpace(label) || metadata is null)
        {
            return null;
        }

        var value = stat.Value * metadata.MeasurementMultiplier + metadata.MeasurementOffset;
        var unit = metadata.MeasurementUnit;
        if (unit == "•")
        {
            unit = null;
        }

        return new AnswerStat
        {
            FieldId = stat.FieldId,
            Label = label,
            Value = FormatNumber(value, metadata.Digits),
            Unit = string.IsNullOrWhiteSpace(unit) ? null : unit
        };
    }

    private static string HumanizeAggregateName(string enumName)
    {
        var words = enumName.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select((word, index) => word.ToLowerInvariant() switch
            {
                "cpu" => "CPU",
                "pbs" => "PBS",
                "ew" => "EW",
                "ep" => "EP",
                _ => index == 0
                    ? char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()
                    : word.ToLowerInvariant()
            });

        return string.Join(' ', words);
    }

    private static string FormatNumber(double value, int digits)
    {
        var decimals = Math.Clamp(digits, 0, 15);
        var format = decimals == 0 ? "0" : $"0.{new string('#', decimals)}";
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static void WriteExport(ExportDocument data, string outputFile)
    {
        var outputPath = Path.GetFullPath(outputFile);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed class ExportDocument
{
    public string Source { get; init; } = string.Empty;
    public List<WeaponItem> Items { get; init; } = [];
    public List<ProductionRecipe> ProductionRecipes { get; init; } = [];
}

internal sealed class ProductionRecipe
{
    public int ProductDefinition { get; init; }
    public string DefinitionName { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public int OutputQuantity { get; init; }
    public List<ProductionRecipeComponent> Components { get; init; } = [];
    public ProductionRecipeAnswerView? AnswerView { get; set; }
    public FullMaterialsAnswerView? FullMaterialsAnswerView { get; set; }
}

internal sealed class ProductionRecipeComponent
{
    public int Definition { get; init; }
    public string DefinitionName { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public int Amount { get; init; }
    public int BaseAmount { get; init; }
}

internal sealed class ProductionRecipeAnswerView
{
    public int OutputQuantity { get; init; }
    public List<ProductionRecipeAnswerComponent> Components { get; init; } = [];
}

internal sealed class FullMaterialsAnswerView
{
    public int OutputQuantity { get; init; }
    public List<ProductionRecipeAnswerComponent> Materials { get; init; } = [];
}

internal sealed class ProductionRecipeAnswerComponent
{
    public string DisplayName { get; init; } = string.Empty;
    public long Amount { get; init; }
}

internal sealed record ExpandedMaterial(string DisplayName, long Amount);

internal sealed class WeaponItem
{
    public string DefinitionName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public List<StatValue> BaseStats { get; init; } = [];
    public List<AmmoVariant> AmmoVariants { get; init; } = [];
    public ItemAnswerView AnswerView { get; set; } = new();
}

internal sealed class ItemAnswerView
{
    public List<AnswerStat> Stats { get; init; } = [];
    public List<AmmoAnswerView> AmmoVariants { get; init; } = [];
}

internal sealed class AnswerStat
{
    public int FieldId { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string? Unit { get; init; }
}

internal sealed class AmmoAnswerView
{
    public string DisplayName { get; init; } = string.Empty;
    public List<AnswerDamage> Damage { get; init; } = [];
}

internal sealed class AnswerDamage
{
    public string Type { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

internal sealed class AmmoVariant
{
    public string DefinitionName { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public List<StatValue> ModuleStats { get; init; } = [];
    public List<StatValue> AmmoStats { get; init; } = [];
    public List<DamageValue> Damage { get; init; } = [];
}

internal sealed class StatValue
{
    public int FieldId { get; init; }
    public string? FieldEnumName { get; init; }
    public string LabelTranslationKey { get; init; } = string.Empty;
    public string? Label { get; init; }
    public double Value { get; init; }
    public AggregateFieldMetadata? Metadata { get; init; }
}

internal sealed class AggregateFieldMetadata
{
    public int Id { get; init; }
    public string? EnumName { get; init; }
    public string? DatabaseName { get; init; }
    public string LabelTranslationKey { get; init; } = string.Empty;
    public string? Label { get; init; }
    public int Formula { get; init; }
    public string? FormulaName { get; init; }
    public string? MeasurementUnitKey { get; init; }
    public string? MeasurementUnit { get; init; }
    public double MeasurementMultiplier { get; init; }
    public double MeasurementOffset { get; init; }
    public int Category { get; init; }
    public int Digits { get; init; }
}

internal sealed class DamageValue
{
    public string Type { get; init; } = string.Empty;
    public double Value { get; init; }
}
