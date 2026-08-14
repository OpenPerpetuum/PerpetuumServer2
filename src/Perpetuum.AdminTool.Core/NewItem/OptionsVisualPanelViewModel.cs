using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Perpetuum.AdminTool.NewItem;

public partial class OptionsVisualPanelViewModel : ObservableObject
{
    [ObservableProperty] private string _optionsText = "";
    [ObservableProperty] private string? _cloneOptionsText;
    [ObservableProperty] private bool _hasDefinitionConfig;
    [ObservableProperty] private IReadOnlyList<DefinitionConfigColumnInfo> _availableConfigColumns = [];

    public ObservableCollection<DefinitionConfigRow> DefinitionConfigRows { get; } = new();

    public void Initialize(NewItemLookups lookups)
    {
        AvailableConfigColumns = lookups.DefinitionConfigColumns;
    }

    [RelayCommand] private void AddConfigRow() => DefinitionConfigRows.Add(new DefinitionConfigRow());
    [RelayCommand] private void RemoveConfigRow(DefinitionConfigRow row) => DefinitionConfigRows.Remove(row);

    public void LoadFromClone(string? options, IReadOnlyDictionary<string, string?> configValues)
    {
        OptionsText = options ?? "";
        CloneOptionsText = options;

        DefinitionConfigRows.Clear();
        if (configValues.Count > 0)
        {
            HasDefinitionConfig = true;
            foreach (var (col, val) in configValues)
                if (val != null)
                    DefinitionConfigRows.Add(new DefinitionConfigRow
                    {
                        ColumnName = col, RawValue = val, OriginalValue = val
                    });
        }
    }

    public bool HasDuplicateConfigColumns()
    {
        var cols = DefinitionConfigRows.Select(r => r.ColumnName).ToList();
        return cols.Count != cols.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    public string? ValidateTintValues()
    {
        foreach (var row in DefinitionConfigRows)
        {
            if (row.ColumnName == "tint" && !string.IsNullOrEmpty(row.RawValue))
            {
                if (!Regex.IsMatch(row.RawValue, @"^#[0-9A-Fa-f]{6}$"))
                    return $"tint must be #RRGGBB, got: {row.RawValue}";
            }
        }
        return null;
    }
}
