using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace CmdPalLinks.Services;

/// <summary>
/// 彩色 emoji 渲染客户端：用自身 exe 拉起 <c>--render-emoji</c> 子进程，读回 PNG。
/// 进程隔离是关键——裸 COM 渲染的访问冲突无法被 try/catch 捕获，
/// 放到子进程后最坏情况只是子进程退出，CmdPal 扩展进程不受影响。
/// 超时、退出码非 0、PNG 无效一律返回 null，调用方降级为单色轮廓。
/// </summary>
internal static class EmojiRenderClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);

    private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(2);

    private const int MaxParallel = 2;

    private static int _running;

    public static byte[]? Render(string emoji, string? color, bool hasBlock, string outPath)
    {
        if (Interlocked.Increment(ref _running) > MaxParallel)
        {
            Interlocked.Decrement(ref _running);
            return null;
        }

        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                HostLog.Error("color emoji skipped: process path unavailable");
                return null;
            }

            TryDelete(outPath);
            TryDelete(outPath + ".err");

            var startInfo = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty,
            };
            startInfo.ArgumentList.Add("--render-emoji");
            startInfo.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(emoji)));
            startInfo.ArgumentList.Add(IsTransparent(color) ? "-" : (color ?? string.Empty).Trim());
            startInfo.ArgumentList.Add(hasBlock ? "1" : "0");
            startInfo.ArgumentList.Add(outPath);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
            {
                TryKill(process);
                HostLog.Info($"color emoji timeout: emoji='{emoji}'");
                return null;
            }

            if (process.ExitCode != 0)
            {
                HostLog.Info($"color emoji unavailable: emoji='{emoji}' exit={process.ExitCode} {ReadError(outPath)}");
                return null;
            }

            if (!File.Exists(outPath))
            {
                HostLog.Info($"color emoji missing output: emoji='{emoji}'");
                return null;
            }

            var png = File.ReadAllBytes(outPath);
            if (!LooksLikePng(png))
            {
                HostLog.Info($"color emoji invalid png: emoji='{emoji}' bytes={png.Length}");
                return null;
            }

            return png;
        }
        catch (Exception ex)
        {
            HostLog.Error($"color emoji failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
        finally
        {
            TryDelete(outPath);
            TryDelete(outPath + ".err");
            Interlocked.Decrement(ref _running);
        }
    }

    private static bool IsTransparent(string? color)
    {
        var text = (color ?? string.Empty).Trim();
        return text.Length == 0 || string.Equals(text, "none", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePng(byte[] bytes) =>
        bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

    private static string ReadError(string outPath)
    {
        try
        {
            var path = outPath + ".err";
            return File.Exists(path) ? "err=" + File.ReadAllText(path) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit((int)KillTimeout.TotalMilliseconds);
        }
        catch
        {
            // 子进程可能已经退出。
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 临时文件删除失败不影响渲染。
        }
    }
}
