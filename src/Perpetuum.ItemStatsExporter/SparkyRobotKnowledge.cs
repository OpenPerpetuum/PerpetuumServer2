namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyRobotKnowledgeExport(
    IReadOnlyList<SparkyRobotRecord> Robots,
    IReadOnlyList<SparkyRobotModuleRecord> RobotModules,
    int SkippedRobotCount,
    IReadOnlyList<string> Diagnostics);

internal sealed record SparkyRobotRecord(
    string DefinitionName,
    IReadOnlyList<string> ComponentDefinitionNames,
    IReadOnlyList<SparkyRobotCompatibility> Compatibility,
    IReadOnlyList<SparkyRobotChassisBonusSource> ChassisBonusSources,
    IReadOnlyDictionary<int, SparkyLocalizedRobotView> LocalizedViews);

internal sealed record SparkyRobotChassisBonusSource(
    int ExtensionId,
    int AggregateFieldId,
    double BonusPerLevel,
    AggregateFieldMetadata Metadata,
    IReadOnlyList<string> ComponentDefinitionNames);

internal sealed record SparkyLocalizedRobotView(
    string? DisplayName,
    IReadOnlyList<AnswerStat> AssembledStats,
    IReadOnlyList<SparkyLocalizedRobotComponentView> Components,
    IReadOnlyList<SparkyLocalizedChassisBonus> ChassisBonuses);

internal sealed record SparkyLocalizedRobotComponentView(
    string DefinitionName,
    string DisplayName,
    IReadOnlyList<AnswerStat> Stats,
    int SlotCount);

internal sealed record SparkyRobotCompatibility(
    string ComponentDefinitionName,
    string ModuleDefinitionName,
    IReadOnlyList<int> EligibleSlots);

internal sealed record SparkyLocalizedChassisBonus(
    string ExtensionName,
    string PropertyName,
    string Value,
    string? Unit,
    int ExtensionLevel,
    IReadOnlyList<string> ComponentNames);

internal sealed record SparkyRobotModuleRecord(
    string DefinitionName,
    IReadOnlyDictionary<int, SparkyLocalizedRobotModuleView> LocalizedViews)
{
    public bool? IsLaser { get; init; }
}

internal sealed record SparkyLocalizedRobotModuleView(
    string? DisplayName,
    IReadOnlyList<AnswerStat> CpuAndPowerGridStats,
    bool UniqueCategoryLimit);

internal enum SparkyRobotQuestionFocus
{
    Stats,
    Components,
    ChassisBonus,
    ModuleCompatibility,
    Fastest,
    CompareStats,
    General
}

internal enum SparkyModuleCatalogFocus
{
    Lasers
}

internal sealed record SparkyRobotStatFact(string Label, string Value, string? Unit);

internal sealed record SparkyRobotComponentFact(
    string DisplayName,
    IReadOnlyList<SparkyRobotStatFact> Stats,
    int SlotCount,
    IReadOnlyList<int> EligibleSlots);

internal sealed record SparkyRobotModuleFact(
    string DisplayName,
    IReadOnlyList<SparkyRobotStatFact> CpuAndPowerGridStats,
    bool UniqueCategoryLimit,
    bool HasStaticSlotEligibility,
    bool CurrentLoadoutFitEvaluated,
    string CompatibilityScope);

internal sealed record SparkyRobotAnswerFacts(
    string RobotName,
    SparkyRobotQuestionFocus Focus,
    IReadOnlyList<SparkyRobotStatFact> AssembledStats,
    IReadOnlyList<SparkyRobotComponentFact> Components,
    IReadOnlyList<SparkyLocalizedChassisBonus> ChassisBonuses,
    SparkyRobotModuleFact? Module,
    IReadOnlyList<SparkyRobotSpeedFact>? SpeedRanking = null,
    IReadOnlyList<SparkyRobotStatsComparison>? Comparisons = null);

internal sealed record SparkyRobotSpeedFact(string RobotName, string Speed, string? Unit);

internal sealed record SparkyRobotStatsComparison(
    string FirstRobotName,
    string SecondRobotName,
    IReadOnlyList<SparkyRobotStatComparison> Stats);

internal sealed record SparkyRobotStatComparison(
    string Label,
    string FirstValue,
    string SecondValue,
    string Difference,
    string? Unit);

internal sealed record SparkyRobotLookupResult(
    string Status,
    SparkyRobotAnswerFacts? Facts,
    IReadOnlyList<string> Candidates);

internal sealed record SparkyClarificationEvidence(
    string Reason,
    string? RequestedTerm,
    IReadOnlyList<string> CandidateNames,
    IReadOnlyList<string> MissingEntities);
