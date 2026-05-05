using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Entities
{
    public partial class EntityDefaultRow : ObservableObject
    {
        // Definition is the primary key; not editable.
        public int Definition { get; }

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
            Original = snapshot;
            ApplySnapshot(snapshot);
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
        public int? TierType { get; init; }
        public int? TierLevel { get; init; }
        public string? Options { get; init; }
    }
}
