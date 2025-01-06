using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Perpetuum
{
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Maps a byte array to struct array
        /// </summary>
        public static T[] ToArray<T>(this byte[] source) where T : struct
        {
            int sizeOf = Marshal.SizeOf(typeof(T));
            T[] to = new T[source.Length / sizeOf];

            if (typeof(T).IsPrimitive)
            {
                Buffer.BlockCopy(source, 0, to, 0, source.Length);
            }
            else
            {
                GCHandle handle = GCHandle.Alloc(to, GCHandleType.Pinned);
                try
                {
                    Marshal.Copy(source, 0, handle.AddrOfPinnedObject(), source.Length);
                }
                finally
                {
                    handle.Free();
                }
            }
            return to;
        }

        public static T ToStruct<T>(this byte[] array) where T : struct
        {
            int size = Marshal.SizeOf(default(T));
            nint ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(array, 0, ptr, size);
                return (T)Marshal.PtrToStructure(ptr, typeof(T));
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// Converts a byte array to an object
        /// </summary>
        public static T? Deserialize<T>(this byte[] data)
        {
            if (data == null)
            {
                return default;
            }

            using MemoryStream ms = new(data);
            return (T)new DataContractSerializer(typeof(T)).ReadObject(ms);
        }
    }
}