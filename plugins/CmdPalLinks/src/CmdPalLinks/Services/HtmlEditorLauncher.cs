using System;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Services;

/// <summary>
/// 把随包的 Assets\links-editor.html 解到 LocalState 的可写副本，再用默认浏览器打开。
/// 宿主扩展页是原生渲染，无法内嵌 HTML/WebView；因此走“本地文件 + 系统默认程序”这条路线。
/// </summary>
internal static class HtmlEditorLauncher
{
    private const string AssetRelativePath = @"Assets\links-editor.html";
    private const string LocalFileName = "links-editor.html";

    /// <summary>返回可打开的本地副本路径；资产缺失或解包失败时返回 null。</summary>
    public static string? EnsureLocalCopy()
    {
        try
        {
            var source = Path.Combine(AppContext.BaseDirectory, AssetRelativePath);
            if (!File.Exists(source))
            {
                HostLog.Error($"editor asset missing: {source}");
                return null;
            }

            var target = Path.Combine(Utilities.BaseSettingsPath("CmdPalLinks"), LocalFileName);
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 包内资产更新时用长度差异做粗判，覆盖本地副本；一致则不写盘。
            var sourceInfo = new FileInfo(source);
            var targetInfo = new FileInfo(target);
            if (!targetInfo.Exists || targetInfo.Length != sourceInfo.Length)
            {
                File.Copy(source, target, overwrite: true);
                HostLog.Info($"editor extracted: {target} ({sourceInfo.Length} bytes)");
            }

            return target;
        }
        catch (Exception ex)
        {
            HostLog.Error($"editor extract failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
