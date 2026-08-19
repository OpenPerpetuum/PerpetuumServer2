using Perpetuum.Accounting;
using Perpetuum.Host.Requests;
using Perpetuum.Network;
using Perpetuum.RequestHandlers;
using Perpetuum.Services.Relay;
using Perpetuum.Services.Sessions;
using Perpetuum.Tests.Fakes.Sessions;
using Perpetuum.Tests.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    /// <summary>
    /// Sign in is where a ghost announces itself: the account says it is logged in when nobody
    /// asked it to be. Until now the handler logged one line for both shapes of that, and they need
    /// different fixes.
    /// </summary>
    [Collection(PerpetuumStaticsCollection.Name)]
    public class StaleLoginReportingTests
    {
        private readonly PerpetuumStaticsFixture _fixture;
        private readonly FakeSessionManager _sessions = new();

        public StaleLoginReportingTests(PerpetuumStaticsFixture fixture)
        {
            _fixture = fixture;
            _fixture.Logger.Clear();
        }

        private sealed class TestSignInHandler(IRelayStateService relayState, ISessionManager sessions, IAccountRepository accounts, ILoginQueueService queue, Account account)
            : SignInRequestHandler(relayState, sessions, accounts, queue)
        {
            protected override Account LoadAccount(IRequest request) => account;
        }

        private (TestSignInHandler Handler, FakeSession Connecting) Handler(Account account)
        {
            FakeSession connecting = new(accountId: 0);

            return (new TestSignInHandler(new FakeRelayStateService(), _sessions, new FakeAccountRepository(account), new FakeLoginQueueService(), account), connecting);
        }

        [Fact]
        public void A_stale_login_still_holding_its_session_is_reported_with_its_silence()
        {
            Account account = new() { Id = 7, IsLoggedIn = true };
            ConnectionActivity activity = new(DateTime.Now - TimeSpan.FromSeconds(40));
            FakeSession held = new(accountId: 7, activity);
            _sessions.Add(held);

            (TestSignInHandler handler, FakeSession connecting) = Handler(account);

            _ = Assert.Throws<PerpetuumException>(() => handler.HandleRequest(new FakeRequest(connecting)));

            Assert.Contains(_fixture.Logger.Events, e => e.Message.Contains("live session still held") && e.Message.Contains("accountId:7"));
            Assert.Equal(ErrorCodes.NoSimultaneousLoginsAllowed, held.ForcedQuitWith);
        }

        [Fact]
        public void A_stale_login_with_no_session_behind_it_is_reported_as_a_flag_left_set()
        {
            Account account = new() { Id = 7, IsLoggedIn = true };

            (TestSignInHandler handler, FakeSession connecting) = Handler(account);

            _ = Assert.Throws<PerpetuumException>(() => handler.HandleRequest(new FakeRequest(connecting)));

            Assert.Contains(_fixture.Logger.Events, e => e.Message.Contains("no live session") && e.Message.Contains("accountId:7"));
        }

        [Fact]
        public void An_ordinary_login_reports_no_ghost_at_all()
        {
            Account account = new() { Id = 7, IsLoggedIn = false };

            (TestSignInHandler handler, FakeSession connecting) = Handler(account);

            handler.HandleRequest(new FakeRequest(connecting));

            Assert.DoesNotContain(_fixture.Logger.Events, e => e.Message.Contains("[Ghost]"));
        }
    }
}
