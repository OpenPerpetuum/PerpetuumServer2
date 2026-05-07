using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Common;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AddFlockRowViewModel : ObservableObject
    {
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private int _presenceId;
        [ObservableProperty] private int _flockMemberCount = 1;
        [ObservableProperty] private int _definition;
        [ObservableProperty] private int _spawnOriginX;
        [ObservableProperty] private int _spawnOriginY;
        [ObservableProperty] private int _spawnRangeMin;
        [ObservableProperty] private int _spawnRangeMax = 10;
        [ObservableProperty] private int _respawnSeconds = 1;
        [ObservableProperty] private int _totalSpawnCount;
        [ObservableProperty] private int _homeRange = 70;
        [ObservableProperty] private string? _note;
        [ObservableProperty] private double _respawnMultiplierLow = 0.9;
        [ObservableProperty] private bool _enabled = true;
        [ObservableProperty] private bool _isCallForHelp = true;
        [ObservableProperty] private int _behaviorType = 1;
        [ObservableProperty] private int _npcSpecialType;
        [ObservableProperty] private string _errorMessage = "";

        public LookupCache Lookups { get; }
        public ObservableCollection<PresencePickItem> PresencePicks { get; } = new();

        public AddFlockRowViewModel(LookupCache lookups)
        {
            Lookups = lookups;
        }
    }
}
