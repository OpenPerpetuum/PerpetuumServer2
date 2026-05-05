using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.ExportedTypes;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Entities
{
    public class EntityRepository
    {
        private readonly ConnectionSettings _connection;

        public EntityRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<EntitiesSnapshot> LoadAsync()
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();

            var fields = await LoadFieldsAsync(cn);
            var rows = await LoadDefaultsAsync(cn);
            var byDef = new Dictionary<int, EntityDefaultRow>(rows.Count);
            foreach (var r in rows) byDef[r.Definition] = r;

            await LoadStatsAsync(cn, byDef);

            return new EntitiesSnapshot
            {
                Rows = rows,
                Fields = fields
            };
        }

        private async Task<IReadOnlyDictionary<int, AggregateFieldInfo>> LoadFieldsAsync(SqlConnection cn)
        {
            var dict = new Dictionary<int, AggregateFieldInfo>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select id, name, formula, measurementunit, measurementmultiplier, measurementoffset, category, digits from aggregatefields";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var info = new AggregateFieldInfo
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Formula = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    MeasurementUnit = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    MeasurementMultiplier = reader.IsDBNull(4) ? 0d : reader.GetDouble(4),
                    MeasurementOffset = reader.IsDBNull(5) ? 0d : reader.GetDouble(5),
                    Category = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    Digits = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                };
                dict[info.Id] = info;
            }
            return dict;
        }

        private async Task<List<EntityDefaultRow>> LoadDefaultsAsync(SqlConnection cn)
        {
            var rows = new List<EntityDefaultRow>(2048);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "select definition, definitionName, descriptionToken, " +
                "categoryflags, attributeflags, mass, volume, health, quantity, " +
                "hidden, purchasable, tiertype, tierlevel, options " +
                "from entitydefaults where enabled = 1 order by definition";

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var snapshot = new EntityDefaultSnapshot
                {
                    Definition = reader.GetInt32(0),
                    DefinitionName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    DescriptionToken = reader.IsDBNull(2) ? null : reader.GetString(2),
                    CategoryFlags = reader.IsDBNull(3) ? 0L : reader.GetInt64(3),
                    AttributeFlags = reader.IsDBNull(4) ? 0L : reader.GetInt64(4),
                    Mass = reader.IsDBNull(5) ? 0d : reader.GetDouble(5),
                    Volume = reader.IsDBNull(6) ? 0d : reader.GetDouble(6),
                    Health = reader.IsDBNull(7) ? 0d : reader.GetDouble(7),
                    Quantity = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                    Hidden = !reader.IsDBNull(9) && reader.GetBoolean(9),
                    Purchasable = !reader.IsDBNull(10) && reader.GetBoolean(10),
                    TierType = reader.IsDBNull(11) ? null : (int?)reader.GetInt32(11),
                    TierLevel = reader.IsDBNull(12) ? null : (int?)reader.GetInt32(12),
                    Options = reader.IsDBNull(13) ? null : reader.GetString(13),
                };
                rows.Add(new EntityDefaultRow(snapshot));
            }
            return rows;
        }

        private async Task LoadStatsAsync(SqlConnection cn, Dictionary<int, EntityDefaultRow> byDef)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select definition, field, value from aggregatevalues";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var def = reader.GetInt32(0);
                var fieldId = reader.GetInt32(1);
                var value = reader.IsDBNull(2) ? 0d : reader.GetDouble(2);

                if (!byDef.TryGetValue(def, out var row)) continue;

                row.Stats.Add(new StatRow(def, (AggregateField)fieldId, value, wasInDb: true));
                row.OriginalStats[fieldId] = value;
            }
        }
    }
}
