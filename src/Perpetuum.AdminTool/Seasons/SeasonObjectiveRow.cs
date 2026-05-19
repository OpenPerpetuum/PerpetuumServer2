using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Packages;
using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonObjectiveRow : ObservableObject
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _description = "";
        [ObservableProperty] private SeasonActivityType _activityType = SeasonActivityType.NpcKill;
        [ObservableProperty] private long _targetValue;
        [ObservableProperty] private int _bonusPoints;
        [ObservableProperty] private int _displayOrder;
        [ObservableProperty] private bool _isDaily;
        [ObservableProperty] private int? _packageId;
        [ObservableProperty] private PackageRow? _selectedPackage;

        partial void OnSelectedPackageChanged(PackageRow? value)
        {
            PackageId = value?.Id;
        }

        [ObservableProperty] private int? _targetDefinitionId;
        [ObservableProperty] private string? _targetDisplayName;

        private IReadOnlyList<MaterialPickItem> _oreAndLiquidMaterials = Array.Empty<MaterialPickItem>();
        private IReadOnlyList<MaterialPickItem> _organicMaterials = Array.Empty<MaterialPickItem>();

        [ObservableProperty] private IReadOnlyList<MaterialPickItem> _availableMaterials = Array.Empty<MaterialPickItem>();

        public void InitializeMaterialLists(
            IReadOnlyList<MaterialPickItem> oreAndLiquid,
            IReadOnlyList<MaterialPickItem> organics)
        {
            _oreAndLiquidMaterials = oreAndLiquid;
            _organicMaterials = organics;
            RefreshAvailableMaterials();
        }

        partial void OnActivityTypeChanged(SeasonActivityType value) => RefreshAvailableMaterials();

        partial void OnTargetDefinitionIdChanged(int? value)
        {
            TargetDisplayName = AvailableMaterials
                .FirstOrDefault(m => m.Definition == value)?.DisplayName;
        }

        public bool HasTargetMaterials => AvailableMaterials.Count > 0;

        private void RefreshAvailableMaterials()
        {
            AvailableMaterials = ActivityType switch
            {
                SeasonActivityType.MineralMined   => _oreAndLiquidMaterials,
                SeasonActivityType.PlantHarvested => _organicMaterials,
                _                                 => Array.Empty<MaterialPickItem>()
            };
            if (TargetDefinitionId.HasValue &&
                !AvailableMaterials.Any(m => m.Definition == TargetDefinitionId))
                TargetDefinitionId = null;
            OnPropertyChanged(nameof(HasTargetMaterials));
        }
    }
}
