using System.Data;
using System.Data.Common;
using System.Transactions;

namespace Perpetuum.Data
{
    public delegate IDbConnection DbConnectionFactory();

    public class DbQuery(DbConnectionFactory connectionFactory, GlobalConfiguration configuration)
    {
        private readonly DbConnectionFactory _connectionFactory = connectionFactory;

        private string _commandText = string.Empty;
        private Dictionary<string, object> _parameters;
        private int _commandTimeout = 30;

        public DbQuery CommandText(string cmdText)
        {
            _commandText = cmdText;
            return this;
        }

        public DbQuery SetParameters(IEnumerable<KeyValuePair<string, object>> parameters)
        {
            if (parameters == null)
            {
                return this;
            }

            foreach (KeyValuePair<string, object> kvp in parameters)
            {
                SetParameter(kvp.Key, kvp.Value);
            }

            return this;
        }

        public DbQuery Timeout(int seconds)
        {
            _commandTimeout = seconds;
            return this;
        }

        public DbQuery SetParameter(string name, object value)
        {
            _parameters ??= [];

            _parameters[name] = value;
            return this;
        }

        private T ExecuteHelper<T>(Func<IDbCommand, T> execute)
        {
            using IDbConnection connection = _connectionFactory();
            connection.Open();

            if (configuration.DistributedTransactions && Transaction.Current != null && connection is DbConnection dbConnection)
            {
                dbConnection.EnlistTransaction(Transaction.Current);
            }

            IDbCommand command = connection.CreateCommand();
            command.CommandText = _commandText;
            command.CommandType = _commandText.Contains(' ') ? CommandType.Text : CommandType.StoredProcedure;
            command.CommandTimeout = _commandTimeout;

            if (_parameters != null)
            {
                foreach (KeyValuePair<string, object> kvp in _parameters)
                {
                    IDbDataParameter parameter = command.CreateParameter();
                    parameter.ParameterName = kvp.Key;
                    parameter.Value = kvp.Value ?? DBNull.Value;
                    command.Parameters.Add(parameter);
                }
            }

            using (command)
            {
                return execute(command);
            }
        }

        public List<IDataRecord> Execute()
        {
            return ExecuteHelper((cmd) =>
            {
                using IDataReader reader = cmd.ExecuteReader();
                return reader.ToEnumerable().ToList();
            });
        }

        public IDataRecord ExecuteSingleRow()
        {
            return ExecuteHelper((cmd) =>
            {
                using IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow);
                return reader.ToEnumerable().FirstOrDefault();
            });
        }

        public int ExecuteNonQuery()
        {
            return ExecuteHelper((cmd) => cmd.ExecuteNonQuery());
        }

        public T ExecuteScalar<T>()
        {
            return (T)ExecuteHelper((cmd) =>
            {
                object? value = cmd.ExecuteScalar();
                return value == DBNull.Value ? default(T) : value ?? default(T);
            });
        }
    }
}