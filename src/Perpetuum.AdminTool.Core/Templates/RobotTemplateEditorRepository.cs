using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;
using Perpetuum.GenXY;

namespace Perpetuum.AdminTool.Templates
{
    public interface IRobotTemplateEditorRepository
    {
        Task<List<RobotTemplateEditorEntity>> LoadAllAsync();
    }

    public interface IRobotTemplateEditorRepositoryFactory
    {
        IRobotTemplateEditorRepository Create(ConnectionSettings connection);
    }

    public sealed class RobotTemplateEditorRepositoryFactory : IRobotTemplateEditorRepositoryFactory
    {
        public IRobotTemplateEditorRepository Create(ConnectionSettings connection)
        {
            return new RobotTemplateEditorRepository(connection);
        }
    }

    /// <summary>
    /// One-shot loader for the structured robot-template editor. Reads every
    /// entitydefaults row and parses its `options` to extract the few fields
    /// the editor needs for filtering. Heavy on the wire (full table + Genxy
    /// parse per row), but the modal opens infrequently and we don't keep this
    /// data around between opens.
    /// </summary>
    public sealed class RobotTemplateEditorRepository : IRobotTemplateEditorRepository
    {
        private readonly ConnectionSettings _connection;

        public RobotTemplateEditorRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<List<RobotTemplateEditorEntity>> LoadAllAsync()
        {
            var rows = new List<RobotTemplateEditorEntity>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "select definition, definitionname, categoryflags, attributeflags, enabled, options " +
                "from entitydefaults order by definitionname";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var definition = reader.GetInt32(0);
                var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var categoryFlags = reader.GetInt64(2);
                var attributeFlags = reader.GetInt64(3);
                var enabled = !reader.IsDBNull(4) && reader.GetBoolean(4);
                var optionsString = reader.IsDBNull(5) ? "" : reader.GetString(5);

                long moduleFlag = 0;
                long ammoType = 0;
                int ammoCapacity = 0;
                int[] slotFlags = System.Array.Empty<int>();

                if (!string.IsNullOrEmpty(optionsString))
                {
                    var dict = GenxyConverter.Deserialize(optionsString);
                    moduleFlag = ToLong(dict.GetValueOrDefault("moduleFlag"));
                    ammoType = ToLong(dict.GetValueOrDefault("ammoType"));
                    ammoCapacity = ToInt(dict.GetValueOrDefault("ammoCapacity"));
                    slotFlags = ToIntArray(dict.GetValueOrDefault("slotFlags"));
                }

                rows.Add(new RobotTemplateEditorEntity
                {
                    Definition = definition,
                    Name = name,
                    CategoryFlags = categoryFlags,
                    AttributeFlags = attributeFlags,
                    Enabled = enabled,
                    ModuleFlag = moduleFlag,
                    AmmoType = ammoType,
                    AmmoCapacity = ammoCapacity,
                    SlotFlags = slotFlags
                });
            }
            return rows;
        }

        private static long ToLong(object? v) => v switch
        {
            null => 0L,
            long l => l,
            int i => i,
            _ => 0L
        };

        private static int ToInt(object? v) => v switch
        {
            null => 0,
            int i => i,
            long l => (int)l,
            _ => 0
        };

        private static int[] ToIntArray(object? v) => v switch
        {
            int[] a => a,
            _ => System.Array.Empty<int>()
        };
    }
}
