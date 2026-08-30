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
            // The same literal is executed for real against the live schema by
            // Perpetuum.Tests.Integration/Data/InsuranceQueryTests.cs. Nothing links the two at
            // compile time; if they drift apart, this stub keeps passing against a fiction.
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

        [Theory]
        [InlineData("usp_RecalculateInsurancePrices", CommandType.StoredProcedure)]
        [InlineData("select 1 from characters", CommandType.Text)]
        public void The_command_type_is_inferred_from_whether_the_text_contains_a_space(
            string commandText,
            CommandType expected)
        {
            // DbQuery.ExecuteHelper decides this with _commandText.Contains(' '), which is what
            // makes a spaceless command run as a stored procedure. Breaking that heuristic changes
            // how every parameterless proc call is dispatched, silently.
            _db.When(commandText, FakeResultSet.Empty("x"));

            _ = Db.Query().CommandText(commandText).Execute();

            RecordedCommand? recorded = _db.LastCommandMatching(commandText);
            Assert.NotNull(recorded);
            Assert.Equal(expected, recorded!.CommandType);
        }

        [Fact]
        public void ExecuteSingleRow_returns_the_first_row_and_null_when_there_are_none()
        {
            _db.When("select top 2 id from characters",
                FakeResultSet.FromRows(["id"], [1], [2]));
            _db.When("select id from nothing", FakeResultSet.Empty("id"));

            IDataRecord? first = Db.Query()
                .CommandText("select top 2 id from characters")
                .ExecuteSingleRow();

            Assert.NotNull(first);
            Assert.Equal(1, first!.GetValue<int>(0));

            Assert.Null(Db.Query().CommandText("select id from nothing").ExecuteSingleRow());
        }
    }
}
