using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class TranslationCatalogViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "perpetuum-native-translation-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoadFilterAndEdit_UsePortableTranslationStore()
    {
        string dictionary = Path.Combine(_root, TranslationStore.DictionaryDirName);
        Directory.CreateDirectory(dictionary);
        File.WriteAllText(Path.Combine(dictionary, "0.json"),
            "{\"welcome_title\":\"Welcome pilot\",\"other\":\"Other text\"}");
        TranslationCatalogViewModel viewModel = CreateViewModel();

        viewModel.LoadCommand.Execute(null);
        viewModel.FilterText = "pilot";

        Assert.Single(viewModel.Rows);
        Assert.Equal("welcome_title", viewModel.Rows[0].Key);
        TranslationValueEditorViewModel editor = Assert.Single(viewModel.Values);
        editor.Value = "Welcome capsuleer";
        Assert.Equal("Welcome capsuleer", viewModel.Rows[0][0]);
    }

    [Fact]
    public void AddLanguageKeyAndSave_RoundTripsNativeEdits()
    {
        Directory.CreateDirectory(_root);
        TranslationCatalogViewModel viewModel = CreateViewModel();
        viewModel.LoadCommand.Execute(null);
        viewModel.SelectedNewLanguage = viewModel.AvailableLanguages.Single(language => language.Id == 0);
        viewModel.AddLanguageCommand.Execute(null);
        viewModel.NewKey = "tutorial_target";
        viewModel.AddKeyCommand.Execute(null);
        Assert.Single(viewModel.Values).Value = "Acquire the marked target";

        viewModel.SaveCommand.Execute(null);

        var reloaded = new TranslationStore(_root);
        reloaded.Load();
        Assert.Equal("Acquire the marked target", Assert.Single(reloaded.Rows)[0]);
    }

    [Fact]
    public void RemoveSelectedKey_UpdatesVisibleAndStoredRows()
    {
        string dictionary = Path.Combine(_root, TranslationStore.DictionaryDirName);
        Directory.CreateDirectory(dictionary);
        File.WriteAllText(Path.Combine(dictionary, "0.json"), "{\"obsolete\":\"Old\"}");
        TranslationCatalogViewModel viewModel = CreateViewModel();
        viewModel.LoadCommand.Execute(null);

        viewModel.RemoveSelectedKeyCommand.Execute(null);
        viewModel.SaveCommand.Execute(null);

        Assert.Empty(viewModel.Rows);
        var reloaded = new TranslationStore(_root);
        reloaded.Load();
        Assert.Empty(reloaded.Rows);
    }

    private TranslationCatalogViewModel CreateViewModel()
    {
        var settings = new AppSettingsStore(Path.Combine(_root, "settings.json"));
        settings.Settings.GameRootPath = _root;
        return new TranslationCatalogViewModel(settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
