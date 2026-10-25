using System.Collections.Generic;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingCmdPal.Services;

/// <summary>
/// Single place that builds the details metadata for a result. Both the item and the
/// preview used to assemble this independently; keeping it here avoids the two drifting.
/// </summary>
internal static class ResultMetadata
{
    public static IDetailsElement[] Build(string path, bool isFolder)
    {
        var items = new List<IDetailsElement>
        {
            new DetailsElement { Key = "路径", Data = new DetailsLink { Text = path } },
        };

        if (isFolder)
        {
            items.Add(new DetailsElement { Key = "类型", Data = new DetailsLink { Text = "文件夹" } });
            return [.. items];
        }

        try
        {
            var info = new FileInfo(path);
            if (info.Exists)
            {
                items.Add(new DetailsElement
                {
                    Key = "大小",
                    Data = new DetailsLink { Text = FormatSize(info.Length) },
                });
                items.Add(new DetailsElement
                {
                    Key = "修改时间",
                    Data = new DetailsLink { Text = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm") },
                });
            }
        }
        catch
        {
            // Metadata is best-effort; a broken path should not break the row.
        }

        return [.. items];
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return string.Empty;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }
}
