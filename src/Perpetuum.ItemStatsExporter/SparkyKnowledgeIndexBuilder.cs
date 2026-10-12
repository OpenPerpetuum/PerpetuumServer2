using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyKnowledgeIndexBuildResult(
    IReadOnlyList<SparkyKnowledgePassage> Passages,
    SparkySearchIndex SearchIndex,
    IReadOnlyDictionary<string, string> SourceHashes);

internal static class SparkyKnowledgeIndexBuilder
{
    private const int MaxSourceFiles = 64;
    private const int MaxPassages = 4000;
    private const int MaxBundleTextCharacters = 2_000_000;
    private const int TargetPassageCharacters = 1200;
    private static readonly Regex WordPattern = new("[\\p{L}\\p{N}]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static SparkyKnowledgeIndexBuildResult Build(
        string repositoryRoot,
        string loreRoot,
        SparkyKnowledgeSourceCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(repositoryRoot);
        if (catalog.Sources.Count > MaxSourceFiles)
        {
            throw new InvalidDataException($"The Sparky source catalog exceeds the {MaxSourceFiles}-source limit.");
        }

        var passages = new List<SparkyKnowledgePassage>();
        var sourceHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        int totalCharacters = 0;

        foreach (var source in catalog.Sources.OrderBy(source => source.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string contentPath = source.ContentRoot == "repository"
                ? SparkyKnowledgeSourceCatalog.ResolveRepositoryFile(root, source.ContentPath)
                : SparkyKnowledgeSourceCatalog.ResolveLoreFile(loreRoot, source.ContentPath);
            string content = NormalizeText(File.ReadAllText(contentPath));
            EnsureApprovedKnowledgeText(content, source.Id);

            using var revisionHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            revisionHash.AppendData(Encoding.UTF8.GetBytes(content));
            foreach (var supportingPath in source.SourcePaths.OrderBy(path => path, StringComparer.Ordinal))
            {
                string fullPath = SparkyKnowledgeSourceCatalog.ResolveRepositoryFile(root, supportingPath);
                revisionHash.AppendData(Encoding.UTF8.GetBytes(Hash(File.ReadAllBytes(fullPath))));
            }

            string revision = Convert.ToHexString(revisionHash.GetHashAndReset());
            sourceHashes.Add(source.Id, revision);
            var chunks = SplitPassages(content);
            foreach (string chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(chunk))
                {
                    continue;
                }

                if (chunk.Length > SparkyKnowledgeIndexLimits.MaxPassageCharacters)
                {
                    throw new InvalidDataException($"A curated Sparky passage from '{source.Id}' exceeds the passage length limit.");
                }

                string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chunk)));
                passages.Add(new SparkyKnowledgePassage(
                    source.Id,
                    source.Category,
                    revision,
                    hash,
                    chunk));
                totalCharacters += chunk.Length;
            }
        }

        if (passages.Count > MaxPassages || totalCharacters > MaxBundleTextCharacters)
        {
            throw new InvalidDataException("The Sparky knowledge catalog exceeds the configured bundle size limits.");
        }

        var searchIndex = BuildSearchIndex(passages);
        return new SparkyKnowledgeIndexBuildResult(passages, searchIndex, sourceHashes);
    }

    public static SparkySearchIndex BuildSearchIndex(IReadOnlyList<SparkyKnowledgePassage> passages)
    {
        var tokenIndexes = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);
        for (int passageIndex = 0; passageIndex < passages.Count; passageIndex++)
        {
            foreach (string token in Tokenize(passages[passageIndex].Text))
            {
                if (!tokenIndexes.TryGetValue(token, out var indexes))
                {
                    indexes = [];
                    tokenIndexes.Add(token, indexes);
                }

                indexes.Add(passageIndex);
            }
        }

        return new SparkySearchIndex(tokenIndexes.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<int>)entry.Value.ToArray(),
            StringComparer.Ordinal), []);
    }

    private static IReadOnlyList<string> SplitPassages(string text)
    {
        var paragraphs = Regex.Split(text, @"\r?\n\s*\r?\n")
            .Select(paragraph => paragraph.Trim())
            .Where(paragraph => paragraph.Length > 0);
        var passages = new List<string>();
        var current = new StringBuilder();
        foreach (string paragraph in paragraphs)
        {
            if (paragraph.Length > SparkyKnowledgeIndexLimits.MaxPassageCharacters)
            {
                FlushCurrent();
                foreach (string sentence in Regex.Split(paragraph, @"(?<=[.!?])\s+"))
                {
                    AppendBounded(sentence);
                }

                continue;
            }

            if (current.Length > 0 && current.Length + paragraph.Length + 2 > TargetPassageCharacters)
            {
                FlushCurrent();
            }

            if (current.Length > 0)
            {
                current.AppendLine().AppendLine();
            }

            current.Append(paragraph);
        }

        FlushCurrent();
        return passages;

        void AppendBounded(string sentence)
        {
            sentence = sentence.Trim();
            if (sentence.Length == 0)
            {
                return;
            }

            if (sentence.Length > SparkyKnowledgeIndexLimits.MaxPassageCharacters)
            {
                for (int offset = 0; offset < sentence.Length; offset += SparkyKnowledgeIndexLimits.MaxPassageCharacters)
                {
                    passages.Add(sentence.Substring(offset, Math.Min(SparkyKnowledgeIndexLimits.MaxPassageCharacters, sentence.Length - offset)));
                }
            }
            else
            {
                passages.Add(sentence);
            }
        }

        void FlushCurrent()
        {
            if (current.Length > 0)
            {
                passages.Add(current.ToString());
                current.Clear();
            }
        }
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        return WordPattern.Matches(text)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.Ordinal);
    }

    private static string NormalizeText(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    private static void EnsureApprovedKnowledgeText(string text, string sourceId)
    {
        string[] forbiddenMarkers = [
            "password=", "password:", "bearer ", "private key", "connection string", "authorization token",
            "account id", "character id", "discord id", "select * from", "insert into", "update ", "delete from"
        ];
        if (text.Length > 250_000 || forbiddenMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException($"Curated source '{sourceId}' contains a forbidden private-data or operational marker.");
        }
    }

    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
}
