// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;
using System;
using System.Threading;

namespace CmdPalLinks;

public class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        // 彩色 emoji 子进程入口：必须在 COM 服务注册之前分流，且不带任何窗口。
        if (EmojiRenderHost.Handles(args))
        {
            Environment.Exit(EmojiRenderHost.Run(args));
            return;
        }

        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            global::Shmuelie.WinRTServer.ComServer server = new();

            ManualResetEvent extensionDisposedEvent = new(false);

            // 扩展实例只创建一次，宿主每次请求 IExtension 都返回同一个对象。
            CmdPalLinksExtension extensionInstance = new(extensionDisposedEvent);
            server.RegisterClass<CmdPalLinksExtension, IExtension>(() => extensionInstance);
            server.Start();

            extensionDisposedEvent.WaitOne();
            server.Stop();
            server.UnsafeDispose();
        }
        else
        {
            Console.WriteLine("Not being launched as a Extension... exiting.");
        }
    }
}
