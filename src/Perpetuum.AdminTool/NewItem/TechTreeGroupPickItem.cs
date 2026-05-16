namespace Perpetuum.AdminTool.NewItem;

public record TechTreeGroupPickItem(int Id, string Name)
{
    public string Display => Name;
}
