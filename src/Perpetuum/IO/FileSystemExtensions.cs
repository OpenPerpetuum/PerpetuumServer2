using System.IO;

namespace Perpetuum.IO
{
    public static class FileSystemExtensions
    {
        public static T[] ReadLayer<T>(this IFileSystem fileSystem, string filename) where T : struct
        {
            return fileSystem.ReadAllBytes(CreateLayerPath(filename)).ToArray<T>();
        }

        public static byte[] ReadLayerAsByteArray(this IFileSystem fileSystem, string filename)
        {
            return fileSystem.ReadAllBytes(CreateLayerPath(filename));
        }

        /// <summary>
        /// Returns a hash for the layer file.
        /// </summary>
        /// <param name="fileSystem"></param>
        /// <param name="filename">Layer filename</param>
        /// <returns>16 bytes of hash</returns>
        public static byte[] MD5(this IFileSystem fileSystem, string filename)
        {
            return fileSystem.MD5SUM(CreateLayerPath(filename));
        }

        /// <summary>
        /// Writes all bytes to a file on disk, calculating the hash in parallel.
        /// </summary>
        /// <param name="fileSystem"></param>
        /// <param name="filename">Layer filename</param>
        /// <param name="bytes">Layer data as bytes</param>
        /// <returns>16 bytes of hash</returns>
        public static byte[] WriteLayerAndMD5(this IFileSystem fileSystem, string filename, ReadOnlySpan<byte> bytes)
        {
            return fileSystem.WriteAllBytesAndMD5(CreateLayerPath(filename), bytes);
        }

        private static string CreateLayerPath(string filename)
        {
            return Path.Combine("layers", filename);
        }

        public static void WriteLayer<T>(this IFileSystem fileSystem, string filename,T[] data) where T:struct
        {
            fileSystem.WriteAllBytes(CreateLayerPath(filename), data.ToByteArray());
        }

        public static void MoveLayerFile(this IFileSystem fileSystem, string sourceFilename, string targetFilename)
        {
            fileSystem.MoveFile(CreateLayerPath(sourceFilename),CreateLayerPath(targetFilename));
        }

        public static string CreatePath(this IFileSystem fileSystem, params string[] paths)
        {
            return fileSystem.CreatePath(Path.Combine(paths));
        }
    }
}