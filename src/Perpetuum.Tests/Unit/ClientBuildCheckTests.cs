using Perpetuum.Host.Requests;
using Perpetuum.RequestHandlers;
using Perpetuum.Tests.Fakes.Sessions;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    /// <summary>
    /// The launcher's forced-update rule: only a client that reports a buildId can be refused, so the
    /// legacy client (which never sends one) keeps signing in whatever the minimum is.
    /// </summary>
    public class ClientBuildCheckTests
    {
        private static FakeRequest Request(params (string Key, object Value)[] data)
        {
            Dictionary<string, object> d = [];
            foreach ((string key, object value) in data)
                d[key] = value;
            return new FakeRequest(new FakeSession(accountId: 0), d);
        }

        [Fact]
        public void A_client_below_the_minimum_is_refused_with_ClientUpdateRequired()
        {
            PerpetuumException ex = Assert.Throws<PerpetuumException>(() =>
                SignInRequestHandler.ThrowIfClientOutdated(Request((k.clientKind, "launcher"), (k.buildId, 1002)), 1005));
            Assert.Equal(ErrorCodes.ClientUpdateRequired, ex.error);
        }

        [Fact]
        public void A_client_at_or_above_the_minimum_passes()
        {
            SignInRequestHandler.ThrowIfClientOutdated(Request((k.buildId, 1005)), 1005);
            SignInRequestHandler.ThrowIfClientOutdated(Request((k.buildId, 1006)), 1005);
        }

        [Fact]
        public void A_legacy_client_without_a_buildId_is_never_refused()
        {
            SignInRequestHandler.ThrowIfClientOutdated(Request(), 1005);
            SignInRequestHandler.ThrowIfClientOutdated(Request((k.clientver, "1.0")), 1005);
        }

        [Fact]
        public void A_minimum_of_zero_disables_the_check()
        {
            SignInRequestHandler.ThrowIfClientOutdated(Request((k.buildId, 1)), 0);
        }

        [Fact]
        public void The_error_code_matches_the_value_the_Unity_client_mirrors()
        {
            Assert.Equal(682, (int)ErrorCodes.ClientUpdateRequired);
        }
    }
}
