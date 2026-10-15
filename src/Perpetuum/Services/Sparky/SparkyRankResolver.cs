namespace Perpetuum.Services.Sparky;

internal sealed class SparkyDiscordRankConfiguration
{
    public SparkyDiscordRankConfiguration(string? officerRoleIds, string? ceoRoleIds, string? commandDirectorRoleIds)
    {
        OfficerRoleIds = ParseRoleIds(officerRoleIds);
        CeoRoleIds = ParseRoleIds(ceoRoleIds);
        CommandDirectorRoleIds = ParseRoleIds(commandDirectorRoleIds);
    }

    internal IReadOnlySet<ulong> OfficerRoleIds { get; }
    internal IReadOnlySet<ulong> CeoRoleIds { get; }
    internal IReadOnlySet<ulong> CommandDirectorRoleIds { get; }

    private static IReadOnlySet<ulong> ParseRoleIds(string? configuredIds)
    {
        var roleIds = new HashSet<ulong>();
        if (string.IsNullOrWhiteSpace(configuredIds))
        {
            return roleIds;
        }

        foreach (var value in configuredIds.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (ulong.TryParse(value, out var roleId) && roleId != 0)
            {
                roleIds.Add(roleId);
            }
        }

        return roleIds;
    }
}

internal static class SparkyRankResolver
{
    public static SparkyCallerRank ResolveGame(AccessLevel accessLevel) =>
        accessLevel.IsAdminOrGm() ? SparkyCallerRank.CommandDirector : SparkyCallerRank.Agent;

    public static SparkyCallerRank ResolveDiscord(
        bool isGuildMember,
        bool hasAdministratorPermission,
        IEnumerable<ulong>? roleIds,
        SparkyDiscordRankConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!isGuildMember)
        {
            return SparkyCallerRank.Agent;
        }

        if (hasAdministratorPermission)
        {
            return SparkyCallerRank.CommandDirector;
        }

        var assignedRoleIds = roleIds?.ToHashSet() ?? new HashSet<ulong>();
        if (assignedRoleIds.Overlaps(configuration.CommandDirectorRoleIds))
        {
            return SparkyCallerRank.CommandDirector;
        }

        if (assignedRoleIds.Overlaps(configuration.CeoRoleIds))
        {
            return SparkyCallerRank.CEO;
        }

        if (assignedRoleIds.Overlaps(configuration.OfficerRoleIds))
        {
            return SparkyCallerRank.Officer;
        }

        return SparkyCallerRank.Agent;
    }
}
