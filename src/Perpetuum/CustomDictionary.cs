using Newtonsoft.Json;
using Perpetuum.Log;
using System.Globalization;

namespace Perpetuum
{
    public interface ICustomDictionary
    {
        Dictionary<string, object> GetDictionary(int language);
    }

    public class CustomDictionary : ICustomDictionary
    {
        /*
        Language IDs:
            0 = English
            1 = Hungarian
            2 = German
            3 = Portuguese
            4 = Russian
            5 = French
            6 = Spanish
            7 = Polish
            8 = Slovenian
            9 = Romanian
            10 = Norwegian
            11 = Greek
            12 = Finnish
            13 = Italian
            14 = Turkish
            15 = Estonian
            16 = Swedish
            17 = Dutch
        */

        private readonly int _defaultLanguage = 0;
        private readonly Dictionary<int, Dictionary<string, object>> _dictionaries;
        private readonly Dictionary<string, object>? _fallbackDictionary;

        public CustomDictionary(IO.IFileSystem fileManager)
        {
            _dictionaries = [];
            try
            {
                // The template lists keys built into the client. Its extension keeps it out of the language file scan.
                var basePath = Path.Combine("customDictionary", "base.json.template");
                var hasBase = fileManager.Exists(basePath);
                if (!hasBase)
                {
                    Logger.Warning($"[CustomDictionary] '{basePath}' is missing. Using legacy dictionary selection. " +
                        "Languages without their own JSON receive the full English dictionary (0.json) when available.");
                }
                var baseline = hasBase ? ReadDictionary(fileManager, basePath) : null;
                var files = new Dictionary<int, string>();
                foreach (var file in fileManager.GetFiles("customDictionary", "*.json"))
                {
                    if (!int.TryParse(Path.GetFileNameWithoutExtension(file), NumberStyles.None,
                            CultureInfo.InvariantCulture, out var languageId))
                    {
                        Logger.Warning($"[CustomDictionary] Ignoring non-language file: {file}");
                        continue;
                    }
                    if (!files.TryAdd(languageId, file))
                    {
                        throw new InvalidDataException($"Duplicate dictionary language ID: {languageId}");
                    }
                }

                var dictionaries = new Dictionary<int, Dictionary<string, object>>();
                foreach (var (languageId, file) in files)
                {
                    try
                    {
                        dictionaries.Add(languageId, ReadDictionary(fileManager, file));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[CustomDictionary] Cannot load '{file}': {ex.Message}");
                        // With a template, English is required to build the shared fallback.
                        // An unreadable file for any other language can use that fallback instead.
                        if (hasBase && languageId == _defaultLanguage)
                        {
                            throw;
                        }
                    }
                }

                if (!hasBase)
                {
                    // Preserve legacy language selection and full English fallback.
                    _dictionaries = dictionaries;
                    dictionaries.TryGetValue(_defaultLanguage, out _fallbackDictionary);
                    return;
                }

                if (!dictionaries.TryGetValue(_defaultLanguage, out var english))
                {
                    throw new InvalidDataException("English dictionary 0.json is required with base.json.template.");
                }

                // Send English as a fallback only for keys outside the client baseline.
                // This preserves the client's own translations for built-in keys.
                var fallback = new Dictionary<string, object>();
                foreach (var (key, value) in english)
                {
                    if (!baseline.ContainsKey(key))
                    {
                        fallback.Add(key, value);
                    }
                }

                // Prepare the response dictionaries at startup. The selected language wins
                // for every key it defines, including keys in the baseline and empty strings.
                var prepared = new Dictionary<int, Dictionary<string, object>>();
                foreach (var (languageId, dictionary) in dictionaries)
                {
                    var merged = new Dictionary<string, object>(fallback);
                    foreach (var (key, value) in dictionary)
                    {
                        merged[key] = value;
                    }
                    prepared.Add(languageId, merged);
                }

                _dictionaries = prepared;
                _fallbackDictionary = fallback;
            }
            catch (Exception ex)
            {
                Logger.Error($"[CustomDictionary] Dictionaries disabled: {ex.Message}");
            }
        }

        private static Dictionary<string, object> ReadDictionary(IO.IFileSystem fileManager, string file)
        {
            // Read translation values as strings and preserve date-like text verbatim.
            using var textReader = new StringReader(fileManager.ReadAllText(file));
            using var reader = new JsonTextReader(textReader) { DateParseHandling = DateParseHandling.None };
            if (!ReadNextToken(reader) || reader.TokenType != JsonToken.StartObject)
            {
                throw new InvalidDataException($"Dictionary '{file}' must be a JSON object.");
            }

            var result = new Dictionary<string, object>();
            var hasToken = ReadNextToken(reader);
            while (hasToken && reader.TokenType == JsonToken.PropertyName)
            {
                var key = (string)reader.Value!;
                var line = reader.LineNumber;
                if (!ReadNextToken(reader))
                {
                    throw new InvalidDataException($"Missing value for key '{key}' in dictionary '{file}'.");
                }
                // GenXY writes property names verbatim and treats whitespace and delimiters as syntax.
                if (key.Length == 0 || key.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '#' or '|' or '=' or '[' or ']'))
                {
                    reader.Skip();
                    Logger.Warning($"[CustomDictionary] Skipping invalid GenXY key {JsonConvert.ToString(key)} in '{file}' at line {line}.");
                }
                else if (result.ContainsKey(key))
                {
                    // Consume the duplicate value so parsing continues with the next key.
                    // Keep the first value from this file and report the duplicate's location.
                    reader.Skip();
                    Logger.Warning($"[CustomDictionary] Duplicate key '{key}' in '{file}' at line {line}. Keeping the first value.");
                }
                else
                {
                    if (reader.TokenType != JsonToken.String)
                    {
                        throw new InvalidDataException($"Dictionary '{file}', key '{key}' must contain a string.");
                    }
                    result.Add(key, (string)reader.Value!);
                }
                hasToken = ReadNextToken(reader);
            }

            if (!hasToken || reader.TokenType != JsonToken.EndObject)
            {
                throw new InvalidDataException($"Dictionary '{file}' is missing its closing brace.");
            }
            if (ReadNextToken(reader))
            {
                throw new InvalidDataException($"Unexpected content after dictionary '{file}'.");
            }
            return result;
        }

        private static bool ReadNextToken(JsonTextReader reader)
        {
            while (reader.Read())
            {
                if (reader.TokenType != JsonToken.Comment)
                {
                    return true;
                }
            }
            return false;
        }

        public Dictionary<string, object>? GetDictionary(int language)
        {
            if (_dictionaries.TryGetValue(language, out Dictionary<string, object>? value))
            {
                return value;
            }
            // Languages without a server file receive only the fallback prepared at startup.
            return _fallbackDictionary;
        }
    }
}
