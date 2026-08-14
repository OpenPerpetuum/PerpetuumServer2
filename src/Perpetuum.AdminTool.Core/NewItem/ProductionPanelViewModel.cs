using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Packages;

namespace Perpetuum.AdminTool.NewItem;

public partial class ProductionPanelViewModel : ObservableObject
{
    private IReadOnlyDictionary<long, double> _existingProductionDurations = new Dictionary<long, double>();

    public ObservableCollection<NewComponentRow> Components { get; } = new();

    [ObservableProperty] private IReadOnlyList<PackageItemPickItem> _availableIngredients = [];
    [ObservableProperty] private bool _hasExistingProductionDuration;
    [ObservableProperty] private double _existingDurationValue;
    [ObservableProperty] private double _durationModifier = 1.0;

    public bool ShouldWriteProductionDuration => !HasExistingProductionDuration;

    public void Initialize(NewItemLookups lookups)
    {
        AvailableIngredients = lookups.EnabledItems;
        _existingProductionDurations = lookups.ExistingProductionDurations;
    }

    public void UpdateCategory(long categoryFlags)
    {
        if (_existingProductionDurations.TryGetValue(categoryFlags, out var dur))
        {
            HasExistingProductionDuration = true;
            ExistingDurationValue = dur;
        }
        else
        {
            HasExistingProductionDuration = false;
        }
    }

    [RelayCommand] private void AddComponent() => Components.Add(new NewComponentRow());
    [RelayCommand] private void RemoveComponent(NewComponentRow row) => Components.Remove(row);

    public void LoadFromClone(IEnumerable<(int ComponentDef, int Amount)> cloneComponents)
    {
        Components.Clear();
        foreach (var (def, amt) in cloneComponents)
            Components.Add(new NewComponentRow { IngredientDefinition = def, Amount = amt, OriginalAmount = amt });
    }

    public bool HasDuplicateIngredients()
    {
        var ids = Components.Select(c => c.IngredientDefinition).ToList();
        return ids.Count != ids.Distinct().Count();
    }
}
