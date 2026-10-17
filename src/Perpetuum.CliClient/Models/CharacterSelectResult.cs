using System;
using System.Collections.Generic;

namespace Perpetuum.CliClient.Models
{
    public class CharacterSelectResult
    {
        public int CharacterId { get; init; }
        public long RootEid { get; init; }
        public long CorporationEid { get; init; }
        public long AllianceEid { get; init; }
        public bool IsDocked { get; init; }
        public long BaseEid { get; set; }
        public string BaseName { get; set; } = string.Empty;
        public IDictionary<string, object>? ZoneData { get; init; }
        public IReadOnlyDictionary<string, object> RawData { get; init; } = new Dictionary<string, object>();

        public static CharacterSelectResult FromDictionary(IDictionary<string, object> data)
        {
            var charId = data.GetInt(k.characterID);
            var rootEid = data.GetLong(k.rootEID);
            var corpEid = data.GetLong(k.corporationEID);
            var allianceEid = data.GetLong(k.allianceEID);
            var baseEid = data.GetLong(k.baseEID);
            var baseName = data.GetString(k.baseName);

            IDictionary<string, object>? zoneDict = data.GetDictionary(k.zone);

            return new CharacterSelectResult
            {
                CharacterId = charId,
                RootEid = rootEid,
                CorporationEid = corpEid,
                AllianceEid = allianceEid,
                IsDocked = zoneDict == null,
                BaseEid = baseEid,
                BaseName = baseName,
                ZoneData = zoneDict,
                RawData = (data as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>(data)
            };
        }
    }
}
