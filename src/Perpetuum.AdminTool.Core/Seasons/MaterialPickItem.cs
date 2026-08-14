namespace Perpetuum.AdminTool.Seasons
{
    public record MaterialPickItem(int Definition, string DisplayName)
    {
        public string Display => $"{Definition} — {DisplayName}";
    }
}
