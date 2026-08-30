using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Common;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AddPresenceRowViewModel : ObservableObject
    {
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private int _topX;
        [ObservableProperty] private int _topY;
        [ObservableProperty] private int _bottomX;
        [ObservableProperty] private int _bottomY;
        [ObservableProperty] private string? _note;
        [ObservableProperty] private int? _spawnId;
        [ObservableProperty] private bool _enabled = true;
        [ObservableProperty] private bool _roaming;
        [ObservableProperty] private int _roamingRespawnSeconds;
        [ObservableProperty] private int _presenceType;
        [ObservableProperty] private int? _maxRandomFlock;
        [ObservableProperty] private int? _randomCenterX;
        [ObservableProperty] private int? _randomCenterY;
        [ObservableProperty] private int? _randomRadius;
        [ObservableProperty] private int? _dynamicLifetime;
        [ObservableProperty] private bool _isBodyPull = true;
        [ObservableProperty] private bool _isRespawnAllowed = true;
        [ObservableProperty] private bool _safeBodyPull;
        [ObservableProperty] private int? _izGroupId;
        [ObservableProperty] private int? _growthSeconds;
        [ObservableProperty] private string _errorMessage = "";

        public ObservableCollection<ZoneSpawnPickItem> ZoneSpawnPicks { get; } = new();
    }
}
