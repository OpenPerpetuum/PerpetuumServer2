using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Perpetuum.IO
{
    public class FileSystem : IFileSystem
    {
        private readonly string _root;

        public FileSystem(string root)
        {
            _root = root;
        }

        public bool Exists(string path)
        {
            return File.Exists(CreatePath(path));
        }

        public byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(CreatePath(path));
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(CreatePath(path));
        }

        public string[] ReadAllLines(string path)
        {
            return File.ReadAllLines(CreatePath(path));
        }

        public void WriteAllBytes(string path, byte[] bytes)
        {
            File.WriteAllBytes(CreatePath(path),bytes);
        }

        public byte[] WriteAllBytesAndMD5(string path, ReadOnlySpan<byte> bytes)
        {
            // The algorithm calculates the hash and passes the data through without modification
            using (var md5 = MD5.Create())
            {
                // Open a stream to a file
                using (var fileStream = File.Create(CreatePath(path)))
                // Wrap it in a CryptoStream, passing in the hashing algorithm
                using (var cryptoStream = new CryptoStream(fileStream, md5, CryptoStreamMode.Write))
                {
                    // We write the data (it will go both to the file and to MD5)
                    cryptoStream.Write(bytes);
                    // Important: Call FlushFinalBlock to complete the hash calculation
                    cryptoStream.FlushFinalBlock();
                }

                // Obtain the final hash from the algorithm object
                return md5.Hash;
            }
        }

        public void WriteAllLines(string path, IEnumerable<string> lines)
        {
            File.WriteAllLines(CreatePath(path), lines);
        }

        public void AppendAllText(string path, string text)
        {
            File.AppendAllText(CreatePath(path),text);
        }

        public void AppendAllLines(string path, IEnumerable<string> lines)
        {
            File.AppendAllLines(CreatePath(path),lines);
        }

        public void MoveFile(string sourcePath, string targetPath)
        {
            var src = CreatePath(sourcePath);
            var dest = CreatePath(targetPath);

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(src,dest);
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(CreatePath(path));
        }

        public string CreatePath(string path)
        {
            return Path.Combine(_root, path);
        }

        public IEnumerable<string> GetFiles(string path, string mask)
        {
            return Directory.GetFiles(Path.Combine(_root, path), mask);
        }

        public override string ToString()
        {
            return $"Root: {_root}";
        }

        public byte[] MD5SUM(string path)
        {
            // We use the hash algorithm MD5
            using (var md5 = MD5.Create())
            {
                // Using a file stream for reading to calculate the hash
                using (var stream = File.OpenRead(CreatePath(path)))
                {
                    // The method itself will read the entire stream to the end
                    return md5.ComputeHash(stream);
                }
            }
        }
    }
}
