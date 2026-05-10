using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Loot
{
    public class NpcLootRepository
    {
        private readonly ConnectionSettings _connection;

        public NpcLootRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<List<NpcLootRow>> LoadAllAsync()
        {
            var rows = new List<NpcLootRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "select id, definition, lootdefinition, minquantity, quantity, probability, dontdamage, repackaged " +
                "from npcloot order by definition, lootdefinition, id";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var snap = new NpcLootSnapshot
                {
                    Id = reader.GetInt32(0),
                    Definition = reader.GetInt32(1),
                    LootDefinition = reader.GetInt32(2),
                    MinQuantity = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    Quantity = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    Probability = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                    DontDamage = !reader.IsDBNull(6) && reader.GetBoolean(6),
                    Repackaged = !reader.IsDBNull(7) && reader.GetBoolean(7)
                };
                rows.Add(new NpcLootRow(snap));
            }
            return rows;
        }
    }
}
