using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class TranslationCatalogViewModel : ObservableObject
{
    private readonly AppSettingsStore _settingsStore;
    private TranslationStore? _store;
    private readonly List<TranslationRow> _allRows = new();

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private TranslationRow? _selectedRow;
    [ObservableProperty] private string _newKey = string.Empty;
    [ObservableProperty] private LanguageInfo? _selectedNewLanguage;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage =
        "Set the game root in connection settings, then load customDictionary files.";

    public TranslationCatalogViewModel(AppSettingsStore settingsStore) => _settingsStore = settingsStore;

    public ObservableCollection<TranslationRow> Rows { get; } = new();
    public ObservableCollection<TranslationValueEditorViewModel> Values { get; } = new();
    public ObservableCollection<LanguageInfo> AvailableLanguages { get; } = new();
    public string GameRootPath => _settingsStore.Settings.GameRootPath;
    public bool IsNotLoading => !IsLoading;

    partial void OnFilterTextChanged(string value) => RebuildFilteredRows();
    partial void OnSelectedRowChanged(TranslationRow? value) => RebuildValueEditors();
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

    [RelayCommand]
    private void Load()
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusIsError = false;
        try
        {
            string gameRoot = _settingsStore.Settings.GameRootPath.Trim();
            if (string.IsNullOrEmpty(gameRoot))
            {
                SetError("Game root is not configured. Enter it above and save settings first.");
                return;
            }
            _store = new TranslationStore(gameRoot);
            _store.Load();
            _allRows.Clear();
            _allRows.AddRange(_store.Rows);
            RebuildAvailableLanguages();
            RebuildFilteredRows();
            SelectedRow = Rows.FirstOrDefault();
            OnPropertyChanged(nameof(GameRootPath));
            StatusMessage = _store.DirectoryExists
                ? $"Loaded {_allRows.Count} key(s) across {_store.Languages.Count} language(s) from {_store.DictionaryDirectory}."
                : $"No customDictionary directory exists yet. Saving will create {_store.DictionaryDirectory}.";
        }
        catch (Exception ex)
        {
            SetError($"Unable to load translations: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddKey()
    {
        if (_store == null)
        {
            SetError("Load translations first.");
            return;
        }
        if (!_store.TryAddKey(NewKey, out string error))
        {
            SetError(error);
            return;
        }
        TranslationRow row = _store.Rows[0];
        _allRows.Insert(0, row);
        NewKey = string.Empty;
        FilterText = string.Empty;
        RebuildFilteredRows();
        SelectedRow = row;
        StatusIsError = false;
        StatusMessage = $"Added unsaved translation key '{row.Key}'.";
    }

    [RelayCommand]
    private void AddLanguage()
    {
        if (_store == null || SelectedNewLanguage == null)
        {
            SetError("Select an available language first.");
            return;
        }
        LanguageInfo language = SelectedNewLanguage;
        if (!_store.TryAddLanguage(language.Id, out string error))
        {
            SetError(error);
            return;
        }
        RebuildAvailableLanguages();
        RebuildValueEditors();
        StatusIsError = false;
        StatusMessage = $"Added unsaved language [{language.Id}] {language.Name}.";
    }

    [RelayCommand]
    private void RemoveSelectedKey()
    {
        if (_store == null || SelectedRow == null)
        {
            SetError("Select a translation key first.");
            return;
        }
        TranslationRow row = SelectedRow;
        _store.RemoveRow(row);
        _allRows.Remove(row);
        Rows.Remove(row);
        SelectedRow = Rows.FirstOrDefault();
        StatusIsError = false;
        StatusMessage = $"Removed key '{row.Key}' in memory. Save to persist.";
    }

    [RelayCommand]
    private void Save()
    {
        if (_store == null)
        {
            SetError("Load translations first.");
            return;
        }
        try
        {
            _store.Save();
            StatusIsError = false;
            StatusMessage = $"Saved {_store.Languages.Count} language file(s) to {_store.DictionaryDirectory}.";
        }
        catch (Exception ex)
        {
            SetError($"Unable to save translations: {ex.Message}");
        }
    }

    private void RebuildFilteredRows()
    {
        string filter = FilterText.Trim();
        IEnumerable<TranslationRow> filtered = string.IsNullOrEmpty(filter)
            ? _allRows
            : _allRows.Where(row => row.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                row.Values.Values.Any(value => value.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        TranslationRow? selected = SelectedRow;
        Rows.Clear();
        foreach (TranslationRow row in filtered) Rows.Add(row);
        SelectedRow = selected != null && Rows.Contains(selected) ? selected : Rows.FirstOrDefault();
    }

    private void RebuildValueEditors()
    {
        Values.Clear();
        if (_store == null || SelectedRow == null) return;
        foreach (int languageId in _store.Languages)
            Values.Add(new TranslationValueEditorViewModel(SelectedRow, languageId));
    }

    private void RebuildAvailableLanguages()
    {
        AvailableLanguages.Clear();
        if (_store == null) return;
        foreach (int id in _store.UnusedLanguages())
            AvailableLanguages.Add(LanguageCatalog.All.First(language => language.Id == id));
        SelectedNewLanguage = AvailableLanguages.FirstOrDefault();
    }

    private void SetError(string message)
    {
        StatusIsError = true;
        StatusMessage = message;
    }
}

public partial class TranslationValueEditorViewModel : ObservableObject
{
    private readonly TranslationRow _row;

    [ObservableProperty] private string _value;

    public TranslationValueEditorViewModel(TranslationRow row, int languageId)
    {
        _row = row;
        LanguageId = languageId;
        LanguageName = LanguageCatalog.NameOf(languageId);
        _value = row[languageId];
    }

    public int LanguageId { get; }
    public string LanguageName { get; }
    public string DisplayName => $"[{LanguageId}] {LanguageName}";

    partial void OnValueChanged(string value) => _row[LanguageId] = value;
}
