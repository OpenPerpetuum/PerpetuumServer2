using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.NewItem;

public partial class BasicPanelViewModel : ObservableObject
{
    private readonly BasicPanelMode _mode;
    private readonly IReadOnlyCollection<string> _existingDefNames;

    public BasicPanelMode Mode => _mode;

    [ObservableProperty] private string _definitionName = "";
    [ObservableProperty] private long _categoryFlags;
    [ObservableProperty] private long _attributeFlags;
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private bool _purchasable;
    [ObservableProperty] private bool _hidden;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private double _mass;
    [ObservableProperty] private double _volume;
    [ObservableProperty] private double _health = 100.0;
    [ObservableProperty] private int? _tierType;
    [ObservableProperty] private int? _tierLevel;
    [ObservableProperty] private string _descriptionToken = "";
    [ObservableProperty] private string _note = "";

    // Only active in Main mode; gate tabs 2, 3, 6, 7
    [ObservableProperty] private bool _isCraftable;
    // Only active in Main mode; gates tab 3
    [ObservableProperty] private bool _hasPrototype;

    // Clone source original values for display (null if no clone)
    public EntityDefaultRow? CloneSource { get; private set; }

    public string? DefinitionNameError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DefinitionName)) return "Required";
            if (!DefinitionName.StartsWith("def_", StringComparison.Ordinal)) return "Must start with def_";
            if (_mode == BasicPanelMode.CalibrationTemplate && !DefinitionName.EndsWith("_cprg", StringComparison.Ordinal))
                return "Must end with _cprg";
            if (_mode == BasicPanelMode.Prototype && !DefinitionName.EndsWith("_pr", StringComparison.Ordinal))
                return "Must end with _pr";
            if (_existingDefNames.Contains(DefinitionName)) return "Name already exists";
            return null;
        }
    }

    public bool HasErrors =>
        DefinitionNameError != null
        || (_mode == BasicPanelMode.Main && CategoryFlags == 0);

    public BasicPanelViewModel(BasicPanelMode mode, IReadOnlyCollection<string> existingDefNames)
    {
        _mode = mode;
        _existingDefNames = existingDefNames;

        Purchasable = mode switch
        {
            BasicPanelMode.CalibrationTemplate => false,
            _ => true
        };
    }

    // Called by the dialog VM when BasicPanel.DefinitionName changes
    public void SuggestName(string mainDefinitionName, string suffix)
    {
        var stripped = mainDefinitionName.StartsWith("def_", StringComparison.Ordinal)
            ? mainDefinitionName : "def_" + mainDefinitionName;
        DefinitionName = stripped + suffix;
    }

    public void LoadFromClone(EntityDefaultRow source, string nameSuffix = "")
    {
        CloneSource = source;
        var baseName = source.DefinitionName.StartsWith("def_", StringComparison.Ordinal)
            ? source.DefinitionName : "def_" + source.DefinitionName;
        DefinitionName = baseName + nameSuffix;
        CategoryFlags = source.CategoryFlags;
        AttributeFlags = source.AttributeFlags;
        Enabled = source.Enabled;
        Hidden = source.Hidden;
        Quantity = source.Quantity;
        Mass = source.Mass;
        Volume = source.Volume;
        Health = source.Health;
        TierType = source.TierType;
        TierLevel = source.TierLevel;
        if (_mode != BasicPanelMode.CalibrationTemplate)
            Purchasable = source.Purchasable;

        OnPropertyChanged(nameof(CloneSource));
    }

    partial void OnDefinitionNameChanged(string value)
    {
        DescriptionToken = SuggestDescriptionToken(value);
        OnPropertyChanged(nameof(DefinitionNameError));
        OnPropertyChanged(nameof(HasErrors));
    }

    partial void OnCategoryFlagsChanged(long value)
    {
        OnPropertyChanged(nameof(HasErrors));
    }

    private string SuggestDescriptionToken(string defName)
    {
        var stripped = defName.StartsWith("def_", StringComparison.OrdinalIgnoreCase)
            ? defName[4..] : defName;
        if (stripped.EndsWith("_desc", StringComparison.OrdinalIgnoreCase))
            return stripped;
        return stripped + "_desc";
    }
}
