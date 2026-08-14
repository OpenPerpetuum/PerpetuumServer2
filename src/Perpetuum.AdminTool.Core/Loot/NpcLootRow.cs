using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Loot
{
    public partial class NpcLootRow : ObservableObject
    {
        // PK is the identity column `id`. 0 means a new (unsaved) row.
        public int Id { get; }

        public bool IsNew { get; set; }
        public NpcLootSnapshot Original { get; private set; }

        // Resolved display labels (not persisted) — kept in sync with current Definition / LootDefinition
        // by NpcLootViewModel using the entitydefaults pick list.
        [ObservableProperty] private string _definitionName = "";
        [ObservableProperty] private string _lootDefinitionName = "";

        [ObservableProperty] private int _definition;
        [ObservableProperty] private int _lootDefinition;
        [ObservableProperty] private int _minQuantity;
        [ObservableProperty] private int _quantity;
        [ObservableProperty] private double _probability;
        [ObservableProperty] private bool _dontDamage;
        [ObservableProperty] private bool _repackaged;

        public NpcLootRow(NpcLootSnapshot snapshot)
        {
            Id = snapshot.Id;
            Original = snapshot;
            ApplySnapshot(snapshot);
        }

        public void ApplySnapshot(NpcLootSnapshot snapshot)
        {
            Original = snapshot;
            Definition = snapshot.Definition;
            LootDefinition = snapshot.LootDefinition;
            MinQuantity = snapshot.MinQuantity;
            Quantity = snapshot.Quantity;
            Probability = snapshot.Probability;
            DontDamage = snapshot.DontDamage;
            Repackaged = snapshot.Repackaged;
        }

        public void RefreshOriginalFromCurrent()
        {
            Original = new NpcLootSnapshot
            {
                Id = Id,
                Definition = Definition,
                LootDefinition = LootDefinition,
                MinQuantity = MinQuantity,
                Quantity = Quantity,
                Probability = Probability,
                DontDamage = DontDamage,
                Repackaged = Repackaged
            };
        }

        public static NpcLootRow CreateNew(int definition, int lootDefinition, int minQuantity, int quantity, double probability, bool dontDamage, bool repackaged)
        {
            var snap = new NpcLootSnapshot
            {
                Id = 0,
                Definition = definition,
                LootDefinition = lootDefinition,
                MinQuantity = minQuantity,
                Quantity = quantity,
                Probability = probability,
                DontDamage = dontDamage,
                Repackaged = repackaged
            };
            return new NpcLootRow(snap) { IsNew = true };
        }
    }

    public class NpcLootSnapshot
    {
        public int Id { get; init; }
        public int Definition { get; init; }
        public int LootDefinition { get; init; }
        public int MinQuantity { get; init; }
        public int Quantity { get; init; }
        public double Probability { get; init; }
        public bool DontDamage { get; init; }
        public bool Repackaged { get; init; }
    }
}
