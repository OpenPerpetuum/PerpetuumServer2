using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Npc
{
    public partial class PresenceRow : ObservableObject
    {
        // PK is the identity column `id`. 0 means a new (unsaved) row.
        public int Id { get; }

        public bool IsNew { get; set; }
        public PresenceSnapshot Original { get; private set; }

        // Resolved display label (not persisted) — kept in sync with current SpawnId
        // by PresencesViewModel using the zones pick list.
        [ObservableProperty] private string _spawnName = "";

        [ObservableProperty] private string _name = "";
        [ObservableProperty] private int _topX;
        [ObservableProperty] private int _topY;
        [ObservableProperty] private int _bottomX;
        [ObservableProperty] private int _bottomY;
        [ObservableProperty] private string? _note;
        [ObservableProperty] private int? _spawnId;
        [ObservableProperty] private bool _enabled;
        [ObservableProperty] private bool _roaming;
        [ObservableProperty] private int _roamingRespawnSeconds;
        [ObservableProperty] private int _presenceType;
        [ObservableProperty] private int? _maxRandomFlock;
        [ObservableProperty] private int? _randomCenterX;
        [ObservableProperty] private int? _randomCenterY;
        [ObservableProperty] private int? _randomRadius;
        [ObservableProperty] private int? _dynamicLifetime;
        [ObservableProperty] private bool _isBodyPull;
        [ObservableProperty] private bool _isRespawnAllowed;
        [ObservableProperty] private bool _safeBodyPull;
        [ObservableProperty] private int? _izGroupId;
        [ObservableProperty] private int? _growthSeconds;

        public PresenceRow(PresenceSnapshot snapshot)
        {
            Id = snapshot.Id;
            Original = snapshot;
            ApplySnapshot(snapshot);
        }

        public void ApplySnapshot(PresenceSnapshot s)
        {
            Original = s;
            Name = s.Name;
            TopX = s.TopX;
            TopY = s.TopY;
            BottomX = s.BottomX;
            BottomY = s.BottomY;
            Note = s.Note;
            SpawnId = s.SpawnId;
            Enabled = s.Enabled;
            Roaming = s.Roaming;
            RoamingRespawnSeconds = s.RoamingRespawnSeconds;
            PresenceType = s.PresenceType;
            MaxRandomFlock = s.MaxRandomFlock;
            RandomCenterX = s.RandomCenterX;
            RandomCenterY = s.RandomCenterY;
            RandomRadius = s.RandomRadius;
            DynamicLifetime = s.DynamicLifetime;
            IsBodyPull = s.IsBodyPull;
            IsRespawnAllowed = s.IsRespawnAllowed;
            SafeBodyPull = s.SafeBodyPull;
            IzGroupId = s.IzGroupId;
            GrowthSeconds = s.GrowthSeconds;
        }

        public void RefreshOriginalFromCurrent()
        {
            Original = new PresenceSnapshot
            {
                Id = Id,
                Name = Name,
                TopX = TopX,
                TopY = TopY,
                BottomX = BottomX,
                BottomY = BottomY,
                Note = Note,
                SpawnId = SpawnId,
                Enabled = Enabled,
                Roaming = Roaming,
                RoamingRespawnSeconds = RoamingRespawnSeconds,
                PresenceType = PresenceType,
                MaxRandomFlock = MaxRandomFlock,
                RandomCenterX = RandomCenterX,
                RandomCenterY = RandomCenterY,
                RandomRadius = RandomRadius,
                DynamicLifetime = DynamicLifetime,
                IsBodyPull = IsBodyPull,
                IsRespawnAllowed = IsRespawnAllowed,
                SafeBodyPull = SafeBodyPull,
                IzGroupId = IzGroupId,
                GrowthSeconds = GrowthSeconds
            };
        }

        public static PresenceRow CreateNew(PresenceSnapshot seed)
        {
            return new PresenceRow(seed) { IsNew = true };
        }
    }

    public class PresenceSnapshot
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public int TopX { get; init; }
        public int TopY { get; init; }
        public int BottomX { get; init; }
        public int BottomY { get; init; }
        public string? Note { get; init; }
        public int? SpawnId { get; init; }
        public bool Enabled { get; init; }
        public bool Roaming { get; init; }
        public int RoamingRespawnSeconds { get; init; }
        public int PresenceType { get; init; }
        public int? MaxRandomFlock { get; init; }
        public int? RandomCenterX { get; init; }
        public int? RandomCenterY { get; init; }
        public int? RandomRadius { get; init; }
        public int? DynamicLifetime { get; init; }
        public bool IsBodyPull { get; init; }
        public bool IsRespawnAllowed { get; init; }
        public bool SafeBodyPull { get; init; }
        public int? IzGroupId { get; init; }
        public int? GrowthSeconds { get; init; }
    }
}
