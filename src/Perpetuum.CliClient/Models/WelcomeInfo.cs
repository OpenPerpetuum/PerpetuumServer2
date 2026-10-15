using System;
using System.Collections.Generic;

namespace Perpetuum.CliClient.Models
{
    public class WelcomeInfo
    {
        public string WorldName { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public DateTime? OSTime { get; init; }
        public bool SteamLoginEnabled { get; init; }
        public string? ResourceServerUrl { get; init; }
        public bool IsDev { get; init; }
        public IReadOnlyDictionary<string, object> RawData { get; init; } = new Dictionary<string, object>();

        public static WelcomeInfo FromDictionary(IDictionary<string, object> data)
        {
            return new WelcomeInfo
            {
                WorldName = data.GetString(k.worldName, "Perpetuum"),
                Version = data.GetString("version", string.Empty),
                OSTime = data.GetDateTime(k.OSTime),
                SteamLoginEnabled = data.GetBool("steamLoginEnabled"),
                ResourceServerUrl = data.TryGetValue("resourceServerURL", out var rUrl) && rUrl != null ? rUrl.ToString() : null,
                IsDev = data.GetBool("dev"),
                RawData = (data as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>(data)
            };
        }
    }
}
