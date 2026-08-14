using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.EquipmentSets
{
    public interface IEquipmentSetRepository
    {
        Task<List<EquipmentSetRow>> LoadAllSetsAsync();
        Task<List<EquipmentSetMemberRow>> LoadMembersAsync(int setId);
        Task<List<EquipmentSetThresholdRow>> LoadThresholdsAsync(int setId);
        Task<List<AggregateFieldInfo>> LoadAggregateFieldsAsync();
        Task<List<SetMemberPickItem>> LoadMemberChoicesAsync();
    }

    public interface IEquipmentSetRepositoryFactory
    {
        IEquipmentSetRepository Create(ConnectionSettings connection);
    }

    public sealed class EquipmentSetRepositoryFactory : IEquipmentSetRepositoryFactory
    {
        public IEquipmentSetRepository Create(ConnectionSettings connection)
        {
            return new EquipmentSetRepository(connection);
        }
    }

    public sealed class EquipmentSetRepository : IEquipmentSetRepository
    {
        private readonly ConnectionSettings _connection;

        public EquipmentSetRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<List<EquipmentSetRow>> LoadAllSetsAsync()
        {
            var result = new List<EquipmentSetRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT set_id, name FROM equipment_sets ORDER BY name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new EquipmentSetRow
                {
                    SetId = reader.GetInt32(0),
                    Name  = reader.IsDBNull(1) ? "" : reader.GetString(1),
                });
            return result;
        }

        public async Task<List<EquipmentSetMemberRow>> LoadMembersAsync(int setId)
        {
            var result = new List<EquipmentSetMemberRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT m.definition, ISNULL(e.definitionname, '') " +
                "FROM equipment_set_members m " +
                "LEFT JOIN entitydefaults e ON e.definition = m.definition " +
                "WHERE m.set_id = @setId " +
                "ORDER BY e.definitionname";
            cmd.Parameters.AddWithValue("@setId", setId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new EquipmentSetMemberRow
                {
                    SetId          = setId,
                    Definition     = reader.GetInt32(0),
                    DefinitionName = reader.GetString(1),
                });
            return result;
        }

        public async Task<List<EquipmentSetThresholdRow>> LoadThresholdsAsync(int setId)
        {
            var result = new List<EquipmentSetThresholdRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT required_pieces, aggregate_field, bonus_value " +
                "FROM equipment_set_bonus_thresholds " +
                "WHERE set_id = @setId " +
                "ORDER BY required_pieces";
            cmd.Parameters.AddWithValue("@setId", setId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new EquipmentSetThresholdRow
                {
                    SetId            = setId,
                    RequiredPieces   = reader.GetInt32(0),
                    AggregateFieldId = reader.GetInt32(1),
                    BonusValue       = reader.GetDouble(2),
                });
            return result;
        }

        public async Task<List<AggregateFieldInfo>> LoadAggregateFieldsAsync()
        {
            var result = new List<AggregateFieldInfo>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, name, formula, measurementunit, measurementmultiplier, " +
                "measurementoffset, category, digits FROM aggregatefields";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new AggregateFieldInfo
                {
                    Id                    = reader.GetInt32(0),
                    Name                  = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Formula               = reader.IsDBNull(2) ? 0  : reader.GetInt32(2),
                    MeasurementUnit       = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    MeasurementMultiplier = reader.IsDBNull(4) ? 0d : reader.GetDouble(4),
                    MeasurementOffset     = reader.IsDBNull(5) ? 0d : reader.GetDouble(5),
                    Category              = reader.IsDBNull(6) ? 0  : reader.GetInt32(6),
                    Digits                = reader.IsDBNull(7) ? 0  : reader.GetInt32(7),
                });
            return result;
        }

        public async Task<List<SetMemberPickItem>> LoadMemberChoicesAsync()
        {
            var result = new List<SetMemberPickItem>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT definition, definitionname FROM entitydefaults " +
                "WHERE enabled = 1 ORDER BY definitionname";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new SetMemberPickItem
                {
                    Definition = reader.GetInt32(0),
                    DefinitionName = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
            return result;
        }
    }
}
