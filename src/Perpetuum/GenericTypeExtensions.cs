using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Perpetuum
{
    public static class GenericTypeExtensions
    {
        /// <summary>
        /// Maps a struct array to a byte array
        /// </summary>
        public static byte[] ToByteArray<T>(this T[] array) where T : struct
        {
            Debug.Assert(array != null);

            byte[] result = new byte[array.Length * Marshal.SizeOf(typeof(T))];

            if (typeof(T).IsPrimitive)
            {
                Buffer.BlockCopy(array, 0, result, 0, result.Length);
            }
            else
            {
                GCHandle sHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
                Marshal.Copy(sHandle.AddrOfPinnedObject(), result, 0, result.Length);
                sHandle.Free();
            }

            return result;
        }

        public static byte[] ToByteArray<T>(this T source) where T : struct
        {
            int size = Marshal.SizeOf(source);
            nint ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(source, ptr, true);
                byte[] array = new byte[size];
                Marshal.Copy(ptr, array, 0, size);
                return array;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public static T? Clone<T>(this T source)
        {
            if (Equals(source, default(T)))
            {
                return default;
            }

            // Debug.Assert(typeof(T).IsSerializable, "EZ NEM SERIALIZALHATO: " + typeof(T)); Obsolete, have to find anothr way

            using MemoryStream ms = new();
            DataContractSerializer dcs = new(typeof(T));
            dcs.WriteObject(ms, source);
            ms.Seek(0, SeekOrigin.Begin);

            return (T)dcs.ReadObject(ms);
        }
    }
}
