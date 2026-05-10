using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Npc
{
    public class FlockRepository
    {
        private readonly ConnectionSettings _connection;

        public FlockRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<FlockLoad> LoadAllAsync()
        {
            var result = new FlockLoad();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();

            await LoadPresencePicksAsync(cn, result.PresencePicks);

            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "select id, name, presenceid, flockmembercount, definition, " +
                "spawnoriginX, spawnoriginY, spawnrangeMin, spawnrangeMax, " +
                "respawnseconds, totalspawncount, homerange, note, respawnmultiplierlow, " +
                "enabled, iscallforhelp, behaviorType, npcSpecialType " +
                "from npcflock order by presenceid, name, id";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var snap = new FlockSnapshot
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    PresenceId = reader.GetInt32(2),
                    FlockMemberCount = reader.GetInt32(3),
                    Definition = reader.GetInt32(4),
                    SpawnOriginX = reader.GetInt32(5),
                    SpawnOriginY = reader.GetInt32(6),
                    SpawnRangeMin = reader.GetInt32(7),
                    SpawnRangeMax = reader.GetInt32(8),
                    RespawnSeconds = reader.GetInt32(9),
                    TotalSpawnCount = reader.GetInt32(10),
                    HomeRange = reader.GetInt32(11),
                    Note = reader.IsDBNull(12) ? null : reader.GetString(12),
                    RespawnMultiplierLow = reader.IsDBNull(13) ? 0 : reader.GetDouble(13),
                    Enabled = !reader.IsDBNull(14) && reader.GetBoolean(14),
                    IsCallForHelp = !reader.IsDBNull(15) && reader.GetBoolean(15),
                    BehaviorType = reader.GetInt32(16),
                    NpcSpecialType = reader.GetInt32(17)
                };
                result.Rows.Add(new FlockRow(snap));
            }
            return result;
        }

        public async Task<List<FlockSummary>> LoadByPresenceAsync(int presenceId)
        {
            var result = new List<FlockSummary>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "select f.id, f.name, f.definition, e.definitionname, " +
                "f.flockmembercount, f.enabled, f.behaviorType, f.npcSpecialType " +
                "from npcflock f " +
                "left join entitydefaults e on e.definition = f.definition " +
                "where f.presenceid = @pid " +
                "order by f.name, f.id";
            cmd.Parameters.AddWithValue("@pid", presenceId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new FlockSummary
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Definition = reader.GetInt32(2),
                    DefinitionName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    FlockMemberCount = reader.GetInt32(4),
                    Enabled = !reader.IsDBNull(5) && reader.GetBoolean(5),
                    BehaviorType = reader.GetInt32(6),
                    NpcSpecialType = reader.GetInt32(7)
                });
            }
            return result;
        }

        private static async Task LoadPresencePicksAsync(SqlConnection cn, List<PresencePickItem> sink)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select id, name from npcpresence order by name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sink.Add(new PresencePickItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }
    }

    public class FlockLoad
    {
        public List<FlockRow> Rows { get; } = new();
        public List<PresencePickItem> PresencePicks { get; } = new();
    }

    public class FlockSummary
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public int Definition { get; init; }
        public string DefinitionName { get; init; } = "";
        public int FlockMemberCount { get; init; }
        public bool Enabled { get; init; }
        public int BehaviorType { get; init; }
        public int NpcSpecialType { get; init; }
    }
}
