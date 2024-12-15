using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

unsafe static class MemoryUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* Alloc(int count) => (void*)Marshal.AllocCoTaskMem(count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* Alloc<T>() where T : unmanaged
        => (T*)Marshal.AllocCoTaskMem(sizeof(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* Alloc<T>(int count) where T : unmanaged
        => (T*)Marshal.AllocCoTaskMem(sizeof(T) * count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Free(void* pointer) => Free((nint)pointer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Free(nint pointer) => Marshal.FreeCoTaskMem(pointer);
}