// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;

namespace CmdPalLinks;

// 必须与 Package.appxmanifest 中的 com:Class Id 和 CreateInstance ClassId 一致。
[Guid("79B2B289-B5A9-4E88-AB21-B2509F22762B")]
public sealed partial class CmdPalLinksExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;

    private readonly CmdPalLinksCommandsProvider _provider = new();

    public CmdPalLinksExtension(ManualResetEvent extensionDisposedEvent)
    {
        this._extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };
    }

    public void Dispose()
    {
        // 扩展进程退出时释放编辑器端口，避免端口长期占用。
        EditorServer.Shutdown();
        this._extensionDisposedEvent.Set();
    }
}
