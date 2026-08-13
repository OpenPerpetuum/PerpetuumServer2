using System.Data;
using Perpetuum.Data;
using Perpetuum.Tests.Fakes.Data;
using Perpetuum.Tests.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    [Collection(PerpetuumStaticsCollection.Name)]
    public class DbQueryTests
    {
        private readonly FakeDb _db;

        public DbQueryTests(PerpetuumStaticsFixture fixture)
        {
            _ = fixture;
            _db = FakeDb.Install();
        }

        [Fact]
        public void Execute_returns_one_record_per_row()
        {
            _db.When("select definition,fee,payout from insuranceprices",
                FakeResultSet.FromRows(
                    ["definition", "fee", "payout"],
                    [1234, 100.5d, 900.0d],
                    [5678, 200.5d, 1800.0d]));

            List<IDataRecord> records = Db.Query()
                .CommandText("select definition,fee,payout from insuranceprices")
                .Execute();

            Assert.Equal(2, records.Count);
            Assert.Equal(1234, records[0].GetValue<int>(0));
            Assert.Equal(900.0d, records[0].GetValue<double>(2));
            Assert.Equal(5678, records[1].GetValue<int>(0));
        }

        [Fact]
        public void Execute_on_no_rows_returns_an_empty_list()
        {
            _db.When("select 1 from nothing", FakeResultSet.Empty("x"));

            List<IDataRecord> records = Db.Query().CommandText("select 1 from nothing").Execute();

            Assert.Empty(records);
        }

        [Fact]
        public void Parameters_are_passed_through_by_name()
        {
            _db.When("select * from characters where id = @id", FakeResultSet.Empty("id"));

            _ = Db.Query()
                .CommandText("select * from characters where id = @id")
                .SetParameter("@id", 42)
                .Execute();

            RecordedCommand? recorded = _db.LastCommandMatching("from characters");
            Assert.NotNull(recorded);
            Assert.Equal(42, recorded!.Parameters["@id"]);
        }

        [Fact]
        public void A_null_parameter_value_is_sent_as_DBNull()
        {
            _db.When("select * from characters where nick = @nick", FakeResultSet.Empty("nick"));

            _ = Db.Query()
                .CommandText("select * from characters where nick = @nick")
                .SetParameter("@nick", null)
                .Execute();

            RecordedCommand? recorded = _db.LastCommandMatching("from characters");
            Assert.NotNull(recorded);
            Assert.Null(recorded!.Parameters["@nick"]);
        }

        [Fact]
        public void Timeout_is_propagated_to_the_command()
        {
            _db.WhenNonQuery("exec usp_RecalculateInsurancePrices", 1);

            _ = Db.Query()
                .CommandText("exec usp_RecalculateInsurancePrices")
                .Timeout(120)
                .ExecuteNonQuery();

            RecordedCommand? recorded = _db.LastCommandMatching("usp_RecalculateInsurancePrices");
            Assert.NotNull(recorded);
            Assert.Equal(120, recorded!.CommandTimeout);
        }

        [Fact]
        public void The_default_timeout_is_thirty_seconds()
        {
            _db.When("select 1", FakeResultSet.Empty("x"));

            _ = Db.Query().CommandText("select 1").Execute();

            RecordedCommand? recorded = _db.LastCommandMatching("select 1");
            Assert.NotNull(recorded);
            Assert.Equal(30, recorded!.CommandTimeout);
        }

        [Fact]
        public void ExecuteScalar_returns_the_first_column_of_the_first_row()
        {
            _db.When("select count(*) from characters",
                FakeResultSet.FromRows(["count"], [7]));

            int count = Db.Query().CommandText("select count(*) from characters").ExecuteScalar<int>();

            Assert.Equal(7, count);
        }

        [Fact]
        public void Db_Query_with_command_text_is_the_same_as_setting_it_afterwards()
        {
            _db.When("select 1", FakeResultSet.Empty("x"));

            _ = Db.Query("select 1").Execute();

            Assert.Equal("select 1", _db.Commands[^1].CommandText);
        }
    }
}
