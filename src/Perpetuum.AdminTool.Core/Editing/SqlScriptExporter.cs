using System.Text;

namespace Perpetuum.AdminTool.Editing
{
    public interface ISqlScriptExporter
    {
        Task<string> ExportAsync(
            string outputDirectory,
            string filePrefix,
            IReadOnlyList<IPendingChange> changes,
            string? authorEmail = null,
            CancellationToken cancellationToken = default);
    }

    public sealed class SqlScriptExporter : ISqlScriptExporter
    {
        public async Task<string> ExportAsync(
            string outputDirectory,
            string filePrefix,
            IReadOnlyList<IPendingChange> changes,
            string? authorEmail = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
            }

            ArgumentNullException.ThrowIfNull(changes);
            if (changes.Count == 0)
            {
                throw new ArgumentException("At least one pending change is required.", nameof(changes));
            }

            string fullDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(fullDirectory);

            string script = SqlScriptBuilder.Build(changes, authorEmail);
            string baseFileName = SqlScriptBuilder.BuildFileName(filePrefix);
            string finalPath = FindAvailablePath(fullDirectory, baseFileName);
            string temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                await File.WriteAllTextAsync(
                    temporaryPath,
                    script,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    cancellationToken);
                File.Move(temporaryPath, finalPath, overwrite: false);
                return finalPath;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static string FindAvailablePath(string directory, string fileName)
        {
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                return path;
            }

            string stem = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            for (int suffix = 2; suffix < int.MaxValue; suffix++)
            {
                path = Path.Combine(directory, $"{stem}_{suffix}{extension}");
                if (!File.Exists(path))
                {
                    return path;
                }
            }

            throw new IOException("Unable to allocate a unique SQL script file name.");
        }
    }
}
