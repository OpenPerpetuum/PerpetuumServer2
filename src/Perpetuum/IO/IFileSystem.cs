using System.Collections.Generic;

namespace Perpetuum.IO
{
    public interface IFileSystem
    {
        bool Exists(string path);
        byte[] ReadAllBytes(string path);
        string ReadAllText(string path);
        string[] ReadAllLines(string path);

        void WriteAllBytes(string path, byte[] bytes);

        /// <summary>
        /// Writes all bytes to a file on disk, calculating the hash in parallel.
        /// </summary>
        /// <param name="path">Path within the game's root directory</param>
        /// <param name="bytes">Bytes to write</param>
        /// <returns>MD5 hash</returns>
        public byte[] WriteAllBytesAndMD5(string path, ReadOnlySpan<byte> bytes);
        void WriteAllLines(string path,IEnumerable<string> lines);

        void AppendAllText(string path, string text);
        void AppendAllLines(string path, IEnumerable<string> lines);

        void MoveFile(string sourcePath, string targetPath);

        void CreateDirectory(string path);

        string CreatePath(string path);

        /// <summary>
        /// Calculates the hash using the MD5 algorithm
        /// </summary>
        /// <param name="path">Path within the game's root directory</param>
        /// <returns>16 bytes of hash</returns>
        byte[] MD5SUM(string path);

        IEnumerable<string> GetFiles(string path, string mask);
    }
}
