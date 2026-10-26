using System;
using System.Collections.Generic;
using EverythingCmdPal.Interop;

namespace EverythingCmdPal.Services;

internal sealed record SearchHit(string FullPath, bool IsFolder);

/// <summary>
/// Minimal Everything 1.5 client. The SDK keeps global state per connection, so all
/// calls are serialized behind a gate and run on a single consumer thread.
/// </summary>
internal sealed class EverythingClient : IEverythingSearch
{
    private readonly object _gate = new();
    private readonly string _instanceName;

    private IntPtr _client;

    public EverythingClient(string instanceName)
    {
        _instanceName = (instanceName ?? string.Empty).Trim();
    }

    /// <summary>最近一次连接尝试的结果，供页面决定空态提示。</summary>
    public EverythingConnectionState State { get; private set; } = EverythingConnectionState.Unknown;

    public bool TryConnect()
    {
        if (_client != IntPtr.Zero) return true;

        if (_instanceName.Length > 0)
        {
            _client = Everything3Native.Everything3_ConnectW(_instanceName);
            return MarkConnected(_client != IntPtr.Zero);
        }

        _client = Everything3Native.Everything3_ConnectW(null);
        if (_client == IntPtr.Zero)
        {
            _client = Everything3Native.Everything3_ConnectW("1.5a");
        }

        return MarkConnected(_client != IntPtr.Zero);
    }

    /// <summary>记录连接结果；失败时顺带判断是「没开」还是「进程在但没管道」。</summary>
    private bool MarkConnected(bool connected)
    {
        State = connected
            ? EverythingConnectionState.Connected
            : ConnectionDiagnostics.Classify(false, ConnectionDiagnostics.IsEverythingProcessRunning());
        return connected;
    }

    public IReadOnlyList<SearchHit> Search(string query, int offset, int count, SortOrder.SortSpec sort, out int total)
    {
        total = 0;

        lock (_gate)
        {
            if (!TryConnect()) return Array.Empty<SearchHit>();
            return SearchCore(query, offset, count, sort, out total) ?? (IReadOnlyList<SearchHit>)Array.Empty<SearchHit>();
        }
    }

    private unsafe List<SearchHit>? SearchCore(string query, int offset, int count, SortOrder.SortSpec sort, out int total)
    {
        total = 0;

        var state = Everything3Native.Everything3_CreateSearchState();
        if (state == IntPtr.Zero) return null;

        IntPtr list = IntPtr.Zero;
        try
        {
            Everything3Native.Everything3_SetSearchTextW(state, query);
            Everything3Native.Everything3_SetSearchViewportOffset(state, (nuint)offset);
            Everything3Native.Everything3_SetSearchViewportCount(state, (nuint)count);

            // 排序必须落在 SearchState 上（服务端排序），客户端排序会让分页顺序错位。
            Everything3Native.Everything3_SetSearchSort(state, sort.PropertyId, sort.Ascending);

            // IMPORTANT: do not call Everything3_AddSearchPropertyRequest here.
            // As soon as a property request is added, Everything3_GetResultFullPathNameW
            // returns 0 for every item (verified against SDK 3.0.0.9 with a standalone
            // harness), which makes the result list look empty. Size/date are read from
            // FileInfo when the details pane is filled instead.

            list = Everything3Native.Everything3_Search(_client, state);
            if (list == IntPtr.Zero)
            {
                // Connection may have died (Everything restarted). Reconnect and retry once.
                Everything3Native.Everything3_DestroyClient(_client);
                _client = IntPtr.Zero;
                if (!TryConnect()) return null;

                list = Everything3Native.Everything3_Search(_client, state);
                if (list == IntPtr.Zero) return null;
            }

            total = (int)Everything3Native.Everything3_GetResultListCount(list);
            var viewport = (int)Everything3Native.Everything3_GetResultListViewportCount(list);
            var hits = new List<SearchHit>(viewport);

            char* buffer = stackalloc char[8192];
            for (var i = 0; i < viewport; i++)
            {
                var length = Everything3Native.Everything3_GetResultFullPathNameW(list, (nuint)i, buffer, 8192);
                if (length == 0) continue;

                var path = new string(buffer, 0, (int)Math.Min(length, 8191));
                if (string.IsNullOrWhiteSpace(path)) continue;

                var isFolder = Everything3Native.Everything3_IsFolderResult(list, (nuint)i);
                hits.Add(new SearchHit(path, isFolder));
            }

            return hits;
        }
        finally
        {
            if (list != IntPtr.Zero)
            {
                Everything3Native.Everything3_DestroyResultList(list);
            }

            Everything3Native.Everything3_DestroySearchState(state);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_client != IntPtr.Zero)
            {
                Everything3Native.Everything3_DestroyClient(_client);
                _client = IntPtr.Zero;
            }
        }
    }
}
