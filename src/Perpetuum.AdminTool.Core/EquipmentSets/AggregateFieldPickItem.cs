namespace Perpetuum.AdminTool.EquipmentSets
{
    public class AggregateFieldPickItem
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string TranslatedName { get; init; } = "";

        public string Display => string.IsNullOrEmpty(TranslatedName) ? Name : TranslatedName;
    }
}
