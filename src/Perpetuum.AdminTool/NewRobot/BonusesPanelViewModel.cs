using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.NewItem;

namespace Perpetuum.AdminTool.NewRobot;

public partial class BonusesPanelViewModel : ObservableObject
{
    [ObservableProperty] private IReadOnlyList<ExtensionPickItem> _availableExtensions = [];
    [ObservableProperty] private IReadOnlyList<AggregateFieldInfo> _availableFields = [];

    public ObservableCollection<NewBonusRow> Rows { get; } = new();

    public void Initialize(NewItemLookups lookups, Dictionary<string, string>? englishNames)
    {
        AvailableExtensions = lookups.Extensions
            .Select(e =>
            {
                var display = (englishNames != null && englishNames.TryGetValue(e.Name, out var eng) && !string.IsNullOrEmpty(eng))
                    ? eng : e.Name;
                return new ExtensionPickItem(e.Id, display);
            })
            .ToList();
        AvailableFields = lookups.AggregateFields;
    }

    [RelayCommand]
    private void AddRow() => Rows.Add(new NewBonusRow());

    [RelayCommand]
    private void RemoveRow(NewBonusRow row) => Rows.Remove(row);

    public void LoadFromClone(IEnumerable<ChassisBonusRow> rows)
    {
        Rows.Clear();
        foreach (var r in rows)
            Rows.Add(new NewBonusRow
            {
                ExtensionId = r.ExtensionId,
                NewBonus = r.Bonus,
                OriginalBonus = r.Bonus,
                TargetPropertyId = r.TargetPropertyId,
                EffectEnhancer = r.EffectEnhancer,
                Note = r.Note ?? ""
            });
    }

    public bool HasDuplicates()
    {
        var keys = Rows.Select(r => (r.ExtensionId, r.TargetPropertyId)).ToList();
        return keys.Count != keys.Distinct().Count();
    }
}
