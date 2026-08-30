using Perpetuum.Services.Sessions;
using Perpetuum.Tests.Fakes.Data;
using Perpetuum.Tests.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    [Collection(PerpetuumStaticsCollection.Name)]
    public class StaleOnlineFlagsTests
    {
        private readonly PerpetuumStaticsFixture _fixture;
        private readonly FakeDb _db;

        public StaleOnlineFlagsTests(PerpetuumStaticsFixture fixture)
        {
            _fixture = fixture;
            _fixture.Logger.Clear();
            _db = FakeDb.Install();
        }

        [Fact]
        public void Clearing_stale_flags_reports_how_many_it_cleared()
        {
            _db.WhenNonQuery("update characters set inuse=0", 2);

            int cleared = StaleOnlineFlags.ClearForAccount(7);

            Assert.Equal(2, cleared);
            Assert.Contains(_fixture.Logger.Events, e => e.Message.Contains("2") && e.Message.Contains("accountId:7"));
        }

        [Fact]
        public void Clearing_stale_flags_says_nothing_when_none_were_set()
        {
            _db.WhenNonQuery("update characters set inuse=0", 0);

            int cleared = StaleOnlineFlags.ClearForAccount(7);

            Assert.Equal(0, cleared);
            Assert.DoesNotContain(_fixture.Logger.Events, e => e.Message.Contains("accountId:7"));
        }

        [Fact]
        public void Clearing_stale_flags_only_matches_rows_that_are_actually_set()
        {
            // The count is the whole point of this type, and it is only a ghost count if the
            // statement matches the rows it changes. "where accountid=@id" alone matches every
            // character on the account, so it would report the account's character count on every
            // single sign in. The extra predicate makes the number mean what it says.
            _db.WhenNonQuery("update characters set inuse=0", 1);

            _ = StaleOnlineFlags.ClearForAccount(7);

            RecordedCommand? command = _db.LastCommandMatching("update characters set inuse=0");
            Assert.NotNull(command);
            Assert.Contains("inuse=1", command.CommandText);
            Assert.Equal(7, command.Parameters["@id"]);
        }
    }
}
