using System.Transactions;
using Perpetuum.Data;
using Perpetuum.Tests.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    /// <summary>
    /// Guards the platform fallback in PerpetuumBootstrapper.Init: on Linux the raw
    /// TransactionManager.ImplicitDistributedTransactions setter throws
    /// PlatformNotSupportedException when enabled (observed as a server boot failure),
    /// while the guarded helper must never throw. Shares the PerpetuumStaticsCollection
    /// with DbQueryEnlistmentTests because both touch process-wide transaction state.
    /// </summary>
    [Collection(PerpetuumStaticsCollection.Name)]
    public class TransactionSupportTests
    {
        public TransactionSupportTests(PerpetuumStaticsFixture fixture)
        {
            _ = fixture;
        }

        [Fact]
        public void Enabling_implicit_distributed_transactions_never_throws_and_restores_the_previous_state()
        {
            bool original = TransactionManager.ImplicitDistributedTransactions;

            try
            {
                TransactionSupport.SetImplicitDistributedTransactions(true);
                TransactionSupport.SetImplicitDistributedTransactions(false);
            }
            finally
            {
                TransactionSupport.SetImplicitDistributedTransactions(original);
            }
        }
    }
}
