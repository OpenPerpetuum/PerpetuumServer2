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
        public void The_insurance_price_recalculation_procedure_exists_and_is_callable()
        {
            DatabaseFixture fixture = new();
            using SqlConnection connection = fixture.OpenConnection();

            using SqlCommand command = connection.CreateCommand();
            command.CommandText =
                "select count(*) from sys.objects where type in ('P','PC') and name = 'usp_RecalculateInsurancePrices'";

            Assert.Equal(1, (int)command.ExecuteScalar());
        }
    }
}
