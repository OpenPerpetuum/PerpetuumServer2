namespace Perpetuum.AdminTool.Common
{
    public class TemplatePickItem
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string Display => $"{Id} — {Name}";
    }
}
