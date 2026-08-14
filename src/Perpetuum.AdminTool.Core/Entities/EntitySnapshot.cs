using System.Collections.Generic;

namespace Perpetuum.AdminTool.Entities
{
    public sealed class EntitiesSnapshot
    {
        public IReadOnlyList<EntityDefaultRow> Rows { get; init; } = new List<EntityDefaultRow>();
        public IReadOnlyDictionary<int, AggregateFieldInfo> Fields { get; init; }
            = new Dictionary<int, AggregateFieldInfo>();
    }
}
