namespace Perpetuum.AdminTool.Editing
{
    public interface IPendingChange
    {
        string Description { get; }
        string ToSql();
        bool IsDestructive => false;
    }
}
