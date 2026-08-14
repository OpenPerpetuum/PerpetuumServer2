using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Npc
{
    public interface IFlockRepository
    {
        Task<FlockLoad> LoadAllAsync();
        Task<List<FlockSummary>> LoadByPresenceAsync(int presenceId);
    }

    public interface IFlockRepositoryFactory
    {
        IFlockRepository Create(ConnectionSettings connection);
    }

    public sealed class FlockRepositoryFactory : IFlockRepositoryFactory
    {
        public IFlockRepository Create(ConnectionSettings connection) => new FlockRepository(connection);
    }

    public sealed class FlockRepository : IFlockRepository
    {
        private readonly ConnectionSettings _connection;
        public FlockRepository(ConnectionSettings connection) => _connection = connection;

        public async Task<FlockLoad> LoadAllAsync()
        {
            var result = new FlockLoad();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await LoadPresencePicksAsync(cn, result.PresencePicks);
            await LoadDefinitionPicksAsync(cn, result.DefinitionPicks);

            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "select f.id, f.name, f.presenceid, f.flockmembercount, f.definition, " +
                "f.spawnoriginX, f.spawnoriginY, f.spawnrangeMin, f.spawnrangeMax, " +
                "f.respawnseconds, f.totalspawncount, f.homerange, f.note, f.respawnmultiplierlow, " +
                "f.enabled, f.iscallforhelp, f.behaviorType, f.npcSpecialType, " +
                "p.name, e.definitionname from npcflock f " +
                "left join npcpresence p on p.id = f.presenceid " +
                "left join entitydefaults e on e.definition = f.definition " +
                "order by f.presenceid, f.name, f.id";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var snapshot = new FlockSnapshot
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
                result.Rows.Add(new FlockRow(snapshot)
                {
                    PresenceName = reader.IsDBNull(18) ? "" : reader.GetString(18),
                    DefinitionName = reader.IsDBNull(19) ? "" : reader.GetString(19)
                });
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
                "select f.id, f.name, f.definition, e.definitionname, f.flockmembercount, " +
                "f.enabled, f.behaviorType, f.npcSpecialType from npcflock f " +
                "left join entitydefaults e on e.definition = f.definition " +
                "where f.presenceid = @pid order by f.name, f.id";
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

        private static async Task LoadPresencePicksAsync(
            SqlConnection connection, List<PresencePickItem> sink)
        {
            await using var cmd = connection.CreateCommand();
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

        private static async Task LoadDefinitionPicksAsync(
            SqlConnection connection, List<EntityPickItem> sink)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "select definition, definitionname, categoryflags, enabled, hidden, tiertype, tierlevel " +
                "from entitydefaults order by definitionname";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sink.Add(new EntityPickItem
                {
                    Definition = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    CategoryFlags = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                    Enabled = !reader.IsDBNull(3) && reader.GetBoolean(3),
                    Hidden = !reader.IsDBNull(4) && reader.GetBoolean(4),
                    TierType = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    TierLevel = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                });
            }
        }
    }

    public sealed class FlockLoad
    {
        public List<FlockRow> Rows { get; } = new();
        public List<PresencePickItem> PresencePicks { get; } = new();
        public List<EntityPickItem> DefinitionPicks { get; } = new();
    }

    public sealed class FlockSummary
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
