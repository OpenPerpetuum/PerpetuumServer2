namespace Perpetuum.AdminTool.NewItem;

public record DefinitionConfigColumnInfo(string Name, string SqlType)
{
    public bool IsFloat => SqlType.StartsWith("float", StringComparison.OrdinalIgnoreCase);
    public bool IsInt => SqlType.StartsWith("int", StringComparison.OrdinalIgnoreCase);
    public bool IsBit => string.Equals(SqlType, "bit", StringComparison.OrdinalIgnoreCase);
    public bool IsVarchar => SqlType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase)
                          || SqlType.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase);
}
