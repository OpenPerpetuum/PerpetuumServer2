using System;
using System.Collections.Concurrent;
using System.Data;
using System.Transactions;
using Perpetuum.Log;

namespace Perpetuum.Data
{
    /// <summary>
    /// Manages sharing of open database connections within ambient transaction scopes.
    /// Reusing a single open connection across multiple queries inside the same TransactionScope
    /// ensures local transaction isolation (LTM) on SQL Server without triggering distributed
    /// transaction escalation (MSDTC), ensuring full compatibility with Linux and reducing
    /// connection pool contention.
    /// </summary>
    public static class DbConnectionManager
    {
        private static readonly ConcurrentDictionary<Transaction, IDbConnection> ActiveConnections = new();
        private static readonly object SyncRoot = new();

        public static IDbConnection GetOrCreateConnection(Transaction transaction, DbConnectionFactory connectionFactory)
        {
            if (ActiveConnections.TryGetValue(transaction, out IDbConnection? existingConn))
            {
                return existingConn;
            }

            lock (SyncRoot)
            {
                if (ActiveConnections.TryGetValue(transaction, out existingConn))
                {
                    return existingConn;
                }

                IDbConnection connection = connectionFactory();
                connection.Open();

                ActiveConnections[transaction] = connection;

                transaction.TransactionCompleted += (sender, e) =>
                {
                    lock (SyncRoot)
                    {
                        ActiveConnections.TryRemove(transaction, out _);
                    }

                    try
                    {
                        connection.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex);
                    }
                };

                return connection;
            }
        }

        public static int ActiveConnectionCount => ActiveConnections.Count;
    }
}
