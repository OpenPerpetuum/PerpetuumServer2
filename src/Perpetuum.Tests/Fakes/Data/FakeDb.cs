using System.Data;
using System.Transactions;
using Perpetuum.Data;

namespace Perpetuum.Tests.Fakes.Data
{
    public sealed record RecordedCommand
    {
        public required string CommandText { get; init; }
        public required CommandType CommandType { get; init; }
        public required IReadOnlyDictionary<string, object?> Parameters { get; init; }
        public required int CommandTimeout { get; init; }
        public required bool HadAmbientTransaction { get; init; }
        public required TransactionStatus? AmbientTransactionStatus { get; init; }
    }

    /// <summary>
    /// Installs a fake data layer into Db.DbQueryFactory, which funnels every Db.Query() call
    /// site in the codebase. Results are matched by substring against the command text.
    /// </summary>
    public sealed class FakeDb
    {
        // _commands is lock-protected because production code logs and queries from timer and task
        // threads. _results and _nonQueries are deliberately not: they are configured by the test
        // before the code under test runs, and never mutated afterwards. Registering a stub from
        // inside the code under test would race, and nothing here prevents it.
        private readonly List<(string Match, FakeResultSet Result)> _results = [];
        private readonly List<(string Match, int RowsAffected)> _nonQueries = [];
        private readonly List<RecordedCommand> _commands = [];
        private readonly object _gate = new();

        public static FakeDb Install()
        {
            FakeDb fake = new();
            Db.DbQueryFactory = () => new DbQuery(
                () => new FakeDbConnection(fake),
                new GlobalConfiguration { DistributedTransactions = false });
            return fake;
        }

        /// <summary>
        /// Registers a result for any command whose text contains <paramref name="commandTextContains"/>.
        /// The first registration that matches wins, so register the most specific pattern first —
        /// a broad pattern registered earlier silently shadows a narrower one registered later.
        /// </summary>
        public void When(string commandTextContains, FakeResultSet result)
            => _results.Add((commandTextContains, result));

        public void WhenNonQuery(string commandTextContains, int rowsAffected)
            => _nonQueries.Add((commandTextContains, rowsAffected));

        public IReadOnlyList<RecordedCommand> Commands
        {
            get { lock (_gate) { return [.. _commands]; } }
        }

        public RecordedCommand? LastCommandMatching(string commandTextContains)
            => Commands.LastOrDefault(c => c.CommandText.Contains(commandTextContains, StringComparison.OrdinalIgnoreCase));

        internal int RowsAffectedFor(string commandText)
        {
            foreach ((string match, int rows) in _nonQueries)
            {
                if (commandText.Contains(match, StringComparison.OrdinalIgnoreCase)) { return rows; }
            }

            return 0;
        }

        internal FakeResultSet Record(FakeDbCommand command)
        {
            Dictionary<string, object?> parameters = [];
            foreach (object p in command.Parameters)
            {
                FakeParameter parameter = (FakeParameter)p;
                parameters[parameter.ParameterName] = parameter.Value is DBNull ? null : parameter.Value;
            }

            Transaction? ambient = command.OwnerConnection.AmbientTransactionAtOpen;

            lock (_gate)
            {
                _commands.Add(new RecordedCommand
                {
                    CommandText = command.CommandText,
                    CommandType = command.CommandType,
                    Parameters = parameters,
                    CommandTimeout = command.CommandTimeout,
                    HadAmbientTransaction = ambient != null,
                    AmbientTransactionStatus = ambient?.TransactionInformation.Status,
                });
            }

            foreach ((string match, FakeResultSet result) in _results)
            {
                if (command.CommandText.Contains(match, StringComparison.OrdinalIgnoreCase)) { return result; }
            }

            return FakeResultSet.Empty();
        }
    }
}
