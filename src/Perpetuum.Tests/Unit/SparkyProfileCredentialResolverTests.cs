using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyProfileCredentialResolverTests
{
    private const string DevToken = "dev-token-for-sparky-tests-0123456789";
    private const string LiveToken = "live-token-for-sparky-tests-0123456789";

    [Fact]
    public void Each_credential_resolves_only_to_its_configured_profile()
    {
        var resolver = new SparkyApiCredentialResolver(DevToken, LiveToken);

        Assert.True(resolver.TryResolve(DevToken, out var devProfile));
        Assert.Equal(SparkyApiProfile.Dev, devProfile);
        Assert.True(resolver.TryResolve(LiveToken, out var liveProfile));
        Assert.Equal(SparkyApiProfile.Live, liveProfile);
    }

    [Fact]
    public void Unknown_empty_and_null_credentials_are_rejected_without_exposing_secrets()
    {
        var resolver = new SparkyApiCredentialResolver(DevToken, LiveToken);

        Assert.False(resolver.TryResolve("unknown-token-which-is-long-enough-to-compare", out _));
        Assert.False(resolver.TryResolve(string.Empty, out _));
        Assert.False(resolver.TryResolve(null, out _));

        var exception = Assert.Throws<InvalidDataException>(
            () => new SparkyApiCredentialResolver(DevToken, DevToken));
        Assert.DoesNotContain(DevToken, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_single_token_is_dev_only()
    {
        var resolver = new SparkyApiCredentialResolver(DevToken, liveToken: null);

        Assert.True(resolver.TryResolve(DevToken, out var profile));
        Assert.Equal(SparkyApiProfile.Dev, profile);
    }
}
