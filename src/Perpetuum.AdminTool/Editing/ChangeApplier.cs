using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Editing
{
    public class ChangeApplier
    {
        private readonly ConnectionSettings _connection;

        public ChangeApplier(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task ExecuteAsync(IReadOnlyList<IPendingChange> changes)
        {
            var script = SqlScriptBuilder.Build(changes);

            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = script;
            cmd.CommandType = System.Data.CommandType.Text;
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
