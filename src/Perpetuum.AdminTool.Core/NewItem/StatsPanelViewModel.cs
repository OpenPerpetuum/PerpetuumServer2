using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.NewItem;

public partial class StatsPanelViewModel : ObservableObject
{
    [ObservableProperty] private IReadOnlyList<AggregateFieldInfo> _availableFields = [];

    public ObservableCollection<NewStatRow> Rows { get; } = new();

    public void Initialize(NewItemLookups lookups)
    {
        AvailableFields = lookups.AggregateFields;
    }

    [RelayCommand]
    private void AddRow() => Rows.Add(new NewStatRow());

    [RelayCommand]
    private void RemoveRow(NewStatRow row) => Rows.Remove(row);

    public void LoadFromClone(IEnumerable<StatRow> cloneStats)
    {
        Rows.Clear();
        foreach (var s in cloneStats)
            Rows.Add(new NewStatRow { FieldId = (int)s.Field, NewValue = s.Value, OriginalValue = s.Value });
    }

    public bool HasDuplicateFields()
    {
        var ids = Rows.Select(r => r.FieldId).ToList();
        return ids.Count != ids.Distinct().Count();
    }
}
