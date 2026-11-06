using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CmdPalLinks.Services;

/// <summary>
/// 彩色 emoji 渲染子进程入口：<c>CmdPalLinks.exe --render-emoji &lt;base64&gt; &lt;color|-&gt; &lt;0|1&gt; &lt;out.png&gt;</c>。
/// 宿主只通过 <see cref="EmojiRenderClient"/> 拉起它并读取 PNG，进程内绝不出窗口：
/// 首行关闭 WER 错误框，所有异常只写 &lt;out.png&gt;.err，返回值用退出码表达。
/// </summary>
internal static class EmojiRenderHost
{
    private const string Marker = "--render-emoji";

    private const uint ErrorMode = 0x0001 | 0x0002 | 0x8000;

    public static bool Handles(string[] args) => args.Length > 0 && args[0] == Marker;

    public static int Run(string[] args)
    {
        // 任何原生异常都不弹系统错误框（SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX）。
        try
        {
            SetErrorMode(ErrorMode);
        }
        catch
        {
            // 老系统上失败也不影响渲染结果。
        }

        if (args.Length < 5)
        {
            return 64;
        }

        var outPath = args[4];
        try
        {
            var emoji = Encoding.UTF8.GetString(Convert.FromBase64String(args[1]));
            var color = string.Equals(args[2], "-", StringComparison.Ordinal) ? string.Empty : args[2];
            var block = string.Equals(args[3], "1", StringComparison.Ordinal);

            var png = ColorEmojiRenderer.Render(emoji, color, block);
            if (png is null)
            {
                WriteError(outPath, "no-color-glyph");
                return 2;
            }

            File.WriteAllBytes(outPath, png);
            return 0;
        }
        catch (Exception ex)
        {
            WriteError(outPath, $"{ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static void WriteError(string outPath, string message)
    {
        try
        {
            File.WriteAllText(outPath + ".err", message);
        }
        catch
        {
            // 诊断信息写不出去也不能再抛。
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);
}
