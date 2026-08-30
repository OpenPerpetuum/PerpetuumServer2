using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Perpetuum.AdminTool.Translations
{
    public class TranslationStore
    {
        public const string DictionaryDirName = "customDictionary";

        public string GameRoot { get; }
        public ObservableCollection<int> Languages { get; } = new();
        public ObservableCollection<TranslationRow> Rows { get; } = new();

        private readonly Dictionary<string, TranslationRow> _byKey = new(StringComparer.Ordinal);

        public TranslationStore(string gameRoot)
        {
            GameRoot = gameRoot ?? "";
        }

        public string DictionaryDirectory => Path.Combine(GameRoot, DictionaryDirName);

        public bool DirectoryExists =>
            !string.IsNullOrWhiteSpace(GameRoot) && Directory.Exists(DictionaryDirectory);

        public void Load()
        {
            Languages.Clear();
            Rows.Clear();
            _byKey.Clear();

            if (!DirectoryExists) return;

            var files = Directory
                .GetFiles(DictionaryDirectory, "*.json")
                .OrderBy(f => f, StringComparer.Ordinal);

            var langs = new List<int>();

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!int.TryParse(name, out var langId)) continue;

                var json = File.ReadAllText(file);
                Dictionary<string, object>? dict;
                try
                {
                    dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                }
                catch
                {
                    // Skip malformed file rather than abort.
                    continue;
                }
                if (dict == null) continue;

                langs.Add(langId);

                foreach (var kv in dict)
                {
                    if (!_byKey.TryGetValue(kv.Key, out var row))
                    {
                        row = new TranslationRow { Key = kv.Key };
                        _byKey[kv.Key] = row;
                        Rows.Add(row);
                    }
                    row[langId] = kv.Value?.ToString() ?? "";
                }
            }

            foreach (var id in langs.OrderBy(x => x))
            {
                Languages.Add(id);
            }
        }

        public bool TryAddKey(string key, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Key cannot be empty.";
                return false;
            }
            if (_byKey.ContainsKey(key))
            {
                error = "A row with that key already exists.";
                return false;
            }
            var row = new TranslationRow { Key = key };
            _byKey[key] = row;
            Rows.Insert(0, row);
            return true;
        }

        public void RemoveRow(TranslationRow row)
        {
            if (row == null) return;
            _byKey.Remove(row.Key);
            Rows.Remove(row);
        }

        public bool TryAddLanguage(int langId, out string error)
        {
            error = "";
            if (Languages.Contains(langId))
            {
                error = "Language is already present.";
                return false;
            }

            // Insert keeping the list sorted.
            var index = 0;
            while (index < Languages.Count && Languages[index] < langId) index++;
            Languages.Insert(index, langId);
            return true;
        }

        public IEnumerable<int> UnusedLanguages() =>
            LanguageCatalog.All
                .Select(l => l.Id)
                .Where(id => !Languages.Contains(id));

        public void Save()
        {
            if (string.IsNullOrWhiteSpace(GameRoot))
            {
                throw new InvalidOperationException("GameRoot path is not configured.");
            }
            Directory.CreateDirectory(DictionaryDirectory);

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            foreach (var langId in Languages)
            {
                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var row in Rows)
                {
                    if (string.IsNullOrEmpty(row.Key)) continue;
                    if (!row.HasValue(langId)) continue;

                    var val = row[langId];
                    if (string.IsNullOrEmpty(val)) continue;

                    dict[row.Key] = val;
                }

                var json = JsonConvert.SerializeObject(dict, Formatting.Indented);
                var path = Path.Combine(DictionaryDirectory, $"{langId}.json");
                File.WriteAllText(path, json, encoding);
            }
        }
    }
}
