using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonRow : ObservableObject
    {
        public int Id { get; }
        public bool IsNew { get; set; }
        public SeasonSnapshot Original { get; private set; }

        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _description = "";
        [ObservableProperty] private DateTime _startTime;
        [ObservableProperty] private DateTime _endTime;
        [ObservableProperty] private bool _isActive;

        public SeasonRow(SeasonSnapshot snapshot)
        {
            Id = snapshot.Id;
            Original = snapshot;
            ApplySnapshot(snapshot);
        }

        public void ApplySnapshot(SeasonSnapshot s)
        {
            Original = s;
            Name = s.Name;
            Description = s.Description;
            StartTime = s.StartTime;
            EndTime = s.EndTime;
            IsActive = s.IsActive;
        }

        public void RefreshOriginalFromCurrent()
        {
            Original = new SeasonSnapshot
            {
                Id = Id,
                Name = Name,
                Description = Description,
                StartTime = StartTime,
                EndTime = EndTime,
                IsActive = IsActive
            };
        }

        public static SeasonRow CreateNew(SeasonSnapshot seed) => new SeasonRow(seed) { IsNew = true };

        public SeasonCardState CardState
        {
            get
            {
                if (IsActive) return SeasonCardState.Active;
                return EndTime > DateTime.UtcNow ? SeasonCardState.Draft : SeasonCardState.Ended;
            }
        }
    }

    public class SeasonSnapshot
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public DateTime StartTime { get; init; }
        public DateTime EndTime { get; init; }
        public bool IsActive { get; init; }
    }

    public enum SeasonCardState { Active, Draft, Ended }
}
