using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Templates
{
    public interface IRobotTemplateRelationRepository
    {
        Task<List<RobotTemplateRelationRow>> LoadAllAsync();
    }

    public interface IRobotTemplateRelationRepositoryFactory
    {
        IRobotTemplateRelationRepository Create(ConnectionSettings connection);
    }

    public sealed class RobotTemplateRelationRepositoryFactory : IRobotTemplateRelationRepositoryFactory
    {
        public IRobotTemplateRelationRepository Create(ConnectionSettings connection)
        {
            return new RobotTemplateRelationRepository(connection);
        }
    }

    public sealed class RobotTemplateRelationRepository : IRobotTemplateRelationRepository
    {
        private readonly ConnectionSettings _connection;

        public RobotTemplateRelationRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<List<RobotTemplateRelationRow>> LoadAllAsync()
        {
            var rows = new List<RobotTemplateRelationRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "select r.definition, r.templateid, r.itemscoresum, r.raceid, " +
                "r.missionlevel, r.missionleveloverride, r.killep, r.note, " +
                "e.definitionName, t.name " +
                "from robottemplaterelation r " +
                "left join entitydefaults e on e.definition = r.definition " +
                "left join robottemplates t on t.id = r.templateid " +
                "order by r.definition";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var snap = new RobotTemplateRelationSnapshot
                {
                    Definition = reader.GetInt32(0),
                    TemplateId = reader.GetInt32(1),
                    ItemScoreSum = reader.GetInt32(2),
                    RaceId = reader.GetInt32(3),
                    MissionLevel = reader.IsDBNull(4) ? null : (int?)reader.GetInt32(4),
                    MissionLevelOverride = reader.IsDBNull(5) ? null : (int?)reader.GetInt32(5),
                    KillEp = reader.IsDBNull(6) ? null : (int?)reader.GetInt32(6),
                    Note = reader.IsDBNull(7) ? null : reader.GetString(7)
                };
                var row = new RobotTemplateRelationRow(snap)
                {
                    DefinitionName = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    TemplateName = reader.IsDBNull(9) ? "" : reader.GetString(9)
                };
                rows.Add(row);
            }
            return rows;
        }
    }
}
