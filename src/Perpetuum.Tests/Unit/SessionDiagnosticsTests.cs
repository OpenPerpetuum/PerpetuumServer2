using System.Net;
using Perpetuum.Services.Sessions;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class SessionDiagnosticsTests
    {
        private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 4321);

        [Fact]
        public void A_stale_login_that_still_holds_a_session_reports_how_long_it_has_been_silent()
        {
            SessionID sessionId = SessionID.New();

            string line = SessionDiagnostics.DescribeStaleLogin(
                accountId: 7,
                sessionId: sessionId,
                remoteEndPoint: Remote,
                silentFor: TimeSpan.FromSeconds(93),
                longestGap: TimeSpan.FromSeconds(12));

            // The silence is the discriminator. A session still held by the server means the peer
            // vanished without closing and nothing here noticed, which is the missing idle timeout
            // rather than a rolled back sign out.
            Assert.Contains("live session", line);
            Assert.Contains("accountId:7", line);
            Assert.Contains($"sessionId:{sessionId}", line);
            Assert.Contains("silentFor:93.0s", line);
            Assert.Contains("longestGap:12.0s", line);
            Assert.Contains(Remote.ToString(), line);
        }

        [Fact]
        public void A_stale_login_with_no_session_left_is_reported_as_a_flag_nobody_cleared()
        {
            string line = SessionDiagnostics.DescribeStaleLogin(accountId: 7);

            // The other half of the discriminator: the session is gone but the account row still
            // says logged in, which is what a sign out that rolled back leaves behind.
            Assert.Contains("no live session", line);
            Assert.Contains("accountId:7", line);
            Assert.DoesNotContain("silentFor", line);
        }

        [Fact]
        public void A_closing_session_is_reported_with_everything_needed_to_stitch_the_other_lines()
        {
            SessionID sessionId = SessionID.New();

            string line = SessionDiagnostics.DescribeClosing(
                sessionId: sessionId,
                accountId: 7,
                characterId: 55,
                remoteEndPoint: Remote,
                silentFor: TimeSpan.FromSeconds(4),
                longestGap: TimeSpan.FromSeconds(2.5));

            // TcpConnection and SessionManager both log the same disconnect with only the endpoint
            // to identify it. This line is the one that carries the identity, so it has to be
            // written before sign out clears AccountId and Character.
            Assert.Contains($"sessionId:{sessionId}", line);
            Assert.Contains("accountId:7", line);
            Assert.Contains("characterId:55", line);
            Assert.Contains(Remote.ToString(), line);
            Assert.Contains("silentFor:4.0s", line);
            Assert.Contains("longestGap:2.5s", line);
        }

        [Fact]
        public void A_census_reports_the_flags_that_have_nobody_connected_behind_them()
        {
            string line = SessionDiagnostics.DescribeCensus(orphaned: 3, flagged: 40, liveSessions: 37);

            Assert.Contains("3", line);
            Assert.Contains("40", line);
            Assert.Contains("37", line);
        }
    }
}
