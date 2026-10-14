using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyAdminAuthenticationTests
{
    private const string AdminToken = "sparky-admin-token-that-is-at-least-32-characters";
    private const string DevToken = "sparky-dev-token-that-is-at-least-32-characters-long";
    private const string LiveToken = "sparky-live-token-that-is-at-least-32-characters-long";

    [Fact]
    public void AcceptsOnlyTheDedicatedAdminBearerToken()
    {
        using var authentication = new SparkyAdminAuthentication(AdminToken, DevToken, LiveToken);

        Assert.True(authentication.IsAuthorized($"Bearer {AdminToken}"));
        Assert.False(authentication.IsAuthorized($"Bearer {DevToken}"));
        Assert.False(authentication.IsAuthorized($"Bearer {LiveToken}"));
        Assert.False(authentication.IsAuthorized(null));
        Assert.False(authentication.IsAuthorized("Basic " + AdminToken));
        Assert.False(authentication.IsAuthorized("Bearer wrong-token"));
    }

    [Theory]
    [InlineData(DevToken)]
    [InlineData(LiveToken)]
    public void RejectsAnAdminTokenThatIsAlsoAGameToken(string gameToken)
    {
        Assert.Throws<InvalidDataException>(() => new SparkyAdminAuthentication(gameToken, DevToken, LiveToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void RequiresAStrongAdminToken(string token)
    {
        Assert.Throws<InvalidDataException>(() => new SparkyAdminAuthentication(token, DevToken, LiveToken));
    }
}
