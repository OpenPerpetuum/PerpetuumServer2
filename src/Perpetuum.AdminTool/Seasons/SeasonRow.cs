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
        [ObservableProperty] private bool _isRecurring;
        [ObservableProperty] private int? _recurrenceGapDays;
        [ObservableProperty] private int _recurrenceIteration = 1;
        [ObservableProperty] private string? _recurrenceBaseName;

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
            IsRecurring = s.IsRecurring;
            RecurrenceGapDays = s.RecurrenceGapDays;
            RecurrenceIteration = s.RecurrenceIteration;
            RecurrenceBaseName = s.RecurrenceBaseName;
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
                IsActive = IsActive,
                IsRecurring = IsRecurring,
                RecurrenceGapDays = RecurrenceGapDays,
                RecurrenceIteration = RecurrenceIteration,
                RecurrenceBaseName = RecurrenceBaseName
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
        public bool IsRecurring { get; init; }
        public int? RecurrenceGapDays { get; init; }
        public int RecurrenceIteration { get; init; } = 1;
        public string? RecurrenceBaseName { get; init; }
    }

    public enum SeasonCardState { Active, Draft, Ended }
}
