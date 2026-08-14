using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.NewItem;

public partial class PropertyModifiersPanelViewModel : ObservableObject
{
    public ObservableCollection<PropertyModifierRow> ModulePropertyModifierRows { get; } = new();
    public ObservableCollection<PropertyModifierRow> AggregateModifierRows { get; } = new();

    [ObservableProperty] private IReadOnlyList<(long CategoryFlags, int BaseField, int ModifierField)> _existingModPropertyModifiers = [];
    [ObservableProperty] private IReadOnlyList<(long CategoryFlags, int BaseField, int ModifierField)> _existingAggregateModifiers = [];
    [ObservableProperty] private IReadOnlyList<AggregateFieldInfo> _availableFields = [];

    public void Initialize(NewItemLookups lookups)
    {
        AvailableFields = lookups.AggregateFields;
        ExistingModPropertyModifiers = lookups.ExistingModPropertyModifiers;
        ExistingAggregateModifiers = lookups.ExistingAggregateModifiers;
    }

    [RelayCommand] private void AddModPropertyRow() => ModulePropertyModifierRows.Add(new PropertyModifierRow());
    [RelayCommand] private void RemoveModPropertyRow(PropertyModifierRow row) => ModulePropertyModifierRows.Remove(row);
    [RelayCommand] private void AddAggModRow() => AggregateModifierRows.Add(new PropertyModifierRow());
    [RelayCommand] private void RemoveAggModRow(PropertyModifierRow row) => AggregateModifierRows.Remove(row);

    public void LoadFromClone(long categoryFlags)
    {
        ModulePropertyModifierRows.Clear();
        AggregateModifierRows.Clear();
        foreach (var (cf, bf, mf) in ExistingModPropertyModifiers.Where(e => e.CategoryFlags == categoryFlags))
            ModulePropertyModifierRows.Add(new PropertyModifierRow { BaseFieldId = bf, ModifierFieldId = mf, OriginalBaseFieldId = bf, OriginalModifierFieldId = mf });
        foreach (var (cf, bf, mf) in ExistingAggregateModifiers.Where(e => e.CategoryFlags == categoryFlags))
            AggregateModifierRows.Add(new PropertyModifierRow { BaseFieldId = bf, ModifierFieldId = mf, OriginalBaseFieldId = bf, OriginalModifierFieldId = mf });
    }
}
