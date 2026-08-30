using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Npc
{
    public partial class FlockRow : ObservableObject
    {
        // PK is the identity column `id`. 0 means a new (unsaved) row.
        public int Id { get; }

        public bool IsNew { get; set; }
        public FlockSnapshot Original { get; private set; }

        // Resolved display labels (not persisted) — kept in sync with current PresenceId / Definition
        // by FlocksViewModel using the pick lists.
        [ObservableProperty] private string _presenceName = "";
        [ObservableProperty] private string _definitionName = "";

        [ObservableProperty] private string _name = "";
        [ObservableProperty] private int _presenceId;
        [ObservableProperty] private int _flockMemberCount;
        [ObservableProperty] private int _definition;
        [ObservableProperty] private int _spawnOriginX;
        [ObservableProperty] private int _spawnOriginY;
        [ObservableProperty] private int _spawnRangeMin;
        [ObservableProperty] private int _spawnRangeMax;
        [ObservableProperty] private int _respawnSeconds;
        [ObservableProperty] private int _totalSpawnCount;
        [ObservableProperty] private int _homeRange;
        [ObservableProperty] private string? _note;
        [ObservableProperty] private double _respawnMultiplierLow;
        [ObservableProperty] private bool _enabled;
        [ObservableProperty] private bool _isCallForHelp;
        [ObservableProperty] private int _behaviorType;
        [ObservableProperty] private int _npcSpecialType;

        public FlockRow(FlockSnapshot snapshot)
        {
            Id = snapshot.Id;
            Original = snapshot;
            ApplySnapshot(snapshot);
        }

        public void ApplySnapshot(FlockSnapshot s)
        {
            Original = s;
            Name = s.Name;
            PresenceId = s.PresenceId;
            FlockMemberCount = s.FlockMemberCount;
            Definition = s.Definition;
            SpawnOriginX = s.SpawnOriginX;
            SpawnOriginY = s.SpawnOriginY;
            SpawnRangeMin = s.SpawnRangeMin;
            SpawnRangeMax = s.SpawnRangeMax;
            RespawnSeconds = s.RespawnSeconds;
            TotalSpawnCount = s.TotalSpawnCount;
            HomeRange = s.HomeRange;
            Note = s.Note;
            RespawnMultiplierLow = s.RespawnMultiplierLow;
            Enabled = s.Enabled;
            IsCallForHelp = s.IsCallForHelp;
            BehaviorType = s.BehaviorType;
            NpcSpecialType = s.NpcSpecialType;
        }

        public void RefreshOriginalFromCurrent()
        {
            Original = new FlockSnapshot
            {
                Id = Id,
                Name = Name,
                PresenceId = PresenceId,
                FlockMemberCount = FlockMemberCount,
                Definition = Definition,
                SpawnOriginX = SpawnOriginX,
                SpawnOriginY = SpawnOriginY,
                SpawnRangeMin = SpawnRangeMin,
                SpawnRangeMax = SpawnRangeMax,
                RespawnSeconds = RespawnSeconds,
                TotalSpawnCount = TotalSpawnCount,
                HomeRange = HomeRange,
                Note = Note,
                RespawnMultiplierLow = RespawnMultiplierLow,
                Enabled = Enabled,
                IsCallForHelp = IsCallForHelp,
                BehaviorType = BehaviorType,
                NpcSpecialType = NpcSpecialType
            };
        }

        public static FlockRow CreateNew(FlockSnapshot seed)
        {
            return new FlockRow(seed) { IsNew = true };
        }
    }

    public class FlockSnapshot
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public int PresenceId { get; init; }
        public int FlockMemberCount { get; init; }
        public int Definition { get; init; }
        public int SpawnOriginX { get; init; }
        public int SpawnOriginY { get; init; }
        public int SpawnRangeMin { get; init; }
        public int SpawnRangeMax { get; init; }
        public int RespawnSeconds { get; init; }
        public int TotalSpawnCount { get; init; }
        public int HomeRange { get; init; }
        public string? Note { get; init; }
        public double RespawnMultiplierLow { get; init; }
        public bool Enabled { get; init; }
        public bool IsCallForHelp { get; init; }
        public int BehaviorType { get; init; }
        public int NpcSpecialType { get; init; }
    }
}
