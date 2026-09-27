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

        public byte[] WriteAllBytesAndMD5(string path, ReadOnlySpan<byte> bytes, int size)
        {
            // Allocate the resulting array to return. MD5 hash is always 16 bytes.
            // This is the only managed allocation in the entire method.
            byte[] finalHash = new byte[16];

            // Initialize incremental hashing without creating heavy managed objects
            using (var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.MD5))
            // Open the file handle directly via the OS to bypass FileStream overhead
            using (var handle = File.OpenHandle(CreatePath(path), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // Allocate a local buffer on the thread's stack (isolated from other threads).
                // x4096 bytes is an optimal size aligned with most OS disk sector sizes.
                Span<byte> stackBuffer = stackalloc byte[4096 * size];

                int position = 0;
                int remaining = bytes.Length;

                while (remaining > 0)
                {
                    int chunkSize = Math.Min(remaining, stackBuffer.Length);

                    // ATOMIC SNAPSHOT: Copy data from the shared memory into the protected stack buffer.
                    // Even if other threads modify sharedBytes concurrently, stackBuffer 
                    // captures a stable point-in-time snapshot of this specific chunk.
                    bytes.Slice(position, chunkSize).CopyTo(stackBuffer);

                    // Work strictly with the isolated snapshot
                    var currentChunk = stackBuffer.Slice(0, chunkSize);

                    // Write the exact captured snapshot to the disk
                    RandomAccess.Write(handle, currentChunk, position);

                    // Feed the exact same captured snapshot into the hash engine
                    incrementalHash.AppendData(currentChunk);

                    position += chunkSize;
                    remaining -= chunkSize;
                }

                // Finalize the hash computation directly into the allocated return array
                incrementalHash.GetCurrentHash(finalHash);
            }

            return finalHash;
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
