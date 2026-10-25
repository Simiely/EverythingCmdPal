using System;
using System.Runtime.InteropServices;

internal static class Native
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
    internal static extern bool Everything3_DestroySearchState(IntPtr state);

    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_SetSearchTextW(IntPtr state, string text);

    [DllImport(Dll, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_SetSearchViewportOffset(IntPtr state, nuint offset);

    [DllImport(Dll, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_SetSearchViewportCount(IntPtr state, nuint count);

    [DllImport(Dll, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_AddSearchPropertyRequest(IntPtr state, uint propertyId);

    [DllImport(Dll, ExactSpelling = true)]
    internal static extern IntPtr Everything3_Search(IntPtr client, IntPtr state);

    [DllImport(Dll, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Everything3_DestroyResultList(IntPtr list);

    [DllImport(Dll, ExactSpelling = true)]
    internal static extern nuint Everything3_GetResultListCount(IntPtr list);

    [DllImport(Dll, ExactSpelling = true)]
    internal static extern nuint Everything3_GetResultListViewportCount(IntPtr list);

    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern unsafe nuint Everything3_GetResultFullPathNameW(IntPtr list, nuint index, char* buffer, nuint chars);
}

internal static class Program
{
    private static unsafe int Main(string[] args)
    {
        var query = args.Length > 0 ? args[0] : "*.sln";
        var withProps = args.Length > 1 && args[1] == "props";

        var client = Native.Everything3_ConnectW(null);
        if (client == IntPtr.Zero) client = Native.Everything3_ConnectW("1.5a");
        if (client == IntPtr.Zero) { Console.WriteLine("CONNECT_FAILED"); return 2; }

        var state = Native.Everything3_CreateSearchState();
        Native.Everything3_SetSearchTextW(state, query);
        Native.Everything3_SetSearchViewportOffset(state, 0);
        Native.Everything3_SetSearchViewportCount(state, 50);

        if (withProps)
        {
            Native.Everything3_AddSearchPropertyRequest(state, 2);
            Native.Everything3_AddSearchPropertyRequest(state, 5);
        }

        var list = Native.Everything3_Search(client, state);
        if (list == IntPtr.Zero) { Console.WriteLine("SEARCH_FAILED"); return 3; }

        var total = Native.Everything3_GetResultListCount(list);
        var viewport = Native.Everything3_GetResultListViewportCount(list);
        Console.WriteLine($"query={query} props={withProps} total={(int)total} viewport={(int)viewport}");

        char* buffer = stackalloc char[8192];
        for (nuint i = 0; i < viewport; i++)
        {
            var len = Native.Everything3_GetResultFullPathNameW(list, i, buffer, 8192);
            Console.WriteLine($"  [{i}] len={(int)len} {new string(buffer, 0, (int)Math.Min(len, 8191))}");
        }

        Native.Everything3_DestroyResultList(list);
        Native.Everything3_DestroySearchState(state);
        Native.Everything3_DestroyClient(client);
        return 0;
    }
}
