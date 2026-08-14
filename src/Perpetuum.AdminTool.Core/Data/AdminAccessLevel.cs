namespace Perpetuum.AdminTool.Data
{
    /// <summary>
    /// Administrative account roles stored in dbo.accounts.accLevel.
    /// Kept local to the AdminTool so its portable core does not depend on the
    /// platform-specific game-server assembly merely to interpret login data.
    /// </summary>
    [Flags]
    public enum AdminAccessLevel : uint
    {
        NotDefined = 0,
        Normal = 1 << 1,
        GameAdmin = 1 << 2 | Normal,
        ToolAdmin = 1 << 3 | GameAdmin,
        Owner = 1 << 4 | ToolAdmin,
        AllAdmin = ToolAdmin | GameAdmin
    }
}
