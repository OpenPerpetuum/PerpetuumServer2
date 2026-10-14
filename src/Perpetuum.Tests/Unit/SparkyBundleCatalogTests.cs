using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyBundleCatalogTests
{
    [Fact]
    public async Task Stages_validated_bundle_under_an_immutable_versioned_name()
    {
        using var temp = new TemporaryDirectory();
        var bundle = Bundle("bundle-1234567890abcdef");
        var catalog = new SparkyBundleCatalog(temp.Path, _ => bundle);
        byte[] content = "bundle bytes"u8.ToArray();

        SparkyBundleInfo staged = await catalog.StageAsync(new MemoryStream(content), TestContext.Current.CancellationToken);

        Assert.Equal(bundle.Manifest.BundleVersion, staged.Version);
        Assert.Equal(content.Length, staged.SizeBytes);
        Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(temp.Path, staged.FileName), TestContext.Current.CancellationToken));
        Assert.Same(bundle, catalog.GetRequired(staged.Version));
        Assert.Single(catalog.List());
    }

    [Fact]
    public async Task Repeated_identical_version_is_idempotent_but_version_collision_is_rejected()
    {
        using var temp = new TemporaryDirectory();
        var bundle = Bundle("bundle-1234567890abcdef");
        var catalog = new SparkyBundleCatalog(temp.Path, _ => bundle);
        byte[] original = "bundle bytes"u8.ToArray();
        await catalog.StageAsync(new MemoryStream(original), CancellationToken.None);

        await catalog.StageAsync(new MemoryStream(original), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.StageAsync(
            new MemoryStream("different bytes"u8.ToArray()), CancellationToken.None));
    }

    [Fact]
    public async Task Staging_does_not_replace_a_registered_legacy_bundle_with_the_same_version()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        string legacyPath = Path.Combine(temp.Path, "legacy.json");
        await File.WriteAllBytesAsync(legacyPath, "legacy bundle bytes"u8.ToArray(), cancellationToken);
        const string version = "legacy-1234567890abcdef";
        var legacyBundle = Bundle(version);
        var stagedBundle = Bundle(version);
        var catalog = new SparkyBundleCatalog(temp.Path, _ => stagedBundle);
        catalog.RegisterExisting(version, legacyPath, legacyBundle);

        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.StageAsync(
            new MemoryStream("different staged bytes"u8.ToArray()), cancellationToken));

        Assert.Same(legacyBundle, catalog.GetRequired(version));
        Assert.False(File.Exists(Path.Combine(temp.Path, version + ".json")));
    }

    [Fact]
    public async Task Rejects_path_like_bundle_versions_and_oversized_uploads()
    {
        using var temp = new TemporaryDirectory();
        var catalog = new SparkyBundleCatalog(temp.Path, _ => Bundle("../escape"));
        await Assert.ThrowsAsync<InvalidDataException>(() => catalog.StageAsync(
            new MemoryStream("bundle"u8.ToArray()), CancellationToken.None));

        var boundedCatalog = new SparkyBundleCatalog(temp.Path, _ => Bundle("bundle-1234567890abcdef"), maximumBytes: 2);
        await Assert.ThrowsAsync<InvalidDataException>(() => boundedCatalog.StageAsync(
            new MemoryStream("larger"u8.ToArray()), CancellationToken.None));
    }

    private static SparkyKnowledgeBundle Bundle(string version) => new(
        SchemaVersion: 3,
        Manifest: new SparkyBundleManifest(version, "revision", DateTimeOffset.UtcNow,
            new Dictionary<int, string>(), new Dictionary<string, string>()),
        Languages: [], Items: [], ProductionRecipes: [], KnowledgePassages: [],
        SearchIndex: new SparkySearchIndex(new Dictionary<string, IReadOnlyList<int>>(), []));

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sparky-bundles-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
