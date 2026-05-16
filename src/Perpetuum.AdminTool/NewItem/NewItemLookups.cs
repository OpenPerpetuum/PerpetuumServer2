using System.Collections.Generic;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.Packages;

namespace Perpetuum.AdminTool.NewItem;

public class NewItemLookups
{
    public IReadOnlyList<AggregateFieldInfo> AggregateFields { get; init; } = [];
    public IReadOnlyList<ExtensionPickItem> Extensions { get; init; } = [];
    public IReadOnlyList<TechTreeGroupPickItem> TechTreeGroups { get; init; } = [];
    public IReadOnlyList<PointTypePickItem> PointTypes { get; init; } = [];
    public IReadOnlyList<PackageItemPickItem> EnabledItems { get; init; } = [];
    public IReadOnlyList<(long CategoryFlags, int BaseField, int ModifierField)> ExistingModPropertyModifiers { get; init; } = [];
    public IReadOnlyList<(long CategoryFlags, int BaseField, int ModifierField)> ExistingAggregateModifiers { get; init; } = [];
    public IReadOnlyDictionary<long, double> ExistingProductionDurations { get; init; } = new Dictionary<long, double>();
    public IReadOnlyList<DefinitionConfigColumnInfo> DefinitionConfigColumns { get; init; } = [];
}
