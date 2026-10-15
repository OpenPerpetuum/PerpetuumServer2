using System;
using System.Collections.Generic;

namespace Perpetuum.CliClient.Models
{
    public class StorageItemInfo
    {
        public long Eid { get; init; }
        public int Definition { get; init; }
        public string DefinitionName { get; set; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public bool IsRepackaged { get; init; }
        public double Health { get; init; }
        public double Volume { get; init; }
        public long Parent { get; init; }
        public long Owner { get; init; }
        public List<StorageItemInfo> Items { get; init; } = new();
        public IReadOnlyDictionary<string, object> RawData { get; init; } = new Dictionary<string, object>();

        public string DisplayName => !string.IsNullOrWhiteSpace(Name)
            ? Name
            : (!string.IsNullOrWhiteSpace(DefinitionName) ? FormatDefinitionName(DefinitionName) : $"Item #{Definition}");

        public static string FormatDefinitionName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var text = raw;
            if (text.StartsWith("def_", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(4);
            }

            var parts = text.Split('_', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + (parts[i].Length > 1 ? parts[i].Substring(1) : "");
                }
            }
            return string.Join(" ", parts);
        }

        public static StorageItemInfo FromDictionary(IDictionary<string, object> dict, Dictionary<int, string>? definitionNames = null)
        {
            var defId = dict.GetInt(k.definition);
            string defName = string.Empty;
            if (definitionNames != null && definitionNames.TryGetValue(defId, out var dn))
            {
                defName = dn;
            }
            else if (dict.TryGetValue(k.definitionName, out var dnObj) && dnObj != null)
            {
                defName = dnObj.ToString() ?? string.Empty;
            }

            var subItems = new List<StorageItemInfo>();
            if (dict.TryGetValue(k.items, out var itemsObj) && itemsObj != null)
            {
                if (itemsObj is IDictionary<string, object> itemsDict)
                {
                    foreach (var kvp in itemsDict)
                    {
                        if (kvp.Value is IDictionary<string, object> childDict)
                        {
                            subItems.Add(FromDictionary(childDict, definitionNames));
                        }
                    }
                }
                else if (itemsObj is IEnumerable<object> itemsEnum)
                {
                    foreach (var item in itemsEnum)
                    {
                        if (item is IDictionary<string, object> childDict)
                        {
                            subItems.Add(FromDictionary(childDict, definitionNames));
                        }
                    }
                }
            }

            var quantity = dict.GetInt(k.quantity, 1);
            if (quantity <= 0) quantity = 1;

            return new StorageItemInfo
            {
                Eid = dict.GetLong(k.eid),
                Definition = defId,
                DefinitionName = defName,
                Name = dict.GetString(k.name),
                Quantity = quantity,
                IsRepackaged = dict.GetBool(k.repackaged),
                Health = dict.GetDouble(k.health, 100.0),
                Volume = dict.GetDouble(k.volume),
                Parent = dict.GetLong(k.parent),
                Owner = dict.GetLong(k.owner),
                Items = subItems,
                RawData = (dict as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>(dict)
            };
        }
    }
}
