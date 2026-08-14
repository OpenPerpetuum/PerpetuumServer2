using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Editing
{
    public interface IChangeApplier
    {
        Task ExecuteAsync(
            IReadOnlyList<IPendingChange> changes,
            string? authorEmail = null,
            CancellationToken cancellationToken = default);
    }

    public interface IChangeApplierFactory
    {
        IChangeApplier Create(ConnectionSettings connection);
    }

    public sealed class ChangeApplierFactory : IChangeApplierFactory
    {
        public IChangeApplier Create(ConnectionSettings connection)
        {
            return new ChangeApplier(connection);
        }
    }

    public sealed class ChangeApplier : IChangeApplier
    {
        private readonly ConnectionSettings _connection;

        public ChangeApplier(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task ExecuteAsync(
            IReadOnlyList<IPendingChange> changes,
            string? authorEmail = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(changes);
            if (changes.Count == 0)
            {
                throw new ArgumentException("At least one pending change is required.", nameof(changes));
            }

            string script = SqlScriptBuilder.Build(changes, authorEmail);

            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync(cancellationToken);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = script;
            cmd.CommandType = System.Data.CommandType.Text;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
