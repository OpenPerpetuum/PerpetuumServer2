using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Perpetuum.AdminTool.NewItem;

public partial class ResearchPanelViewModel : ObservableObject
{
    [ObservableProperty] private int _researchLevel = 1;
    [ObservableProperty] private bool _isEnabled = true;

    // When IsCraftable, the calibration program is @cprgDef (resolved in SQL).
    // UseCprgRef = true means ItemSqlBuilder will use @cprgDef.
    // UseCprgRef = false means ManualCalibrationProgramDefinition is used instead.
    [ObservableProperty] private bool _useCprgRef = true;
    [ObservableProperty] private int? _manualCalibrationProgramDefinition;

    [ObservableProperty] private IReadOnlyList<ExtensionPickItem> _availableExtensions = [];
    [ObservableProperty] private IReadOnlyList<TechTreeGroupPickItem> _availableTechTreeGroups = [];
    [ObservableProperty] private IReadOnlyList<PointTypePickItem> _availablePointTypes = [];

    public ObservableCollection<TechTreePlacementRow> TechTreeRows { get; } = new();
    public ObservableCollection<ResearchCostRow> ResearchCostRows { get; } = new();
    public ObservableCollection<EnablerExtensionRow> EnablerExtensionRows { get; } = new();

    public void Initialize(NewItemLookups lookups)
    {
        AvailableExtensions = lookups.Extensions;
        AvailableTechTreeGroups = lookups.TechTreeGroups;
        AvailablePointTypes = lookups.PointTypes;
    }

    [RelayCommand] private void AddTechTreeRow() => TechTreeRows.Add(new TechTreePlacementRow());
    [RelayCommand] private void RemoveTechTreeRow(TechTreePlacementRow row) => TechTreeRows.Remove(row);
    [RelayCommand] private void AddResearchCost() => ResearchCostRows.Add(new ResearchCostRow());
    [RelayCommand] private void RemoveResearchCost(ResearchCostRow row) => ResearchCostRows.Remove(row);
    [RelayCommand] private void AddEnablerExtension() => EnablerExtensionRows.Add(new EnablerExtensionRow());
    [RelayCommand] private void RemoveEnablerExtension(EnablerExtensionRow row) => EnablerExtensionRows.Remove(row);

    public void LoadFromClone(CloneExtendedData clone)
    {
        if (clone.ResearchLevel.HasValue)
        {
            var (lvl, _, enabled) = clone.ResearchLevel.Value;
            ResearchLevel = lvl;
            IsEnabled = enabled;
            // Leave UseCprgRef = true so the new _cprg entity is referenced automatically
        }

        TechTreeRows.Clear();
        foreach (var (pd, grp, x, y, ext) in clone.TechTree)
            TechTreeRows.Add(new TechTreePlacementRow
            {
                ParentDefinition = pd, GroupId = grp, X = x, Y = y,
                EnablerExtensionId = ext,
                OriginalParentDefinition = pd, OriginalX = x, OriginalY = y
            });

        ResearchCostRows.Clear();
        foreach (var (pt, amt) in clone.ResearchCosts)
            ResearchCostRows.Add(new ResearchCostRow { PointTypeId = pt, Amount = amt, OriginalAmount = amt });

        EnablerExtensionRows.Clear();
        foreach (var (extId, lvl) in clone.EnablerExtensions)
            EnablerExtensionRows.Add(new EnablerExtensionRow
            {
                ExtensionId = extId, ExtensionLevel = lvl,
                OriginalExtensionId = extId, OriginalExtensionLevel = lvl
            });
    }

    public bool HasDuplicatePointTypes()
    {
        var ids = ResearchCostRows.Select(r => r.PointTypeId).ToList();
        return ids.Count != ids.Distinct().Count();
    }
}
