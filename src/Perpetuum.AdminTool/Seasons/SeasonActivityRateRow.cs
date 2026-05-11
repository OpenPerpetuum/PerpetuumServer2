using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonActivityRateRow : ObservableObject
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }

        [ObservableProperty] private SeasonActivityType _activityType;
        [ObservableProperty] private double _pointsPerUnit;
        [ObservableProperty] private int _unitScale = 1;

        public string ActivityTypeLabel => ActivityType switch
        {
            SeasonActivityType.NpcKill         => "NPC Kill",
            SeasonActivityType.PvpKill         => "PvP Kill",
            SeasonActivityType.MissionComplete => "Mission Complete",
            SeasonActivityType.MineralMined    => "Mineral Mined",
            SeasonActivityType.EpSpent         => "EP Spent",
            SeasonActivityType.NicEarned       => "NIC Earned",
            SeasonActivityType.NicSpent        => "NIC Spent",
            SeasonActivityType.IntrusionPoint  => "Intrusion Point",
            _ => ActivityType.ToString()
        };

        public string EffectiveRate => GetEffectiveRateLabel(ActivityType, PointsPerUnit, UnitScale);

        public bool CanQueueSave => Id > 0 || PointsPerUnit > 0;

        partial void OnPointsPerUnitChanged(double value)
        {
            OnPropertyChanged(nameof(EffectiveRate));
            OnPropertyChanged(nameof(CanQueueSave));
        }
        partial void OnUnitScaleChanged(int value) => OnPropertyChanged(nameof(EffectiveRate));
        partial void OnActivityTypeChanged(SeasonActivityType value) => OnPropertyChanged(nameof(EffectiveRate));

        public static string GetEffectiveRateLabel(SeasonActivityType type, double pointsPerUnit, int unitScale)
        {
            if (pointsPerUnit == 0) return "Disabled";

            var pts = pointsPerUnit.ToString("0.##", CultureInfo.InvariantCulture);
            var scale = unitScale.ToString("N0", CultureInfo.InvariantCulture);

            return type switch
            {
                SeasonActivityType.NpcKill => unitScale > 1
                    ? $"{pts} pts per {scale} kills"
                    : $"{pts} pts per kill",
                SeasonActivityType.PvpKill => unitScale > 1
                    ? $"{pts} pts per {scale} kills"
                    : $"{pts} pts per kill",
                SeasonActivityType.MissionComplete => unitScale > 1
                    ? $"{pts} pts per {scale} completions"
                    : $"{pts} pts per completion",
                SeasonActivityType.IntrusionPoint => unitScale > 1
                    ? $"{pts} pts per {scale} intrusion points"
                    : $"{pts} pts per intrusion point",
                SeasonActivityType.MineralMined => unitScale > 1
                    ? $"{pts} pts per {scale} units mined"
                    : $"{pts} pts per unit mined",
                SeasonActivityType.EpSpent => unitScale > 1
                    ? $"{pts} pts per {scale} EP spent"
                    : $"{pts} pts per EP spent",
                SeasonActivityType.NicEarned => unitScale > 1
                    ? $"{pts} pts per {scale} NIC earned"
                    : $"{pts} pts per NIC earned",
                SeasonActivityType.NicSpent => unitScale > 1
                    ? $"{pts} pts per {scale} NIC spent"
                    : $"{pts} pts per NIC spent",
                _ => $"{pts} pts"
            };
        }
    }
}
