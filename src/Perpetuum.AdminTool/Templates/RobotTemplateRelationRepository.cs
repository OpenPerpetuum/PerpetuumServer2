using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Templates
{
    public class RobotTemplateRelationRepository
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
                "select definition, templateid, itemscoresum, raceid, " +
                "missionlevel, missionleveloverride, killep, note " +
                "from robottemplaterelation order by definition";
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
                rows.Add(new RobotTemplateRelationRow(snap));
            }
            return rows;
        }
    }
}
