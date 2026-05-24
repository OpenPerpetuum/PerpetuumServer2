using Perpetuum.ExportedTypes;

namespace Perpetuum.Robots.EquipmentSets
{
    public readonly struct SetBonusThreshold
    {
        public SetBonusThreshold(int requiredPieces, AggregateField field, double value)
        {
            RequiredPieces = requiredPieces;
            Field = field;
            Value = value;
        }

        public int RequiredPieces { get; }
        public AggregateField Field { get; }
        public double Value { get; }
    }
}
