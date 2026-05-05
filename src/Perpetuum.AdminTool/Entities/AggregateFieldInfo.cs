namespace Perpetuum.AdminTool.Entities
{
    public class AggregateFieldInfo
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public int Formula { get; init; }
        public string MeasurementUnit { get; init; } = "";
        public double MeasurementMultiplier { get; init; }
        public double MeasurementOffset { get; init; }
        public int Category { get; init; }
        public int Digits { get; init; }

        public string DisplayLabel =>
            string.IsNullOrEmpty(Name)
                ? $"#{Id}"
                : $"{Name} (#{Id})";
    }
}
