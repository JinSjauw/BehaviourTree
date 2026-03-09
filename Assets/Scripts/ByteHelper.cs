using System;
using System.Runtime.InteropServices;
using UnityEngine;

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
}
