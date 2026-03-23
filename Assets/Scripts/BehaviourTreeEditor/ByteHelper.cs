using System;
using System.Runtime.InteropServices;

namespace BTreeEditor.Utility
{
    public static class ByteHelper
    {
        public static byte[] StructToBytes<T>(T data) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] buffer = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(data, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return buffer;
        }

        public unsafe static void ByteArrayToFixedBuffer(byte[] source, byte* destination, int maxSize)
        {
            int copyLength = Math.Min(source.Length, maxSize);
            Marshal.Copy(source, 0, (IntPtr)destination, copyLength);

            //Pad the remainder of the buffer if empty with 0
            for (int i = copyLength; i < maxSize; i++)
            {
                destination[i] = 0;
            }
        }
    }
}
