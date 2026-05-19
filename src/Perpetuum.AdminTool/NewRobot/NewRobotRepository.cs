using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.NewRobot;

public record ChassisBonusRow(int ExtensionId, double Bonus, int TargetPropertyId, bool EffectEnhancer, string? Note);

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
            ItemScoreSum: r.GetInt32(0),
            RaceId: r.GetInt32(1),
            MissionLevel: r.IsDBNull(2) ? 0 : r.GetInt32(2),
            MissionLevelOverride: r.IsDBNull(3) ? 0 : r.GetInt32(3),
            KillEp: r.IsDBNull(4) ? 0 : r.GetInt32(4),
            Note: r.IsDBNull(5) ? null : r.GetString(5));
    }

    public async Task<IReadOnlyList<ChassisBonusRow>> LoadChassisBonusesAsync(int chassisDefinition)
    {
        await using var cn = new SqlConnection(_connection.BuildConnectionString());
        await cn.OpenAsync();

        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            SELECT extension, bonus, targetpropertyID, effectenhancer, note
            FROM chassisbonus
            WHERE definition = @def";
        cmd.Parameters.AddWithValue("@def", chassisDefinition);

        var results = new List<ChassisBonusRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            results.Add(new ChassisBonusRow(
                ExtensionId: r.GetInt32(0),
                Bonus: r.GetDouble(1),
                TargetPropertyId: r.GetInt32(2),
                EffectEnhancer: r.GetBoolean(3),
                Note: r.IsDBNull(4) ? null : r.GetString(4)));
        return results;
    }
}
