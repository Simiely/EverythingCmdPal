using System;
using System.Diagnostics;
using System.IO;

namespace EverythingCmdPal.Actions;

/// <summary>Locates the Everything executable across common install locations.</summary>
internal static class EverythingLocator
{
    public static string FindExe()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything 1.5a", "Everything.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", "Everything.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Everything 1.5a", "Everything.exe"),
        ];

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        try
        {
            foreach (var process in Process.GetProcessesByName("Everything"))
            {
                var path = process.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path)) return path;
            }
        }
        catch
        {
            // Best effort only.
        }

        return string.Empty;
    }
}
