using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks;

/// <summary>
/// 通过扩展宿主写日志，落盘到 Command Palette 的日志文件。
/// </summary>
internal static class HostLog
{
    public static void Info(string message) => Write(message);

    public static void Error(string message) => Write(message);

    private static void Write(string message)
    {
        try
        {
            ExtensionHost.LogMessage(new LogMessage { Message = $"CmdPalLinks: {message}" });
        }
        catch
        {
            // 日志失败不能影响扩展。
        }
    }
}
