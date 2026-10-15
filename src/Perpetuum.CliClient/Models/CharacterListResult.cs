using System.Collections.Generic;

namespace Perpetuum.CliClient.Models
{
    public class CharacterListResult
    {
        public List<CharacterSummary> Characters { get; init; } = new List<CharacterSummary>();
        public int ExtensionPoints { get; init; }
        public IReadOnlyDictionary<string, object> RawData { get; init; } = new Dictionary<string, object>();

        public static CharacterListResult FromDictionary(IDictionary<string, object> data)
        {
            var result = new CharacterListResult();
            IDictionary<string, object>? content = data;

            // In CharacterList handler, data is wrapped in "result": { characters: { ... }, extensionPoints: ... }
            if (data.TryGetValue(k.result, out var resObj) && resObj is IDictionary<string, object> resDict)
            {
                content = resDict;
            }

            var extensionPoints = content.GetInt("extensionPoints");

            if (content.TryGetValue("characters", out var charObj) && charObj is IDictionary<string, object> charDict)
            {
                foreach (var kvp in charDict)
                {
                    if (kvp.Value is IDictionary<string, object> cdict)
                    {
                        result.Characters.Add(CharacterSummary.FromDictionary(cdict));
                    }
                }
            }

            return new CharacterListResult
            {
                Characters = result.Characters,
                ExtensionPoints = extensionPoints,
                RawData = (data as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>(data)
            };
        }
    }
}
