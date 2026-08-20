using Perpetuum.Services.Sessions;
using Perpetuum.Tests.Fakes.Data;
using Perpetuum.Tests.Fakes.Sessions;
using Perpetuum.Tests.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    [Collection(PerpetuumStaticsCollection.Name)]
    public class StaleOnlineFlagCensusTests
    {
        private const string FlaggedCharactersQuery = "select accountid from characters where inuse=1";

        private readonly PerpetuumStaticsFixture _fixture;
        private readonly FakeDb _db;
        private readonly FakeSessionManager _sessions = new();

        public StaleOnlineFlagCensusTests(PerpetuumStaticsFixture fixture)
        {
            _fixture = fixture;
            _fixture.Logger.Clear();
            _db = FakeDb.Install();
        }

        [Fact]
        public void A_flag_whose_account_has_no_session_is_counted_as_orphaned()
        {
            _db.When(FlaggedCharactersQuery, FakeResultSet.FromRows(["accountid"], [7], [8], [9]));
            _sessions.Add(new FakeSession(accountId: 7));

            new StaleOnlineFlagCensus(_sessions).Update(TimeSpan.FromMinutes(5));

            Assert.Contains(_fixture.Logger.Events, e => e.Message.Contains("2 of 3"));
        }

        [Fact]
        public void Nothing_is_orphaned_while_every_flag_has_a_session_behind_it()
        {
            _db.When(FlaggedCharactersQuery, FakeResultSet.FromRows(["accountid"], [7], [8]));
            _sessions.Add(new FakeSession(accountId: 7));
            _sessions.Add(new FakeSession(accountId: 8));

            new StaleOnlineFlagCensus(_sessions).Update(TimeSpan.FromMinutes(5));

            Assert.Contains(_fixture.Logger.Events, e => e.Message.Contains("0 of 2"));
        }

        [Fact]
        public void The_census_reports_every_cycle_so_a_quiet_server_is_told_apart_from_a_dead_census()
        {
            _db.When(FlaggedCharactersQuery, FakeResultSet.Empty("accountid"));

            new StaleOnlineFlagCensus(_sessions).Update(TimeSpan.FromMinutes(5));

            Assert.Contains(_fixture.Logger.Events, e => e.Message.Contains("[Ghost] census"));
        }

        [Fact]
        public void The_census_takes_a_reading_as_soon_as_it_starts()
        {
            // The timer fires a full interval after start, so without this the first number would
            // arrive five minutes into the run. A reading at start is also the most interesting one
            // there is: nobody is connected yet, so every flag still set was left by the last run.
            _db.When(FlaggedCharactersQuery, FakeResultSet.FromRows(["accountid"], [8], [9]));

            new StaleOnlineFlagCensus(_sessions).Start();

            Assert.Contains(_fixture.Logger.Events, e => e.Message.Contains("2 of 2"));
        }

        [Fact]
        public void Two_flags_on_one_account_with_no_session_are_both_counted()
        {
            // One account can hold several characters, and a rolled back sign out leaves the flag on
            // whichever one was selected. Counting accounts rather than rows would undercount.
            _db.When(FlaggedCharactersQuery, FakeResultSet.FromRows(["accountid"], [8], [8]));

            new StaleOnlineFlagCensus(_sessions).Update(TimeSpan.FromMinutes(5));

            Assert.Contains(_fixture.Logger.Events, e => e.Message.Contains("2 of 2"));
        }
    }
}
