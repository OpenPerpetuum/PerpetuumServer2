using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Loot
{
    public interface INpcLootRepository
    {
        Task<List<NpcLootRow>> LoadAllAsync();
    }

    public interface INpcLootRepositoryFactory
    {
        INpcLootRepository Create(ConnectionSettings connection);
    }

    public sealed class NpcLootRepositoryFactory : INpcLootRepositoryFactory
    {
        public INpcLootRepository Create(ConnectionSettings connection)
        {
            return new NpcLootRepository(connection);
        }
    }

    public sealed class NpcLootRepository : INpcLootRepository
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
                "select l.id, l.definition, l.lootdefinition, l.minquantity, l.quantity, " +
                "l.probability, l.dontdamage, l.repackaged, npc.definitionname, item.definitionname " +
                "from npcloot l " +
                "left join entitydefaults npc on npc.definition = l.definition " +
                "left join entitydefaults item on item.definition = l.lootdefinition " +
                "order by l.definition, l.lootdefinition, l.id";
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
                rows.Add(new NpcLootRow(snap)
                {
                    DefinitionName = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    LootDefinitionName = reader.IsDBNull(9) ? "" : reader.GetString(9)
                });
            }
            return rows;
        }
    }
}
