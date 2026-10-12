using System.Text.Json;

namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyKnowledgeSourceCatalog(IReadOnlyList<SparkyKnowledgeSource> Sources)
{
    public const int CurrentVersion = 1;

    public static SparkyKnowledgeSourceCatalog Load(string repositoryRoot)
    {
        string fullRoot = Path.GetFullPath(repositoryRoot);
        string catalogPath = ResolveRepositoryFile(fullRoot, "tools/sparky-knowledge-sources.json");
        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("version", out var versionElement) ||
            !versionElement.TryGetInt32(out int version) || version != CurrentVersion ||
            !root.TryGetProperty("sources", out var sourcesElement) || sourcesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The Sparky knowledge source catalog has an unsupported or invalid schema.");
        }

        var sources = new List<SparkyKnowledgeSource>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in sourcesElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String ||
                !element.TryGetProperty("category", out var categoryElement) || categoryElement.ValueKind != JsonValueKind.String ||
                !element.TryGetProperty("contentRoot", out var contentRootElement) || contentRootElement.ValueKind != JsonValueKind.String ||
                !element.TryGetProperty("contentPath", out var contentPathElement) || contentPathElement.ValueKind != JsonValueKind.String ||
                !element.TryGetProperty("sourcePaths", out var sourcePathsElement) || sourcePathsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("The Sparky knowledge source catalog contains an invalid entry.");
            }

            string id = idElement.GetString()!.Trim();
            string category = categoryElement.GetString()!.Trim();
            string contentRoot = contentRootElement.GetString()!.Trim();
            string contentPath = NormalizeRelativePath(contentPathElement.GetString()!);
            string[] sourcePaths = sourcePathsElement.EnumerateArray()
                .Select(value => value.ValueKind == JsonValueKind.String
                    ? value.GetString()!
                    : throw new InvalidDataException($"The Sparky knowledge source '{idElement.GetString()}' contains a non-string source path."))
                .Select(NormalizeRelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            bool mechanicsSource = category == "mechanics" && contentRoot == "repository" &&
                                   contentPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && sourcePaths.Length > 0 &&
                                   sourcePaths.All(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
            bool loreSource = category == "lore" && contentRoot == "lore" &&
                              contentPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                              !contentPath.Contains('/') && sourcePaths.Length == 0;

            if (id.Length is < 1 or > 80 || !id.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') ||
                !ids.Add(id) || (!mechanicsSource && !loreSource))
            {
                throw new InvalidDataException($"The Sparky knowledge source '{id}' is not an approved curated entry.");
            }

            if (contentRoot == "repository")
            {
                ResolveRepositoryFile(fullRoot, contentPath);
            }

            foreach (var sourcePath in sourcePaths)
            {
                ResolveRepositoryFile(fullRoot, sourcePath);
            }

            sources.Add(new SparkyKnowledgeSource(id, category, contentRoot, contentPath, sourcePaths));
        }

        if (sources.Count == 0)
        {
            throw new InvalidDataException("The Sparky knowledge source catalog has no approved sources.");
        }

        return new SparkyKnowledgeSourceCatalog(sources);
    }

    internal static string ResolveRepositoryFile(string repositoryRoot, string relativePath)
    {
        string normalized = NormalizeRelativePath(relativePath);
        string fullRoot = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            throw new InvalidDataException($"An allowlisted Sparky source is missing or resolves outside the repository: {normalized}");
        }

        string? current = fullPath;
        while (current is not null && !string.Equals(current, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Sparky knowledge source paths must not traverse reparse points.");
            }

            current = Path.GetDirectoryName(current);
        }

        return fullPath;
    }

    internal static string ResolveLoreFile(string loreRoot, string fileName)
    {
        string normalized = NormalizeRelativePath(fileName);
        if (normalized.Contains('/') || !normalized.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Allowlisted lore sources must be direct-child .txt files.");
        }

        string fullRoot = Path.GetFullPath(loreRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(fullRoot) || (File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new DirectoryNotFoundException("The configured Sparky lore directory is missing or is a reparse point.");
        }

        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalized));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            throw new InvalidDataException($"An allowlisted Sparky lore source is missing or resolves outside its configured directory: {normalized}");
        }

        if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Sparky lore source files must not be reparse points.");
        }

        return fullPath;
    }

    private static string NormalizeRelativePath(string path)
    {
        string normalized = path.Replace('\\', '/').Trim();
        if (normalized.Length == 0 || Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(part => part.Length == 0 || part is "." or ".."))
        {
            throw new InvalidDataException("Sparky knowledge source paths must be normalized relative paths without traversal.");
        }

        return normalized;
    }
}

internal sealed record SparkyKnowledgeSource(
    string Id,
    string Category,
    string ContentRoot,
    string ContentPath,
    IReadOnlyList<string> SourcePaths);
