using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingCmdPal.Services;

/// <summary>
/// Writes through the extension host so messages land in the Command Palette log
/// instead of a private temp file (and without per-item synchronous disk writes).
/// </summary>
internal static class HostLog
{
    public static void Info(string message) => Write(message);

    public static void Error(string message) => Write(message);

    private static void Write(string message)
    {
        try
        {
            ExtensionHost.LogMessage(new LogMessage { Message = $"EverythingCmdPal: {message}" });
        }
        catch
        {
            // Logging must never break the extension.
        }
    }
}
