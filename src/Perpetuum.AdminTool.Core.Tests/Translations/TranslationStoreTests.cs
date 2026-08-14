using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.Core.Tests.Translations;

public sealed class TranslationStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "perpetuum-translation-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_MergesLanguageFilesAndSkipsMalformedJson()
    {
        string dictionary = Path.Combine(_root, TranslationStore.DictionaryDirName);
        Directory.CreateDirectory(dictionary);
        File.WriteAllText(Path.Combine(dictionary, "0.json"), "{\"hello\":\"Hello\",\"shared\":\"EN\"}");
        File.WriteAllText(Path.Combine(dictionary, "2.json"), "{\"hello\":\"Hallo\",\"shared\":\"DE\"}");
        File.WriteAllText(Path.Combine(dictionary, "3.json"), "not-json");

        var store = new TranslationStore(_root);
        store.Load();

        Assert.Equal([0, 2], store.Languages);
        TranslationRow hello = Assert.Single(store.Rows, row => row.Key == "hello");
        Assert.Equal("Hello", hello[0]);
        Assert.Equal("Hallo", hello[2]);
    }

    [Fact]
    public void Save_WritesUtf8FilesAtomicallyAndRoundTrips()
    {
        var store = new TranslationStore(_root);
        Assert.True(store.TryAddLanguage(0, out _));
        Assert.True(store.TryAddLanguage(2, out _));
        Assert.True(store.TryAddKey("welcome", out _));
        store.Rows[0][0] = "Welcome";
        store.Rows[0][2] = "Willkommen";

        store.Save();

        string englishPath = Path.Combine(store.DictionaryDirectory, "0.json");
        Assert.True(File.Exists(englishPath));
        Assert.False(File.ReadAllBytes(englishPath).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Empty(Directory.GetFiles(store.DictionaryDirectory, "*.tmp-*"));
        var reloaded = new TranslationStore(_root);
        reloaded.Load();
        Assert.Equal("Willkommen", Assert.Single(reloaded.Rows)[2]);
    }

    [Fact]
    public void RenamedKey_IsIndexedAndDuplicateRenameIsRejectedOnSave()
    {
        var store = new TranslationStore(_root);
        Assert.True(store.TryAddKey("first", out _));
        Assert.True(store.TryAddKey("second", out _));
        store.Rows[0].Key = "first";

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Save());

        Assert.Contains("Duplicate translation key", exception.Message);
    }

    [Fact]
    public void AddLanguage_RejectsUnknownIds()
    {
        var store = new TranslationStore(_root);

        Assert.False(store.TryAddLanguage(99, out string error));
        Assert.Contains("not supported", error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
