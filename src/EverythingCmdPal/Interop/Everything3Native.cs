using System;
using System.Runtime.InteropServices;

namespace EverythingCmdPal.Interop;

/// <summary>
/// P/Invoke signatures taken from the official voidtools Everything SDK3 header
/// (Everything3.h, SDK 3.0.0.9). BOOL == 32-bit int, SIZE_T == nuint, UINT64 == ulong.
/// </summary>
internal static class Everything3Native
{
    private const string Dll = "Everything3.dll";

    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern IntPtr Everything3_ConnectW(string? instanceName);

    [DllImport(Dll, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_DestroyClient(IntPtr client);

    [DllImport(Dll, ExactSpelling = true)]
    internal static extern IntPtr Everything3_CreateSearchState();

    [DllImport(Dll, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_DestroySearchState(IntPtr searchState);

    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_SetSearchTextW(IntPtr searchState, string search);

    [DllImport(Dll, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_SetSearchViewportOffset(IntPtr searchState, nuint offset);

    [DllImport(Dll, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_SetSearchViewportCount(IntPtr searchState, nuint count);

    [DllImport(Dll, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_AddSearchPropertyRequest(IntPtr searchState, uint propertyId);

    [DllImport(Dll, ExactSpelling = true)]
    internal static extern IntPtr Everything3_Search(IntPtr client, IntPtr searchState);

    [DllImport(Dll, ExactSpelling = true)]
    internal static extern IntPtr Everything3_GetResults(IntPtr client, IntPtr searchState);

    [DllImport(Dll, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_DestroyResultList(IntPtr resultList);

    [DllImport(Dll, ExactSpelling = true)]
    internal static extern nuint Everything3_GetResultListCount(IntPtr resultList);

    [DllImport(Dll, ExactSpelling = true)]
    internal static extern nuint Everything3_GetResultListViewportCount(IntPtr resultList);

    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern unsafe nuint Everything3_GetResultFullPathNameW(
        IntPtr resultList,
        nuint resultIndex,
        char* outBuffer,
        nuint bufferChars);

    [DllImport(Dll, ExactSpelling = true)]
    internal static extern ulong Everything3_GetResultSize(IntPtr resultList, nuint resultIndex);

    [DllImport(Dll, ExactSpelling = true)]
    internal static extern ulong Everything3_GetResultDateModified(IntPtr resultList, nuint resultIndex);

    [DllImport(Dll, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_IsFolderResult(IntPtr resultList, nuint resultIndex);
}
