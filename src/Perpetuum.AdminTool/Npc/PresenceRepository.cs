using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Npc
{
    public class PresenceRepository
    {
        private readonly ConnectionSettings _connection;

        public PresenceRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<PresenceLoad> LoadAllAsync()
        {
            var result = new PresenceLoad();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();

            await LoadZoneSpawnPicksAsync(cn, result.ZoneSpawnPicks);

            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "select id, name, topx, topy, bottomx, bottomy, note, spawnid, enabled, " +
                "roaming, roamingrespawnseconds, presencetype, maxrandomflock, " +
                "randomcenterx, randomcentery, randomradius, dynamiclifetime, " +
                "isbodypull, isrespawnallowed, safebodypull, izgroupid, growthseconds " +
                "from npcpresence order by name, id";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var snap = new PresenceSnapshot
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    TopX = reader.GetInt32(2),
                    TopY = reader.GetInt32(3),
                    BottomX = reader.GetInt32(4),
                    BottomY = reader.GetInt32(5),
                    Note = reader.IsDBNull(6) ? null : reader.GetString(6),
                    SpawnId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    Enabled = !reader.IsDBNull(8) && reader.GetBoolean(8),
                    Roaming = !reader.IsDBNull(9) && reader.GetBoolean(9),
                    RoamingRespawnSeconds = reader.GetInt32(10),
                    PresenceType = reader.GetInt32(11),
                    MaxRandomFlock = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                    RandomCenterX = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                    RandomCenterY = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                    RandomRadius = reader.IsDBNull(15) ? null : reader.GetInt32(15),
                    DynamicLifetime = reader.IsDBNull(16) ? null : reader.GetInt32(16),
                    IsBodyPull = !reader.IsDBNull(17) && reader.GetBoolean(17),
                    IsRespawnAllowed = !reader.IsDBNull(18) && reader.GetBoolean(18),
                    SafeBodyPull = !reader.IsDBNull(19) && reader.GetBoolean(19),
                    IzGroupId = reader.IsDBNull(20) ? null : reader.GetInt32(20),
                    GrowthSeconds = reader.IsDBNull(21) ? null : reader.GetInt32(21)
                };
                result.Rows.Add(new PresenceRow(snap));
            }
            return result;
        }

        private static async Task LoadZoneSpawnPicksAsync(SqlConnection cn, List<ZoneSpawnPickItem> sink)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select spawnid, name from zones where spawnid is not null order by name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sink.Add(new ZoneSpawnPickItem
                {
                    SpawnId = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
        }
    }

    public class PresenceLoad
    {
        public List<PresenceRow> Rows { get; } = new();
        public List<ZoneSpawnPickItem> ZoneSpawnPicks { get; } = new();
    }
}
