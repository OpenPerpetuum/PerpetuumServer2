using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Items.Templates;
using Perpetuum.Modules;
using Perpetuum.Robots;
using Perpetuum.Services.ExtensionService;

namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyRobotKnowledgeBuilder
{
    private static readonly AggregateField[] ModuleUsageFields =
    [
        AggregateField.cpu_usage,
        AggregateField.powergrid_usage
    ];

    public SparkyRobotKnowledgeExport Build(
        IEntityDefaultReader defaults,
        IRobotTemplateRelations relations,
        IExtensionReader extensions,
        SparkyDictionaryCatalog dictionaries,
        IReadOnlyDictionary<int, AggregateFieldMetadata> metadata)
    {
        var diagnostics = new List<string>();
        var moduleEntries = BuildModules(defaults, dictionaries, metadata, diagnostics);
        var moduleRecords = moduleEntries.Select(entry => entry.Record).ToArray();
        var robots = new List<SparkyRobotRecord>();
        int skippedRobots = 0;

        var definitions = defaults.GetAll()
            .Where(definition => definition.CategoryFlags.IsCategory(CategoryFlags.cf_robots))
            .Where(definition => dictionaries.GetTranslation(0, definition.Name) is not null)
            .OrderBy(definition => dictionaries.GetTranslation(0, definition.Name), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (EntityDefault definition in definitions)
        {
            try
            {
                RobotTemplate? template = relations.Get(definition.Definition)?.Template;
                if (template is null)
                {
                    skippedRobots++;
                    diagnostics.Add($"Skipped robot '{definition.Name}' ({definition.Definition}): no robot template relation.");
                    continue;
                }

                if (Entity.Factory.CreateWithRandomEID(definition) is not Robot robot ||
                    robot.Definition != definition.Definition ||
                    !ReferenceEquals(robot.Template, template))
                {
                    skippedRobots++;
                    diagnostics.Add($"Skipped robot '{definition.Name}' ({definition.Definition}): server entity construction did not use the related template.");
                    continue;
                }

                robot.IsRepackaged = false;
                robot.CreateComponents();
                robot.Initialize();
                RobotComponent[] components = robot.RobotComponents.OrderBy(component => component.Type).ToArray();
                if (components.Length == 0 || components.Any(component => component.ED is null))
                {
                    skippedRobots++;
                    diagnostics.Add($"Skipped robot '{definition.Name}' ({definition.Definition}): initialized template has no valid components.");
                    continue;
                }

                string[] componentDefinitionNames = components
                    .Select(component => component.ED.Name)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                var compatibility = BuildCompatibility(definition, components, moduleEntries);
                var chassisBonusSources = BuildChassisBonusSources(components, extensions, metadata);
                var localizedViews = dictionaries.Languages.ToDictionary(
                    language => language.LanguageId,
                    language => BuildRobotView(robot, definition, components, chassisBonusSources, extensions, dictionaries, language, metadata));

                robots.Add(new SparkyRobotRecord(
                    definition.Name,
                    componentDefinitionNames,
                    compatibility,
                    chassisBonusSources,
                    localizedViews));
            }
            catch (Exception exception)
            {
                skippedRobots++;
                diagnostics.Add($"Skipped robot '{definition.Name}' ({definition.Definition}): {exception.GetType().Name} while building its related template.");
            }
        }

        return new SparkyRobotKnowledgeExport(robots, moduleRecords, skippedRobots, diagnostics);
    }

    private static IReadOnlyList<ModuleEntry> BuildModules(
        IEntityDefaultReader defaults,
        SparkyDictionaryCatalog dictionaries,
        IReadOnlyDictionary<int, AggregateFieldMetadata> metadata,
        ICollection<string> diagnostics)
    {
        var modules = new List<ModuleEntry>();
        var definitions = defaults.GetAll()
            .Where(definition => definition.CategoryFlags.IsCategory(CategoryFlags.cf_robot_equipment))
            .Where(definition => dictionaries.GetTranslation(0, definition.Name) is not null)
            .OrderBy(definition => dictionaries.GetTranslation(0, definition.Name), StringComparer.OrdinalIgnoreCase);

        foreach (EntityDefault definition in definitions)
        {
            try
            {
                if (Entity.Factory.CreateWithRandomEID(definition) is not Module module)
                {
                    continue;
                }

                module.Initialize();
                bool uniqueCategoryLimit = module.ED.CategoryFlags.IsUniqueCategoryFlags(out _);
                var localizedViews = dictionaries.Languages.ToDictionary(
                    language => language.LanguageId,
                    language => BuildModuleView(module, definition, dictionaries, language, metadata, uniqueCategoryLimit));

                modules.Add(new ModuleEntry(
                    definition,
                    module,
                    new SparkyRobotModuleRecord(definition.Name, localizedViews)
                    {
                        IsLaser = definition.CategoryFlags.IsCategory(CategoryFlags.cf_lasers)
                    }));
            }
            catch (Exception exception)
            {
                diagnostics.Add($"Skipped robot module '{definition.Name}' ({definition.Definition}): {exception.GetType().Name} while initializing the module.");
            }
        }

        return modules;
    }

    private static SparkyLocalizedRobotView BuildRobotView(
        Robot robot,
        EntityDefault definition,
        IReadOnlyList<RobotComponent> components,
        IReadOnlyList<SparkyRobotChassisBonusSource> chassisBonusSources,
        IExtensionReader extensions,
        SparkyDictionaryCatalog dictionaries,
        SparkyDictionaryLanguage language,
        IReadOnlyDictionary<int, AggregateFieldMetadata> metadata)
    {
        string? displayName = dictionaries.GetTranslation(language.LanguageId, definition.Name);
        if (displayName is null)
        {
            return new SparkyLocalizedRobotView(null, [], [], []);
        }

        var localizedComponents = components
            .Select(component => BuildComponentView(component, dictionaries, language, metadata))
            .Where(component => component is not null)
            .Cast<SparkyLocalizedRobotComponentView>()
            .ToArray();

        return new SparkyLocalizedRobotView(
            displayName,
            ProjectStats(robot.BuildPropertiesDictionary(), language, metadata),
            localizedComponents,
            BuildLocalizedBonuses(robot, chassisBonusSources, extensions, dictionaries, language));
    }

    private static SparkyLocalizedRobotComponentView? BuildComponentView(
        RobotComponent component,
        SparkyDictionaryCatalog dictionaries,
        SparkyDictionaryLanguage language,
        IReadOnlyDictionary<int, AggregateFieldMetadata> metadata)
    {
        string? displayName = dictionaries.GetTranslation(language.LanguageId, component.ED.Name);
        if (displayName is null)
        {
            return null;
        }

        return new SparkyLocalizedRobotComponentView(
            component.ED.Name,
            displayName,
            ProjectStats(component.BuildPropertiesDictionary(), language, metadata),
            component.MaxSlots);
    }

    private static SparkyLocalizedRobotModuleView BuildModuleView(
        Module module,
        EntityDefault definition,
        SparkyDictionaryCatalog dictionaries,
        SparkyDictionaryLanguage language,
        IReadOnlyDictionary<int, AggregateFieldMetadata> metadata,
        bool uniqueCategoryLimit)
    {
        string? displayName = dictionaries.GetTranslation(language.LanguageId, definition.Name);
        if (displayName is null)
        {
            return new SparkyLocalizedRobotModuleView(null, [], uniqueCategoryLimit);
        }

        var usageStats = ProjectStats(module.BuildPropertiesDictionary(), language, metadata)
            .Where(stat => ModuleUsageFields.Contains((AggregateField)stat.FieldId))
            .ToArray();
        return new SparkyLocalizedRobotModuleView(displayName, usageStats, uniqueCategoryLimit);
    }

    private static IReadOnlyList<SparkyRobotCompatibility> BuildCompatibility(
        EntityDefault robotDefinition,
        IReadOnlyList<RobotComponent> components,
        IReadOnlyList<ModuleEntry> modules)
    {
        var result = new List<SparkyRobotCompatibility>();
        foreach (RobotComponent component in components)
        {
            foreach (ModuleEntry entry in modules)
            {
                int[] allowedBots = entry.Module.ED.Options.AllowedBots;
                if (allowedBots.Length > 0 && !allowedBots.Contains(robotDefinition.Definition))
                {
                    continue;
                }

                int[] eligibleSlots = Enumerable.Range(1, Math.Max(0, component.MaxSlots))
                    .Where(slot => component.IsValidSlotTo(entry.Module, slot))
                    .ToArray();
                if (eligibleSlots.Length > 0)
                {
                    result.Add(new SparkyRobotCompatibility(
                        component.ED.Name,
                        entry.Definition.Name,
                        eligibleSlots));
                }
            }
        }

        return result
            .GroupBy(entry => (entry.ComponentDefinitionName, entry.ModuleDefinitionName))
            .Select(group => new SparkyRobotCompatibility(
                group.Key.ComponentDefinitionName,
                group.Key.ModuleDefinitionName,
                group.SelectMany(entry => entry.EligibleSlots).Distinct().Order().ToArray()))
            .OrderBy(entry => entry.ComponentDefinitionName, StringComparer.Ordinal)
            .ThenBy(entry => entry.ModuleDefinitionName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<SparkyLocalizedChassisBonus> BuildLocalizedBonuses(
        Robot robot,
        IReadOnlyList<SparkyRobotChassisBonusSource> sources,
        IExtensionReader extensions,
        SparkyDictionaryCatalog dictionaries,
        SparkyDictionaryLanguage language)
    {
        var extensionInfo = extensions.GetExtensions();
        var result = new List<SparkyLocalizedChassisBonus>();
        foreach (SparkyRobotChassisBonusSource source in sources)
        {
            if (!extensionInfo.TryGetValue(source.ExtensionId, out ExtensionInfo? info) ||
                !Enum.IsDefined(typeof(AggregateField), source.AggregateFieldId))
            {
                continue;
            }

            AggregateField field = (AggregateField)source.AggregateFieldId;
            AggregateFieldMetadata fieldMetadata = source.Metadata;
            // IExtensionReader.GetExtensionName returns the exact customDictionary key (for example, ext_railgun).
            string? translatedExtensionName = dictionaries.GetTranslation(language.LanguageId, info.name);
            if (translatedExtensionName is null || !HasDictionaryStatLabel(language, fieldMetadata))
            {
                continue;
            }

            string?[] componentTranslations = source.ComponentDefinitionNames
                .Select(name => dictionaries.GetTranslation(language.LanguageId, name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (componentTranslations.Length == 0 || componentTranslations.Any(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            bool requiresUnit = !string.IsNullOrWhiteSpace(fieldMetadata.MeasurementUnitKey) &&
                                !string.Equals(fieldMetadata.MeasurementUnitKey, "•", StringComparison.Ordinal);
            string? unit = requiresUnit
                ? dictionaries.GetTranslation(language.LanguageId, fieldMetadata.MeasurementUnitKey)
                : null;
            if (requiresUnit && unit is null)
            {
                continue;
            }

            ItemPropertyModifier baseModifier = robot.GetPropertyModifier(field);
            ItemPropertyModifier extensionModifier = ItemPropertyModifier.Create(field, source.BonusPerLevel);
            extensionModifier.NormalizeExtensionBonus();
            extensionModifier.Modify(ref baseModifier);
            if (!double.IsFinite(baseModifier.Value))
            {
                continue;
            }

            var stat = new StatValue
            {
                FieldId = source.AggregateFieldId,
                FieldEnumName = fieldMetadata.EnumName,
                LabelTranslationKey = fieldMetadata.LabelTranslationKey,
                Label = fieldMetadata.Label,
                Value = baseModifier.Value,
                Metadata = fieldMetadata
            };
            AnswerStat? projected = Program.BuildAnswerStat(stat, language.Translations, language.LanguageId);
            if (projected is null)
            {
                continue;
            }

            result.Add(new SparkyLocalizedChassisBonus(
                translatedExtensionName,
                projected.Label,
                projected.Value,
                projected.Unit,
                ExtensionLevel: 1,
                componentTranslations.Cast<string>().ToArray()));
        }

        return result;
    }

    private static IReadOnlyList<SparkyRobotChassisBonusSource> BuildChassisBonusSources(
        IReadOnlyList<RobotComponent> components,
        IExtensionReader extensions,
        IReadOnlyDictionary<int, AggregateFieldMetadata> metadata)
    {
        var extensionInfo = extensions.GetExtensions();
        var bonusGroups = components
            .SelectMany(component => component.ExtensionBonuses.Select(bonus => new { Component = component, Bonus = bonus }))
            .Where(entry => extensionInfo.ContainsKey(entry.Bonus.extensionId))
            .GroupBy(entry => (entry.Bonus.extensionId, entry.Bonus.aggregateField))
            .OrderBy(group => extensionInfo[group.Key.extensionId].name, StringComparer.Ordinal)
            .ThenBy(group => (int)group.Key.aggregateField);

        var result = new List<SparkyRobotChassisBonusSource>();
        foreach (var group in bonusGroups)
        {
            double bonusPerLevel = group.Sum(entry => entry.Bonus.bonus);
            int fieldId = (int)group.Key.aggregateField;
            if (!double.IsFinite(bonusPerLevel) || Math.Abs(bonusPerLevel) < double.Epsilon ||
                !metadata.TryGetValue(fieldId, out AggregateFieldMetadata? fieldMetadata) ||
                !Enum.IsDefined(typeof(AggregateField), fieldId))
            {
                continue;
            }

            result.Add(new SparkyRobotChassisBonusSource(
                group.Key.extensionId,
                fieldId,
                bonusPerLevel,
                fieldMetadata,
                group.Select(entry => entry.Component.ED.Name)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray()));
        }

        return result;
    }

    private static bool HasDictionaryStatLabel(SparkyDictionaryLanguage language, AggregateFieldMetadata metadata)
    {
        return language.Translations.TryGetValue(metadata.LabelTranslationKey, out var label) &&
               !string.IsNullOrWhiteSpace(label) ||
               metadata.EnumName is { } enumName &&
               language.Translations.TryGetValue(enumName, out label) &&
               !string.IsNullOrWhiteSpace(label);
    }

    private static IReadOnlyList<AnswerStat> ProjectStats(
        IDictionary<string, object> properties,
        SparkyDictionaryLanguage language,
        IReadOnlyDictionary<int, AggregateFieldMetadata> metadata)
    {
        return Program.ReadStats(properties, language.Translations, metadata)
            .Where(stat => double.IsFinite(stat.Value))
            .Select(stat => Program.BuildAnswerStat(stat, language.Translations, language.LanguageId))
            .Where(stat => stat is not null)
            .Cast<AnswerStat>()
            .ToArray();
    }

    private sealed record ModuleEntry(
        EntityDefault Definition,
        Module Module,
        SparkyRobotModuleRecord Record);
}
