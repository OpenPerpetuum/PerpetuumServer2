using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Common;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AddTemplateRelationRowViewModel : ObservableObject
    {
        [ObservableProperty] private int _definition;
        [ObservableProperty] private int _templateId;
        [ObservableProperty] private int _itemScoreSum;
        [ObservableProperty] private int _raceId;
        [ObservableProperty] private int? _missionLevel;
        [ObservableProperty] private int? _missionLevelOverride;
        [ObservableProperty] private int? _killEp;
        [ObservableProperty] private string? _note;
        [ObservableProperty] private string _errorMessage = "";

        public LookupCache Lookups { get; }

        public AddTemplateRelationRowViewModel(LookupCache lookups)
        {
            Lookups = lookups;
        }
    }
}
