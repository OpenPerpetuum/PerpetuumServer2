using System;
using System.Collections.Generic;

namespace Perpetuum.CliClient.Models
{
    public class CharacterSummary
    {
        public int CharacterId { get; init; }
        public long RootEid { get; init; }
        public string Nick { get; init; } = string.Empty;
        public bool IsDocked { get; init; }
        public long BaseEid { get; init; }
        public string BaseName { get; init; } = string.Empty;
        public long HomeBaseEid { get; init; }
        public string HomeBaseName { get; init; } = string.Empty;
        public int? ZoneId { get; init; }
        public long Credit { get; init; }
        public DateTime? Creation { get; init; }
        public DateTime? LastUsed { get; init; }
        public string MoodMessage { get; init; } = string.Empty;
        public bool InUse { get; init; }
        public bool IsOffensiveNick { get; init; }
        public IReadOnlyDictionary<string, object> RawData { get; init; } = new Dictionary<string, object>();

        public static CharacterSummary FromDictionary(IDictionary<string, object> dict)
        {
            int? zoneId = null;
            if (dict.TryGetValue(k.zoneID, out var zObj) && zObj != null)
            {
                zoneId = dict.GetInt(k.zoneID);
            }

            return new CharacterSummary
            {
                CharacterId = dict.GetInt(k.characterID),
                RootEid = dict.GetLong(k.rootEID),
                Nick = dict.GetString(k.nick),
                IsDocked = dict.GetBool(k.docked),
                BaseEid = dict.GetLong(k.baseEID),
                BaseName = dict.GetString(k.baseName),
                HomeBaseEid = dict.GetLong(k.homeBaseEID),
                HomeBaseName = dict.GetString(k.homeBaseName),
                ZoneId = zoneId,
                Credit = dict.GetLong(k.credit),
                Creation = dict.GetDateTime(k.creation),
                LastUsed = dict.GetDateTime(k.lastUsed),
                MoodMessage = dict.GetString(k.moodMessage),
                InUse = dict.GetBool(k.inUse),
                IsOffensiveNick = dict.GetBool(k.offensiveNick),
                RawData = (dict as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>(dict)
            };
        }
    }
}
