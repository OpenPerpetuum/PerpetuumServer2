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

        public string this[int langId]
        {
            get => _values.TryGetValue(langId, out string? value) ? value : "";
            set
            {
                string normalized = value ?? "";
                if (this[langId] == normalized) return;
                if (string.IsNullOrEmpty(normalized)) _values.Remove(langId);
                else _values[langId] = normalized;
                OnPropertyChanged("Item[]");
            }
        }

        public IReadOnlyDictionary<int, string> Values => _values;
        public bool HasValue(int langId) => _values.ContainsKey(langId);
    }
}
