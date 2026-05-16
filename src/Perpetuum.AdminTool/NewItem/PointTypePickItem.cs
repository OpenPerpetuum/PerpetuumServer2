namespace Perpetuum.AdminTool.NewItem;

public record PointTypePickItem(int Id, string Name)
{
    public string Display => Name;
}
