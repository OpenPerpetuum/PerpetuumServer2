namespace Perpetuum.AdminTool.EquipmentSets
{
    public class EquipmentSetMemberRow
    {
        public int SetId { get; init; }
        public int Definition { get; init; }
        public string DefinitionName { get; init; } = "";
        public string TranslatedName { get; init; } = "";
    }
}
