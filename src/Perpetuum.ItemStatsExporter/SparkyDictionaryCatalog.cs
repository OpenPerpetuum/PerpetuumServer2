using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Perpetuum;

namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyDictionaryCatalog
{
    private readonly IReadOnlyDictionary<int, SparkyDictionaryLanguage> _languagesById;

    private SparkyDictionaryCatalog(IReadOnlyDictionary<int, SparkyDictionaryLanguage> languagesById)
    {
        _languagesById = languagesById;
        Languages = languagesById.Values.OrderBy(language => language.LanguageId).ToArray();
    }

    public IReadOnlyList<SparkyDictionaryLanguage> Languages { get; }

    public SparkyDictionaryLanguage English => _languagesById[0];

    public bool TryGetLanguage(int languageId, out SparkyDictionaryLanguage language)
    {
        return _languagesById.TryGetValue(languageId, out language!);
    }

    public string? GetTranslation(int languageId, string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || !_languagesById.TryGetValue(languageId, out var language))
        {
            return null;
        }

        return language.Translations.TryGetValue(key, out var translation) &&
               !string.IsNullOrWhiteSpace(translation)
            ? translation
            : null;
    }

    public static SparkyDictionaryCatalog Load(string customDictionaryDirectory)
    {
        if (string.IsNullOrWhiteSpace(customDictionaryDirectory))
        {
            throw new InvalidDataException("A custom-dictionary directory is required.");
        }

        string directory = Path.GetFullPath(customDictionaryDirectory);
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Custom-dictionary directory was not found: {directory}");
        }

        var dictionaryFiles = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (dictionaryFiles.Length == 0)
        {
            throw new InvalidDataException($"No custom-dictionary JSON files were found in {directory}.");
        }

        var languages = new Dictionary<int, SparkyDictionaryLanguage>();
        foreach (string path in dictionaryFiles)
        {
            string languageName = Path.GetFileNameWithoutExtension(path);
            if (!int.TryParse(languageName, NumberStyles.None, CultureInfo.InvariantCulture, out int languageId) || languageId < 0)
            {
                throw new InvalidDataException($"Custom-dictionary filename '{Path.GetFileName(path)}' must use a numeric game language ID.");
            }

            if (!CustomDictionary.LanguageNames.TryGetValue(languageId, out string? displayName))
            {
                throw new InvalidDataException($"Custom-dictionary language ID {languageId} is not defined by the game.");
            }

            if (languages.ContainsKey(languageId))
            {
                throw new InvalidDataException($"More than one custom-dictionary file maps to language ID {languageId}.");
            }

            var translations = ReadDictionary(path);
            languages.Add(languageId, new SparkyDictionaryLanguage(
                languageId,
                displayName,
                ComputeDictionaryHash(translations),
                translations));
        }

        if (!languages.TryGetValue(0, out var english))
        {
            throw new InvalidDataException("The canonical custom dictionary customDictionary/0.json is required.");
        }

        if (english.Translations.Count == 0)
        {
            throw new InvalidDataException("The canonical English dictionary customDictionary/0.json is empty.");
        }

        var canonicalKeys = new HashSet<string>(english.Translations.Keys, StringComparer.Ordinal);
        foreach (var languageId in languages.Keys.Where(languageId => languageId != 0).ToArray())
        {
            var language = languages[languageId];
            var canonicalTranslations = language.Translations
                .Where(entry => canonicalKeys.Contains(entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            languages[languageId] = language with
            {
                DictionaryHash = ComputeDictionaryHash(canonicalTranslations),
                Translations = canonicalTranslations
            };
        }

        return new SparkyDictionaryCatalog(languages);
    }

    public IReadOnlyList<SparkySupportedLanguage> ToSupportedLanguages()
    {
        return Languages.Select(language => new SparkySupportedLanguage(
            language.LanguageId,
            language.DisplayName,
            language.DictionaryHash,
            new Dictionary<string, string>(language.Translations, StringComparer.Ordinal))).ToArray();
    }

    public static string ComputeDictionaryHash(IReadOnlyDictionary<string, string> translations)
    {
        var ordered = translations.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(ordered)));
    }

    private static Dictionary<string, string> ReadDictionary(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Custom dictionary '{Path.GetFileName(path)}' must contain a JSON object.");
        }

        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name) || property.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"Custom dictionary '{Path.GetFileName(path)}' must contain only named string translations.");
            }

            if (!translations.TryAdd(property.Name, property.Value.GetString() ?? string.Empty))
            {
                throw new InvalidDataException($"Custom dictionary '{Path.GetFileName(path)}' contains a duplicate key '{property.Name}'.");
            }
        }

        return translations;
    }
}

internal sealed record SparkyDictionaryLanguage(
    int LanguageId,
    string DisplayName,
    string DictionaryHash,
    IReadOnlyDictionary<string, string> Translations);
