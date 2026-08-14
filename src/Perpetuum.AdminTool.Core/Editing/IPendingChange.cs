namespace Perpetuum.AdminTool.Editing
{
    public interface IPendingChange
    {
        /// <summary>A human-readable summary shown before export or execution.</summary>
        string Description { get; }
        string ToSql();
        bool IsDestructive => false;
    }
}
