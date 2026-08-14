using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Templates
{
    public interface IRobotTemplateRepository
    {
        Task<List<RobotTemplateRow>> LoadAllAsync();
    }

    public interface IRobotTemplateRepositoryFactory
    {
        IRobotTemplateRepository Create(ConnectionSettings connection);
    }

    public sealed class RobotTemplateRepositoryFactory : IRobotTemplateRepositoryFactory
    {
        public IRobotTemplateRepository Create(ConnectionSettings connection)
        {
            return new RobotTemplateRepository(connection);
        }
    }

    public sealed class RobotTemplateRepository : IRobotTemplateRepository
    {
        private readonly ConnectionSettings _connection;

        public RobotTemplateRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<List<RobotTemplateRow>> LoadAllAsync()
        {
            var rows = new List<RobotTemplateRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select id, name, description, note from robottemplates order by id";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var snap = new RobotTemplateSnapshot
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Note = reader.IsDBNull(3) ? null : reader.GetString(3)
                };
                rows.Add(new RobotTemplateRow(snap));
            }
            return rows;
        }
    }
}
