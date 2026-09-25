using System.Transactions;
using Perpetuum.Log;

namespace Perpetuum.Data
{
    /// <summary>
    /// Platform-aware switch for implicit distributed transactions. On Windows, enabling
    /// TransactionManager.ImplicitDistributedTransactions makes new scopes distributed at
    /// creation. On platforms without a DTC coordinator (e.g. Linux) the setter throws
    /// PlatformNotSupportedException when enabled; in that case the default is kept and
    /// scopes run as local transactions. The explicit EnlistTransaction in DbQuery still
    /// provides commit/rollback semantics for single-resource scopes (see
    /// DistributedTransactionTests, which covers that path against a live database).
    /// </summary>
    public static class TransactionSupport
    {
        public static void SetImplicitDistributedTransactions(bool enabled)
        {
            try
            {
                TransactionManager.ImplicitDistributedTransactions = enabled;
            }
            catch (PlatformNotSupportedException)
            {
                Logger.Warning(
                    "DistributedTransactions is enabled, but this platform does not support implicit distributed transactions. " +
                    "TransactionScopes will run as local transactions with explicit enlistment.");
            }
        }
    }
}
