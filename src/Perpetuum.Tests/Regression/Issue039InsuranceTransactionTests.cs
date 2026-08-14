using System.Reflection;
using Perpetuum.Services.Insurance;
using Perpetuum.Tests.Fakes.Data;
using Perpetuum.Tests.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Regression
{
    /// <summary>
    /// ISSUE-039. Refresh() recalculated prices inside a TransactionScope and then reloaded the
    /// price cache. When the scope was opened with a using declaration it was still alive, and
    /// already completed, while the reload ran — so the reload threw and a running server kept
    /// quoting stale insurance fees.
    ///
    /// Revert the fix in InsurancePriceRefreshService.Refresh() and this test fails.
    /// </summary>
    [Collection(PerpetuumStaticsCollection.Name)]
    public class Issue039InsuranceTransactionTests
    {
        private const string RecalculateCommand = "usp_RecalculateInsurancePrices";
        private const string ReloadCommand = "from insuranceprices";

        private readonly FakeDb _db;

        public Issue039InsuranceTransactionTests(PerpetuumStaticsFixture fixture)
        {
            fixture.Logger.Clear();
            _db = FakeDb.Install();
            _db.WhenNonQuery(RecalculateCommand, 1);
            _db.When(ReloadCommand,
                FakeResultSet.FromRows(["definition", "fee", "payout"], [1234, 100.0d, 900.0d]));
        }

        private static void InvokeRefresh()
        {
            InsurancePriceRefreshService service = new();
            MethodInfo refresh = typeof(InsurancePriceRefreshService)
                .GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    "InsurancePriceRefreshService.Refresh() not found. If it was renamed, update this test.");

            refresh.Invoke(service, null);
        }

        [Fact]
        public void The_recalculation_runs_inside_a_transaction()
        {
            InvokeRefresh();

            RecordedCommand? recalculate = _db.LastCommandMatching(RecalculateCommand);
            Assert.NotNull(recalculate);
            Assert.True(
                recalculate!.HadAmbientTransaction,
                "usp_RecalculateInsurancePrices must run inside a transaction scope.");
        }

        [Fact]
        public void The_cache_reload_runs_outside_any_transaction()
        {
            InvokeRefresh();

            RecordedCommand? reload = _db.LastCommandMatching(ReloadCommand);
            Assert.NotNull(reload);
            Assert.False(
                reload!.HadAmbientTransaction,
                "ISSUE-039: the insurance price cache reload must run after the transaction scope "
                + "is disposed, not inside it. A using declaration instead of a using block "
                + "reintroduces the defect.");
        }

        [Fact]
        public void Both_statements_run_and_in_order()
        {
            InvokeRefresh();

            List<string> texts = [.. _db.Commands.Select(c => c.CommandText)];

            int recalculateIndex = texts.FindIndex(t => t.Contains(RecalculateCommand, StringComparison.OrdinalIgnoreCase));
            int reloadIndex = texts.FindIndex(t => t.Contains(ReloadCommand, StringComparison.OrdinalIgnoreCase));

            Assert.True(recalculateIndex >= 0, "The recalculation command never ran.");
            Assert.True(reloadIndex >= 0, "The cache reload never ran.");
            Assert.True(recalculateIndex < reloadIndex, "The reload must follow the recalculation.");
        }
    }
}
