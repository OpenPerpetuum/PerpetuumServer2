using System.Collections.Generic;

namespace Perpetuum.CliClient.Models
{
    public class ChannelInfo
    {
        public string Name { get; init; } = string.Empty;
        public int Type { get; init; }
        public bool HasPassword { get; init; }
        public int MemberCount { get; init; }
        public string Topic { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, object> RawData { get; init; } = new Dictionary<string, object>();

        public string TypeName => Type switch
        {
            0 => "Public",
            1 => "Highlighted",
            2 => "Corporation",
            3 => "Gang",
            4 => "Station",
            5 => "Admin",
            _ => $"Type({Type})"
        };

        public static ChannelInfo FromDictionary(IDictionary<string, object> data)
        {
            return new ChannelInfo
            {
                Name = data.GetString(k.name),
                Type = data.GetInt(k.type),
                HasPassword = data.GetBool(k.password),
                MemberCount = data.GetInt(k.count),
                Topic = data.GetString(k.topic),
                RawData = (data as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>(data)
            };
        }
    }
}
