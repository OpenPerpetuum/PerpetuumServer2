using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class TranslationsViewModel : ObservableObject
    {
        private readonly AppSettingsStore _settings;

        [ObservableProperty] private TranslationStore? _store;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;
        [ObservableProperty] private TranslationRow? _selectedRow;

        public TranslationsViewModel(AppSettingsStore settings)
        {
            _settings = settings;
        }

        public ObservableCollection<int> Languages =>
            Store?.Languages ?? new ObservableCollection<int>();

        public ObservableCollection<TranslationRow> Rows =>
            Store?.Rows ?? new ObservableCollection<TranslationRow>();

        public bool HasGameRoot =>
            !string.IsNullOrWhiteSpace(_settings.Settings.GameRootPath);

        public bool HasDictionaryDir =>
            Store != null && Store.DirectoryExists;

        public void Load()
        {
            try
            {
                Store = new TranslationStore(_settings.Settings.GameRootPath);
                Store.Load();

                if (!HasGameRoot)
                {
                    StatusIsError = true;
                    StatusMessage =
                        "GameRoot path is not configured. Set it in Connection settings, then click Reload.";
                    return;
                }

                if (!HasDictionaryDir)
                {
                    StatusIsError = true;
                    StatusMessage =
                        $"customDictionary folder not found under '{_settings.Settings.GameRootPath}'. " +
                        "It will be created on first save.";
                    return;
                }

                StatusIsError = false;
                StatusMessage =
                    $"Loaded {Rows.Count} key(s) across {Languages.Count} language(s) " +
                    $"from {Store.DictionaryDirectory}.";

                OnPropertyChanged(nameof(Rows));
                OnPropertyChanged(nameof(Languages));
                OnPropertyChanged(nameof(HasDictionaryDir));
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Failed to load translations: {ex.Message}";
            }
        }

        public bool TryAddKey(string key, out string error)
        {
            if (Store == null)
            {
                error = "Translations not loaded.";
                return false;
            }
            return Store.TryAddKey(key, out error);
        }

        public bool TryAddLanguage(int langId, out string error)
        {
            if (Store == null)
            {
                error = "Translations not loaded.";
                return false;
            }
            return Store.TryAddLanguage(langId, out error);
        }

        public void RemoveSelected()
        {
            if (Store == null || SelectedRow == null) return;
            Store.RemoveRow(SelectedRow);
            SelectedRow = null;
        }

        public void Save()
        {
            if (Store == null) return;
            try
            {
                Store.Save();
                StatusIsError = false;
                StatusMessage =
                    $"Saved {Languages.Count} language file(s) to {Store.DictionaryDirectory}.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Save failed: {ex.Message}";
            }
        }
    }
}
