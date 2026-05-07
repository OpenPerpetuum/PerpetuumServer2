using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Entities
{
    public partial class EntityDefaultRow : ObservableObject
    {
        // Negative placeholder ids for in-memory new rows (real ids come from SQL identity).
        private static int _placeholderCounter = 0;

        // Definition is the primary key; not editable.
        // For new rows it is a negative placeholder until SQL Server assigns the real id.
        public int Definition { get; }

        // True for rows created in-memory. The autoincrement id is unknown until insert applies,
        // so this stays true even after Save until the user Reloads from DB.
        public bool IsNew { get; set; }

        // Suffix for SCOPE_IDENTITY-capturing T-SQL variable names. Stable per new row.
        public string NewIdToken { get; init; }

        // Set to true after Save queues this new row; locks the row from further edits.
        [ObservableProperty] private bool _isQueued;

        partial void OnIsQueuedChanged(bool value)
        {
            OnPropertyChanged(nameof(DefinitionDisplay));
        }

        public string DefinitionDisplay =>
            IsNew
                ? (IsQueued
                    ? "(pending insert — reload after commit)"
                    : "(autoincrement on save)")
                : Definition.ToString();

        // Snapshot of the row as loaded from DB; used for diffing.
        public EntityDefaultSnapshot Original { get; private set; }

        [ObservableProperty] private string _definitionName = "";
        [ObservableProperty] private string? _descriptionToken;
        [ObservableProperty] private long _categoryFlags;
        [ObservableProperty] private long _attributeFlags;
        [ObservableProperty] private double _mass;
        [ObservableProperty] private double _volume;
        [ObservableProperty] private double _health;
        [ObservableProperty] private int _quantity;
        [ObservableProperty] private bool _hidden;
        [ObservableProperty] private bool _purchasable;
        [ObservableProperty] private bool _enabled = true;
        [ObservableProperty] private int? _tierType;
        [ObservableProperty] private int? _tierLevel;
        [ObservableProperty] private string? _options; // raw Genxy string; read-only in 3a

        // Stats live alongside the row; loaded from aggregatevalues.
        public ObservableCollection<StatRow> Stats { get; } = new();

        // Stats that existed in DB at load (definition, field) → original value.
        // Used to determine UPDATE vs INSERT and to detect deletions.
        public System.Collections.Generic.Dictionary<int, double> OriginalStats { get; } = new();

        public EntityDefaultRow(EntityDefaultSnapshot snapshot)
        {
            Definition = snapshot.Definition;
            NewIdToken = "";
            Original = snapshot;
            ApplySnapshot(snapshot);
        }

        public static EntityDefaultRow CreateNew(string definitionName)
        {
            var placeholderDef = System.Threading.Interlocked.Decrement(ref _placeholderCounter);
            var snapshot = new EntityDefaultSnapshot
            {
                Definition = placeholderDef,
                DefinitionName = definitionName,
                Mass = 1.0,
                Volume = 1.0,
                Health = 100.0,
                Quantity = 1,
                Enabled = true
            };
            var row = new EntityDefaultRow(snapshot)
            {
                IsNew = true,
                NewIdToken = System.Guid.NewGuid().ToString("N").Substring(0, 8)
            };
            return row;
        }

        public void ApplySnapshot(EntityDefaultSnapshot snapshot)
        {
            Original = snapshot;
            DefinitionName = snapshot.DefinitionName;
            DescriptionToken = snapshot.DescriptionToken;
            CategoryFlags = snapshot.CategoryFlags;
            AttributeFlags = snapshot.AttributeFlags;
            Mass = snapshot.Mass;
            Volume = snapshot.Volume;
            Health = snapshot.Health;
            Quantity = snapshot.Quantity;
            Hidden = snapshot.Hidden;
            Purchasable = snapshot.Purchasable;
            Enabled = snapshot.Enabled;
            TierType = snapshot.TierType;
            TierLevel = snapshot.TierLevel;
            Options = snapshot.Options;
        }

        public void RefreshOriginalFromCurrent()
        {
            Original = new EntityDefaultSnapshot
            {
                Definition = Definition,
                DefinitionName = DefinitionName,
                DescriptionToken = DescriptionToken,
                CategoryFlags = CategoryFlags,
                AttributeFlags = AttributeFlags,
                Mass = Mass,
                Volume = Volume,
                Health = Health,
                Quantity = Quantity,
                Hidden = Hidden,
                Purchasable = Purchasable,
                Enabled = Enabled,
                TierType = TierType,
                TierLevel = TierLevel,
                Options = Options
            };
        }
    }

    public class EntityDefaultSnapshot
    {
        public int Definition { get; init; }
        public string DefinitionName { get; init; } = "";
        public string? DescriptionToken { get; init; }
        public long CategoryFlags { get; init; }
        public long AttributeFlags { get; init; }
        public double Mass { get; init; }
        public double Volume { get; init; }
        public double Health { get; init; }
        public int Quantity { get; init; }
        public bool Hidden { get; init; }
        public bool Purchasable { get; init; }
        public bool Enabled { get; init; } = true;
        public int? TierType { get; init; }
        public int? TierLevel { get; init; }
        public string? Options { get; init; }
    }
}
