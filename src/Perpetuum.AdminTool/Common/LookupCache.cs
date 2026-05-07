using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Common
{
    /// <summary>
    /// Process-wide cache of small lookup tables that drive dropdowns in multiple tabs:
    /// entitydefaults (definition, definitionname) and robottemplates (id, name).
    /// Refresh on app start, after every successful Direct-DB commit, and from the
    /// per-tab Reload buttons.
    /// </summary>
    public class LookupCache
    {
        public ObservableCollection<EntityPickItem> Entities { get; } = new();
        public ObservableCollection<TemplatePickItem> Templates { get; } = new();

        public Dictionary<int, string> EntityNamesByDefinition { get; private set; } = new();
        public Dictionary<int, string> TemplateNamesById { get; private set; } = new();

        public async Task RefreshEntitiesAsync(ConnectionSettings connection)
        {
            await using var cn = new SqlConnection(connection.BuildConnectionString());
            await cn.OpenAsync();
            await RefreshEntitiesAsync(cn);
        }

        public async Task RefreshTemplatesAsync(ConnectionSettings connection)
        {
            await using var cn = new SqlConnection(connection.BuildConnectionString());
            await cn.OpenAsync();
            await RefreshTemplatesAsync(cn);
        }

        public async Task RefreshAllAsync(ConnectionSettings connection)
        {
            await using var cn = new SqlConnection(connection.BuildConnectionString());
            await cn.OpenAsync();
            await RefreshEntitiesAsync(cn);
            await RefreshTemplatesAsync(cn);
        }

        private async Task RefreshEntitiesAsync(SqlConnection cn)
        {
            var fresh = new List<EntityPickItem>();
            var names = new Dictionary<int, string>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select definition, definitionname, categoryflags, enabled from entitydefaults order by definitionname";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var def = reader.GetInt32(0);
                var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var categoryFlags = reader.IsDBNull(2) ? 0L : reader.GetInt64(2);
                var enabled = !reader.IsDBNull(3) && reader.GetBoolean(3);
                fresh.Add(new EntityPickItem
                {
                    Definition = def,
                    Name = name,
                    CategoryFlags = categoryFlags,
                    Enabled = enabled
                });
                names[def] = name;
            }
            Entities.Clear();
            foreach (var p in fresh) Entities.Add(p);
            EntityNamesByDefinition = names;
        }

        private async Task RefreshTemplatesAsync(SqlConnection cn)
        {
            var fresh = new List<TemplatePickItem>();
            var names = new Dictionary<int, string>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "select id, name from robottemplates order by name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                fresh.Add(new TemplatePickItem { Id = id, Name = name });
                names[id] = name;
            }
            Templates.Clear();
            foreach (var p in fresh) Templates.Add(p);
            TemplateNamesById = names;
        }
    }
}
