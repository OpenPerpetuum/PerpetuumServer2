using System.Collections.ObjectModel;
using System.Text;
using Newtonsoft.Json;

namespace Perpetuum.AdminTool.Translations
{
    public class TranslationStore
    {
        public const string DictionaryDirName = "customDictionary";

        private readonly Dictionary<string, TranslationRow> _byKey = new(StringComparer.Ordinal);

        public TranslationStore(string gameRoot) => GameRoot = gameRoot ?? "";

        public string GameRoot { get; }
        public ObservableCollection<int> Languages { get; } = new();
        public ObservableCollection<TranslationRow> Rows { get; } = new();
        public string DictionaryDirectory => Path.Combine(GameRoot, DictionaryDirName);
        public bool DirectoryExists =>
            !string.IsNullOrWhiteSpace(GameRoot) && Directory.Exists(DictionaryDirectory);

        public void Load()
        {
            Languages.Clear();
            Rows.Clear();
            _byKey.Clear();
            if (!DirectoryExists) return;

            var languageIds = new SortedSet<int>();
            foreach (string file in Directory.GetFiles(DictionaryDirectory, "*.json")
                .OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!int.TryParse(Path.GetFileNameWithoutExtension(file), out int languageId)) continue;
                Dictionary<string, object>? dictionary;
                try
                {
                    dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                        File.ReadAllText(file));
                }
                catch
                {
                    continue;
                }
                if (dictionary == null) continue;
                languageIds.Add(languageId);
                foreach ((string key, object? value) in dictionary)
                {
                    if (!_byKey.TryGetValue(key, out TranslationRow? row))
                    {
                        row = new TranslationRow { Key = key };
                        _byKey[key] = row;
                        Rows.Add(row);
                    }
                    row[languageId] = value?.ToString() ?? "";
                }
            }
            foreach (int id in languageIds) Languages.Add(id);
        }

        public bool TryAddKey(string key, out string error)
        {
            key = key.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Key cannot be empty.";
                return false;
            }
            if (!TryRebuildKeyIndex(out error)) return false;
            if (_byKey.ContainsKey(key))
            {
                error = "A row with that key already exists.";
                return false;
            }
            var row = new TranslationRow { Key = key };
            _byKey[key] = row;
            Rows.Insert(0, row);
            error = "";
            return true;
        }

        public void RemoveRow(TranslationRow? row)
        {
            if (row == null) return;
            Rows.Remove(row);
            TryRebuildKeyIndex(out _);
        }

        public bool TryAddLanguage(int languageId, out string error)
        {
            if (!LanguageCatalog.All.Any(language => language.Id == languageId))
            {
                error = $"Language id {languageId} is not supported.";
                return false;
            }
            if (Languages.Contains(languageId))
            {
                error = "Language is already present.";
                return false;
            }
            int index = 0;
            while (index < Languages.Count && Languages[index] < languageId) index++;
            Languages.Insert(index, languageId);
            error = "";
            return true;
        }

        public IEnumerable<int> UnusedLanguages() =>
            LanguageCatalog.All.Select(language => language.Id).Where(id => !Languages.Contains(id));

        public void Save()
        {
            if (string.IsNullOrWhiteSpace(GameRoot))
                throw new InvalidOperationException("GameRoot path is not configured.");
            if (!TryRebuildKeyIndex(out string error)) throw new InvalidOperationException(error);
            Directory.CreateDirectory(DictionaryDirectory);
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            foreach (int languageId in Languages)
            {
                var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (TranslationRow row in Rows)
                {
                    if (!row.HasValue(languageId)) continue;
                    string value = row[languageId];
                    if (!string.IsNullOrEmpty(value)) dictionary[row.Key] = value;
                }
                string json = JsonConvert.SerializeObject(dictionary, Formatting.Indented);
                string path = Path.Combine(DictionaryDirectory, $"{languageId}.json");
                string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.WriteAllText(temporaryPath, json, encoding);
                    File.Move(temporaryPath, path, overwrite: true);
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
        }

        private bool TryRebuildKeyIndex(out string error)
        {
            _byKey.Clear();
            foreach (TranslationRow row in Rows)
            {
                row.Key = row.Key.Trim();
                if (string.IsNullOrEmpty(row.Key))
                {
                    error = "Translation keys cannot be empty.";
                    return false;
                }
                if (!_byKey.TryAdd(row.Key, row))
                {
                    error = $"Duplicate translation key '{row.Key}'.";
                    return false;
                }
            }
            error = "";
            return true;
        }
    }
}
