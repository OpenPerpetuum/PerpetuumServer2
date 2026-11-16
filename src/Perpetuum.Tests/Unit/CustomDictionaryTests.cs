using NSubstitute;
using Perpetuum.IO;
using Xunit;

namespace Perpetuum.Tests.Unit;

public class CustomDictionaryTests
{
    private static CustomDictionary Create(string? baseline, params (string Name, string Json)[] files)
    {
        var fs = Substitute.For<IFileSystem>();
        var basePath = Path.Combine("customDictionary", "base.json.template");
        fs.Exists(basePath).Returns(baseline != null);
        if (baseline != null) fs.ReadAllText(basePath).Returns(baseline);
        fs.GetFiles("customDictionary", "*.json").Returns(files.Select(f => f.Name).ToArray());
        foreach (var file in files) fs.ReadAllText(file.Name).Returns(file.Json);
        return new CustomDictionary(fs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BaseFiltersFallbackAndLocalOverridesWin(bool reverse)
    {
        var files = new[] { ("0.json", "{\"base\":\"English\",\"new\":\"New\",\"override\":\"English\"}"),
            ("4.json", "{\"override\":\"Русский\",\"local\":\"\",\"base\":\"База\"}") };
        var dictionary = Create("{\"base\":\"Original\",\"clientOnly\":\"Client\"}", reverse ? files.Reverse().ToArray() : files);
        var local = dictionary.GetDictionary(4);
        Assert.Equal(4, local.Count);
        Assert.Equal("База", local["base"]);
        Assert.Equal("Русский", local["override"]);
        Assert.Equal("", local["local"]);
        Assert.Equal("New", local["new"]);
        Assert.False(local.ContainsKey("clientOnly"));
        var fallback = dictionary.GetDictionary(6);
        Assert.Equal(2, fallback.Count);
        Assert.False(fallback.ContainsKey("base"));
        Assert.Equal("English", fallback["override"]);
        Assert.Equal("English", dictionary.GetDictionary(0)["base"]);
        Assert.Equal(4, dictionary.GetDictionary(4).Count);
    }

    [Fact]
    public void MissingBasePreservesLegacySelectionIncludingEmptyLocal()
    {
        var dictionary = Create(null, ("0.json", "{\"a\":\"English\"}"), ("4.json", "{}"));
        Assert.Empty(dictionary.GetDictionary(4));
        Assert.Equal("English", dictionary.GetDictionary(6)["a"]);
        Assert.Null(Create(null, ("4.json", "{}")).GetDictionary(6));
    }

    [Theory]
    [InlineData("broken", "{}")]
    [InlineData("{}", "broken")]
    [InlineData("{\"key\":null}", "{}")]
    [InlineData("{}", "{\"key\":12}")]
    public void InvalidRequiredInputDisablesDictionaries(string baseline, string english)
    {
        var dictionary = Create(baseline, ("4.json", "{\"local\":\"text\"}"), ("0.json", english));
        Assert.Null(dictionary.GetDictionary(4));
        Assert.Null(dictionary.GetDictionary(0));
    }

    [Fact]
    public void MissingEnglishWithBaseDisablesDictionaries()
    {
        Assert.Null(Create("{}", ("4.json", "{}")).GetDictionary(4));
    }

    [Fact]
    public void BadLocalAndUnrelatedJsonDoNotPreventOtherLanguagesLoading()
    {
        var dictionary = Create("{}", ("report.json", "[]"), ("4.json", "broken"),
            ("0.json", "{\"new\":\"English\",}"), ("2.json", "{\"new\":\"Deutsch\"}"));
        Assert.Equal("English", dictionary.GetDictionary(4)["new"]);
        Assert.Equal("Deutsch", dictionary.GetDictionary(2)["new"]);
    }

    [Fact]
    public void DuplicateKeysKeepFirstValueAndAllowLoadingToContinue()
    {
        var dictionary = Create("{\"base\":\"Original\",\"base\":\"Duplicate\"}",
            ("0.json", "{\"base\":\"English\",\"new\":\"First\",\"new\":\"Second\",\"after\":\"Next\"}"),
            ("4.json", "{\"new\":\"\",\"new\":\"Duplicate\"}"));

        Assert.Equal("First", dictionary.GetDictionary(0)["new"]);
        Assert.Equal("", dictionary.GetDictionary(4)["new"]);
        Assert.Equal("Next", dictionary.GetDictionary(4)["after"]);
        Assert.False(dictionary.GetDictionary(6).ContainsKey("base"));
    }

    [Fact]
    public void InvalidGenxyKeysAreSkippedWithoutDiscardingValidEntries()
    {
        var dictionary = Create("""{"client":"Built in","bad base":"Ignored"}""",
            ("0.json", """{"bad key":"Ignored","bad\u0023key":"Ignored","bad|key":"Ignored","bad=key":"Ignored","bad[key":"Ignored","bad]key":"Ignored","bad\tkey":"Ignored","":"Ignored","client":"English","new_key":"New"}"""),
            ("4.json", """{"bad\nkey":{"unused":"Ignored"},"new_key":"Новый","tierlevel_t2+":"T2+"}"""));

        Assert.Equal(new[] { "client", "new_key" }, dictionary.GetDictionary(0).Keys.OrderBy(key => key));
        Assert.Equal(new[] { "new_key", "tierlevel_t2+" }, dictionary.GetDictionary(4).Keys.OrderBy(key => key));
        Assert.Equal("Новый", dictionary.GetDictionary(4)["new_key"]);
        Assert.Equal("New", Assert.Single(dictionary.GetDictionary(6)).Value);
    }

    [Fact]
    public void DuplicateLanguageIdsDisableLoadRegardlessOfOrder()
    {
        Assert.Null(Create("{}", ("0.json", "{}"), ("04.json", "{}"), ("4.json", "{}")).GetDictionary(0));
    }
}
