namespace Perpetuum.AdminTool.EquipmentSets
{
    public class SetMemberPickItem
    {
        public int Definition { get; init; }
        public string DefinitionName { get; init; } = "";
        public string TranslatedName { get; init; } = "";

        public string Display => string.IsNullOrEmpty(TranslatedName)
            ? $"{Definition} — {DefinitionName}"
            : $"{Definition} — {DefinitionName}  ({TranslatedName})";
    }
}
