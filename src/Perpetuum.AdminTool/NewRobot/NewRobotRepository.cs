using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.NewRobot;

public class NewRobotRepository
{
    private readonly ConnectionSettings _connection;

    public NewRobotRepository(ConnectionSettings connection)
    {
        _connection = connection;
    }

    public async Task<RobotTemplateRelationData?> LoadTemplateRelationAsync(int robotDefinition)
    {
        await using var cn = new SqlConnection(_connection.BuildConnectionString());
        await cn.OpenAsync();

        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            SELECT itemscoresum, raceid, missionlevel, missionleveloverride, killep, note
            FROM robottemplaterelation
            WHERE definition = @def";
        cmd.Parameters.AddWithValue("@def", robotDefinition);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new RobotTemplateRelationData(
            ItemScoreSum: r.GetDouble(0),
            RaceId: r.GetInt32(1),
            MissionLevel: r.GetInt32(2),
            MissionLevelOverride: r.GetInt32(3),
            KillEp: r.GetInt32(4),
            Note: r.IsDBNull(5) ? null : r.GetString(5));
    }
}
