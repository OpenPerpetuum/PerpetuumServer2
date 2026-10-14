using System.Security.Cryptography;

namespace Perpetuum.ItemStatsExporter;

internal interface ISparkyBundleCatalog
{
    void RegisterExisting(string version, string path, SparkyKnowledgeBundle bundle);
    IReadOnlyList<SparkyBundleInfo> List();
    SparkyKnowledgeBundle GetRequired(string version);
    Task<SparkyBundleInfo> StageAsync(Stream input, CancellationToken cancellationToken);
}

internal sealed class SparkyBundleCatalog : ISparkyBundleCatalog
{
    public const long DefaultMaximumBytes = 100L * 1024 * 1024;
    private readonly string _root;
    private readonly Func<string, SparkyKnowledgeBundle> _loader;
    private readonly long _maximumBytes;
    private readonly object _gate = new();
    private readonly Dictionary<string, SparkyKnowledgeBundle> _loaded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _existingPaths = new(StringComparer.Ordinal);

    public SparkyBundleCatalog(
        string root,
        Func<string, SparkyKnowledgeBundle> loader,
        long maximumBytes = DefaultMaximumBytes)
    {
        if (string.IsNullOrWhiteSpace(root) || maximumBytes is < 1 or > DefaultMaximumBytes)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        _root = Path.GetFullPath(root);
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _maximumBytes = maximumBytes;
        Directory.CreateDirectory(_root);
        AssertNotReparsePoint(_root);
    }

    public void RegisterExisting(string version, string path, SparkyKnowledgeBundle bundle)
    {
        ValidateVersion(version);
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath) || !string.Equals(bundle.Manifest.BundleVersion, version, StringComparison.Ordinal))
            throw new InvalidDataException("The existing Sparky bundle does not match its registered version.");
        lock (_gate)
        {
            _existingPaths[version] = fullPath;
            _loaded[version] = bundle;
        }
    }

    public IReadOnlyList<SparkyBundleInfo> List()
    {
        DirectoryInfo root = new(_root);
        if (!root.Exists)
            return [];
        AssertNotReparsePoint(_root);
        return root.EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
            .Where(file => !file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .Select(file => new SparkyBundleInfo(
                Path.GetFileNameWithoutExtension(file.Name),
                file.Name,
                file.Length,
                new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)))
            .Where(info => IsSafeVersion(info.Version))
            .OrderByDescending(info => info.StagedAtUtc)
            .Take(100)
            .ToArray();
    }

    public SparkyKnowledgeBundle GetRequired(string version)
    {
        ValidateVersion(version);
        lock (_gate)
        {
            if (_loaded.TryGetValue(version, out var loaded))
                return loaded;

            string path = _existingPaths.TryGetValue(version, out var existingPath)
                ? existingPath
                : Path.Combine(_root, version + ".json");
            string fullPath = Path.GetFullPath(path);
            if (!_existingPaths.ContainsKey(version) && !fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The requested Sparky bundle is outside the bundle catalog.");
            var file = new FileInfo(fullPath);
            if (!file.Exists || file.Length is <= 0 || file.Length > _maximumBytes || file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("The requested Sparky bundle is unavailable or exceeds the configured size limit.");

            // The loader is the repository's validated bundle reader; it runs the full schema validator.
            SparkyKnowledgeBundle bundle = _loader(fullPath);
            if (!string.Equals(bundle.Manifest.BundleVersion, version, StringComparison.Ordinal))
                throw new InvalidDataException("The Sparky bundle version does not match the catalog entry.");
            _loaded[version] = bundle;
            return bundle;
        }
    }

    public async Task<SparkyBundleInfo> StageAsync(Stream input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        string temporaryPath = Path.Combine(_root, ".stage-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                var buffer = new byte[64 * 1024];
                long total = 0;
                while (true)
                {
                    int read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        break;
                    total += read;
                    if (total > _maximumBytes)
                        throw new InvalidDataException("The uploaded Sparky bundle exceeds the 100 MB limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }

                if (total == 0)
                    throw new InvalidDataException("The uploaded Sparky bundle is empty.");
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }

            // The loader is the repository's validated bundle reader; no unvalidated object enters the catalog.
            SparkyKnowledgeBundle bundle = _loader(temporaryPath);
            string version = bundle.Manifest.BundleVersion;
            ValidateVersion(version);
            string destination = Path.Combine(_root, version + ".json");
            lock (_gate)
            {
                if (_existingPaths.TryGetValue(version, out var registeredPath) &&
                    !FilesEqual(registeredPath, temporaryPath))
                {
                    throw new InvalidDataException("A different bundle is already registered with this immutable version identifier.");
                }

                if (File.Exists(destination))
                {
                    if (!FilesEqual(destination, temporaryPath))
                        throw new InvalidDataException("A different bundle already exists with this immutable version identifier.");
                    File.Delete(temporaryPath);
                }
                else
                {
                    File.Move(temporaryPath, destination);
                }

                _loaded[version] = bundle;
                return new SparkyBundleInfo(
                    version,
                    Path.GetFileName(destination),
                    new FileInfo(destination).Length,
                    new DateTimeOffset(File.GetLastWriteTimeUtc(destination), TimeSpan.Zero));
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static bool FilesEqual(string leftPath, string rightPath)
    {
        using var left = File.OpenRead(leftPath);
        using var right = File.OpenRead(rightPath);
        if (left.Length != right.Length)
            return false;
        byte[] leftHash = SHA256.HashData(left);
        byte[] rightHash = SHA256.HashData(right);
        try
        {
            return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftHash);
            CryptographicOperations.ZeroMemory(rightHash);
        }
    }

    private static void ValidateVersion(string version)
    {
        if (!IsSafeVersion(version))
            throw new InvalidDataException("The Sparky bundle version is not a safe catalog identifier.");
    }

    private static bool IsSafeVersion(string? version) =>
        !string.IsNullOrWhiteSpace(version) && version.Length <= 64 &&
        char.IsAsciiLetterOrDigit(version[0]) &&
        version.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    private static void AssertNotReparsePoint(string path)
    {
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("The Sparky bundle directory cannot be a reparse point.");
    }
}
