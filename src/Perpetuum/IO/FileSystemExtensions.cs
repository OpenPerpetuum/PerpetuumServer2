using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Perpetuum.IO
{
    public static class FileSystemExtensions
    {
        public static T[] ReadLayer<T>(this IFileSystem fileSystem, string filename) where T : struct
        {
            var path = fileSystem.CreatePath(CreateLayerPath(filename));

            // Open the stream and read data directly into a T[] to avoid an extra intermediate byte[]
            using var stream = File.OpenRead(path);

            var sizeOfT = Marshal.SizeOf<T>();
            if (sizeOfT <= 0)
                return Array.Empty<T>();

            long length = stream.Length;
            if (length == 0)
                return Array.Empty<T>();

            int count = (int)(length / sizeOfT);

            // If the file size is not a multiple of sizeof(T), fall back to the previous byte[] reading logic
            if (length % sizeOfT != 0)
            {
                var bytes = fileSystem.ReadAllBytes(CreateLayerPath(filename));
                return bytes.ToArray<T>();
            }

            var result = new T[count];

            // For blittable types we can read directly into the byte representation of the T[]
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                var span = MemoryMarshal.AsBytes(result.AsSpan());

                int read = 0;
                while (read < span.Length)
                {
                    int n = stream.Read(span.Slice(read));
                    if (n == 0)
                        break;
                    read += n;
                }

                if (read != span.Length)
                {
                    // Read did not complete as expected; fall back to the safe byte[] path
                    var bytes = fileSystem.ReadAllBytes(CreateLayerPath(filename));
                    return bytes.ToArray<T>();
                }

                return result;
            }

            // For non-blittable structs (with reference fields) read into a byte buffer and convert
            var buffer = fileSystem.ReadAllBytes(CreateLayerPath(filename));
            return buffer.ToArray<T>();
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
        /// <param name="size">Size fo saved type</param>
        /// <returns>16 bytes of hash</returns>
        public static byte[] WriteLayerAndMD5(this IFileSystem fileSystem, string filename, ReadOnlySpan<byte> bytes, int size)
        {
            return fileSystem.WriteAllBytesAndMD5(CreateLayerPath(filename), bytes, size);
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