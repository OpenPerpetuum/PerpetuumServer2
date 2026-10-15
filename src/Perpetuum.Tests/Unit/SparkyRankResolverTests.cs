using Perpetuum.Services.Sparky;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyRankResolverTests
{
    [Theory]
    [InlineData(AccessLevel.normal, (int)SparkyCallerRank.Agent)]
    [InlineData(AccessLevel.notDefined, (int)SparkyCallerRank.Agent)]
    [InlineData(AccessLevel.gameAdmin, (int)SparkyCallerRank.CommandDirector)]
    [InlineData(AccessLevel.owner, (int)SparkyCallerRank.CommandDirector)]
    public void Game_rank_uses_authoritative_access_level(AccessLevel accessLevel, int expected)
    {
        Assert.Equal((SparkyCallerRank)expected, SparkyRankResolver.ResolveGame(accessLevel));
    }

    [Fact]
    public void Discord_rank_defaults_to_agent_for_non_guild_callers_and_unmapped_members()
    {
        var configuration = new SparkyDiscordRankConfiguration("100", "200", "300");

        Assert.Equal(SparkyCallerRank.Agent,
            SparkyRankResolver.ResolveDiscord(false, true, [300], configuration));
        Assert.Equal(SparkyCallerRank.Agent,
            SparkyRankResolver.ResolveDiscord(true, false, [999], configuration));
    }

    [Fact]
    public void Discord_administrator_and_highest_matching_configured_role_win()
    {
        var configuration = new SparkyDiscordRankConfiguration("100,101", "200", "300");

        Assert.Equal(SparkyCallerRank.CommandDirector,
            SparkyRankResolver.ResolveDiscord(true, true, [], configuration));
        Assert.Equal(SparkyCallerRank.CEO,
            SparkyRankResolver.ResolveDiscord(true, false, [100, 200], configuration));
        Assert.Equal(SparkyCallerRank.Officer,
            SparkyRankResolver.ResolveDiscord(true, false, [101], configuration));
    }
}
