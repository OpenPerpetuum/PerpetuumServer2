using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Templates
{
    public partial class RobotTemplateRow : ObservableObject
    {
        private static int _placeholderCounter = 0;

        public int Id { get; }
        public bool IsNew { get; set; }
        public string NewIdToken { get; init; } = "";

        [ObservableProperty] private bool _isQueued;
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _description = "";
        [ObservableProperty] private string? _note;

        public RobotTemplateSnapshot Original { get; private set; }

        partial void OnIsQueuedChanged(bool value) =>
            OnPropertyChanged(nameof(IdDisplay));

        public string IdDisplay =>
            IsNew
                ? (IsQueued ? "(pending insert — reload after commit)" : "(autoincrement on save)")
                : Id.ToString();

        public RobotTemplateRow(RobotTemplateSnapshot snapshot)
        {
            Id = snapshot.Id;
            Original = snapshot;
            ApplySnapshot(snapshot);
        }

        public void ApplySnapshot(RobotTemplateSnapshot snapshot)
        {
            Original = snapshot;
            Name = snapshot.Name;
            Description = snapshot.Description;
            Note = snapshot.Note;
        }

        public void RefreshOriginalFromCurrent()
        {
            Original = new RobotTemplateSnapshot
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Note = Note
            };
        }

        public static RobotTemplateRow CreateNew(string name)
        {
            var placeholder = System.Threading.Interlocked.Decrement(ref _placeholderCounter);
            var snap = new RobotTemplateSnapshot
            {
                Id = placeholder,
                Name = name,
                Description = "",
                Note = null
            };
            return new RobotTemplateRow(snap)
            {
                IsNew = true,
                NewIdToken = System.Guid.NewGuid().ToString("N").Substring(0, 8)
            };
        }
    }

    public class RobotTemplateSnapshot
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string? Note { get; init; }
    }
}
