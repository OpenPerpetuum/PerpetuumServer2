using System.Data;
using Microsoft.Data.SqlClient;
using Perpetuum.Tests.Integration.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Integration.Data
{
    /// <summary>
    /// The unit tests stub this exact query text against a fake connection. Running it for real
    /// is what keeps the fake honest: if a column is renamed or the table is dropped, the unit
    /// tests keep passing against their stub and this test is what fails.
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class InsuranceQueryTests
    {
        // Kept in sync by hand with two other copies of this literal: the stub in
        // Perpetuum.Tests/Unit/DbQueryTests.cs and the production query in
        // InsuranceHelper.LoadInsurancePrices. Nothing links the three at compile time — they are in
        // two assemblies plus production — so if this one drifts, this test keeps passing while
        // verifying a query production no longer runs, and the anchor silently stops anchoring.
        private const string InsurancePricesQuery = "select definition,fee,payout from insuranceprices";

        [RequiresGameRootFact]
        public void The_insurance_prices_query_runs_and_returns_the_expected_columns()
        {
            DatabaseFixture fixture = new();
            using SqlConnection connection = fixture.OpenConnection();

            using SqlCommand command = connection.CreateCommand();
            command.CommandText = InsurancePricesQuery;

            using SqlDataReader reader = command.ExecuteReader();

            Assert.Equal(3, reader.FieldCount);
            Assert.Equal("definition", reader.GetName(0), ignoreCase: true);
            Assert.Equal("fee", reader.GetName(1), ignoreCase: true);
            Assert.Equal("payout", reader.GetName(2), ignoreCase: true);
        }

        [RequiresGameRootFact]
        public void The_insurance_prices_columns_have_the_types_the_code_reads_them_as()
        {
            // InsuranceHelper.LoadInsurancePrices reads column 0 as int and columns 1 and 2 as
            // double. A type change in the database would break that silently at runtime.
            DatabaseFixture fixture = new();
            using SqlConnection connection = fixture.OpenConnection();

            using SqlCommand command = connection.CreateCommand();
            command.CommandText = InsurancePricesQuery;

            using SqlDataReader reader = command.ExecuteReader(CommandBehavior.SchemaOnly);
            DataTable? schema = reader.GetSchemaTable();

            Assert.NotNull(schema);
            Assert.Equal(typeof(int), schema!.Rows[0]["DataType"]);
            Assert.Equal(typeof(double), schema.Rows[1]["DataType"]);
            Assert.Equal(typeof(double), schema.Rows[2]["DataType"]);
        }

        [RequiresGameRootFact]
        public void The_insurance_price_recalculation_procedure_exists()
        {
            // Task 8 already asserts every documented procedure exists, so this looks redundant.
            // It is not, for two reasons worth stating rather than leaving a reader to reconstruct.
            // It anchors the unit tier's `WhenNonQuery("exec usp_RecalculateInsurancePrices", 1)`
            // stub exactly as the two tests above anchor the price-query stub. And Task 8's check is
            // driven from the contents of docs/db_structure/, so deleting the procedure together
            // with its documentation file would pass there and fail here.
            //
            // The name says only "exists": this asserts a catalog row, it never invokes the
            // procedure. Invoking it would write, and nothing in stages 0-4 writes.
            DatabaseFixture fixture = new();
            using SqlConnection connection = fixture.OpenConnection();

            using SqlCommand command = connection.CreateCommand();

            // Schema-qualified deliberately: this database already carries one same-named pair
            // across schemas (dbo.extensionSubscriptionStart and opp.extensionSubscriptionStart),
            // so a bare name match is not a safe assumption here.
            command.CommandText =
                "select count(*) from sys.objects o "
                + "join sys.schemas s on s.schema_id = o.schema_id "
                + "where o.type in ('P','PC') and s.name = 'dbo' "
                + "and o.name = 'usp_RecalculateInsurancePrices'";

            Assert.Equal(1, (int)command.ExecuteScalar());
        }
    }
}
