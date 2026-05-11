using CommunityToolkit.Mvvm.ComponentModel;
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
    }
}
