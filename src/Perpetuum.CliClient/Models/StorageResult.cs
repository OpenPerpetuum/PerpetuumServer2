using System;
using System.Collections.Generic;
using System.Linq;

namespace Perpetuum.CliClient.Models
{
    public class StorageResult
    {
        public long ContainerEid { get; init; }
        public int ContainerDefinition { get; init; }
        public long BaseEid { get; init; }
        public string BaseName { get; set; } = string.Empty;
        public List<StorageItemInfo> Items { get; init; } = new();
        public IReadOnlyDictionary<string, object> RawData { get; init; } = new Dictionary<string, object>();

        public int TotalItemCount => Items.Sum(i => i.Quantity > 0 ? i.Quantity : 1);
        public double TotalVolume => Items.Sum(i => i.Volume);

        public static StorageResult FromDictionary(IDictionary<string, object> dict, Dictionary<int, string>? definitionNames = null)
        {
            var itemsList = new List<StorageItemInfo>();
            if (dict.TryGetValue(k.items, out var itemsObj) && itemsObj != null)
            {
                if (itemsObj is IDictionary<string, object> itemsDict)
                {
                    foreach (var kvp in itemsDict)
                    {
                        if (kvp.Value is IDictionary<string, object> itemDict)
                        {
                            itemsList.Add(StorageItemInfo.FromDictionary(itemDict, definitionNames));
                        }
                    }
                }
                else if (itemsObj is IEnumerable<object> itemsEnum)
                {
                    foreach (var item in itemsEnum)
                    {
                        if (item is IDictionary<string, object> itemDict)
                        {
                            itemsList.Add(StorageItemInfo.FromDictionary(itemDict, definitionNames));
                        }
                    }
                }
            }

            return new StorageResult
            {
                ContainerEid = dict.GetLong(k.eid),
                ContainerDefinition = dict.GetInt(k.definition),
                BaseEid = dict.GetLong(k.baseEID),
                Items = itemsList,
                RawData = (dict as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>(dict)
            };
        }
    }
}
