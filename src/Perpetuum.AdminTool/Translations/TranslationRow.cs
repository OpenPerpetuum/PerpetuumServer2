using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Translations
{
    public class TranslationRow : ObservableObject
    {
        private string _key = "";
        private readonly Dictionary<int, string> _values = new();

        public string Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }

        // Indexer so DataGrid columns can bind via Path=[langId].
        public string this[int langId]
        {
            get => _values.TryGetValue(langId, out var v) ? v : "";
            set
            {
                var current = this[langId];
                var normalized = value ?? "";
                if (current == normalized) return;

                if (string.IsNullOrEmpty(normalized))
                {
                    _values.Remove(langId);
                }
                else
                {
                    _values[langId] = normalized;
                }
                // WPF binding listens for "Item[]" to refresh indexer-bound paths.
                OnPropertyChanged("Item[]");
            }
        }

        public IReadOnlyDictionary<int, string> Values => _values;

        public bool HasValue(int langId) => _values.ContainsKey(langId);
    }
}
