using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.ExportedTypes;

namespace Perpetuum.AdminTool.Entities
{
    public sealed partial class StatRow : ObservableObject
    {
        public int Definition { get; }

        // Was this row present in the DB at load time?
        // If false, save will INSERT; if true, save will UPDATE on value change.
        public bool WasInDb { get; }

        // Original value at load time (only meaningful when WasInDb is true).
        public double OriginalValue { get; }

        [ObservableProperty] private AggregateField _field;
        [ObservableProperty] private double _value;

        public StatRow(int definition, AggregateField field, double value, bool wasInDb)
        {
            Definition = definition;
            _field = field;
            _value = value;
            WasInDb = wasInDb;
            OriginalValue = wasInDb ? value : 0d;
        }

        public bool IsModified => WasInDb && Value != OriginalValue;
    }
}
