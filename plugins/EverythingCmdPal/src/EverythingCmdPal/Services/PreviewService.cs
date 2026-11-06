using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace EverythingCmdPal.Services;

/// <summary>
/// Fills the details pane asynchronously. Responsibilities: metadata (via
/// <see cref="ResultMetadata"/>), one preview visual, and a short text snippet.
/// Everything else (icons, actions, layout) belongs to the item and the page.
/// </summary>
internal static class PreviewService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".jfif", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico", ".svg", ".avif", ".heic", ".heif",
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".md", ".json", ".xml", ".yaml", ".yml", ".ini", ".cfg", ".conf", ".csv", ".tsv",
        ".cs", ".cpp", ".c", ".h", ".hpp", ".py", ".js", ".ts", ".tsx", ".jsx", ".html", ".htm", ".css",
        ".ps1", ".bat", ".cmd", ".sh", ".sql", ".java", ".go", ".rs", ".toml",
    };

    /// <summary>预览缓存上限：条数与总体积，超过就按最后写入时间淘汰最旧的。</summary>
    private const int MaxCachedPreviews = 300;
    private const long MaxCacheBytes = 256L * 1024 * 1024;

    public static async Task FillAsync(Details details, string path, bool isFolder, int previewWidth)
    {
        try
        {
            // Single source of truth for metadata (path / size / modified).
            details.Metadata = ResultMetadata.Build(path, isFolder);

            var extension = Path.GetExtension(path);

            if (isFolder || !ImageExtensions.Contains(extension))
            {
                var hero = await GetThumbnailAsync(path, 512);
                if (hero is not null)
                {
                    details.HeroImage = IconInfo.FromStream(hero);
                }

                if (TextExtensions.Contains(extension))
                {
                    var text = await ReadHeadAsync(path, 2048);
                    if (!string.IsNullOrEmpty(text))
                    {
                        details.Body = "```\n" + text.Replace("```", "'''", StringComparison.Ordinal) + "\n```";
                    }
                }
                else if (hero is null)
                {
                    details.Body = "_没有可用缩略图。按 Ctrl+K 选择「用 Peek 预览」查看内容。_";
                }

                return;
            }

            // Images: only ONE preview goes into the pane — the host renders HeroImage and
            // the markdown body in two separate places, so setting both shows two images.
            var bodySet = false;
            if (previewWidth > 0)
            {
                try
                {
                    // Pre-downscale into a temp file. The host decodes images at full
                    // resolution before applying layout hints, so very large images can
                    // fail to load; a small file also always fits the details column.
                    var previewPath = await GetResizedPreviewAsync(path, previewWidth);
                    if (previewPath is not null)
                    {
                        // fit=fit makes the host size the image from the details column
                        // width (min(column, natural)) and re-layout on resize.
                        details.Body = $"![preview](<{new Uri(previewPath).AbsoluteUri}?--x-cmdpal-fit=fit>)";
                        bodySet = true;
                    }
                }
                catch (Exception ex)
                {
                    HostLog.Error($"preview resize failed for '{path}': {ex.Message}");
                }
            }

            var heroThumb = bodySet ? null : await GetThumbnailAsync(path, 512);
            if (heroThumb is not null)
            {
                details.HeroImage = IconInfo.FromStream(heroThumb);
            }

            if (heroThumb is null && !bodySet)
            {
                details.Body = "_没有可用预览。按 Ctrl+K 选择「用 Peek 预览」查看内容。_";
            }
        }
        catch (Exception ex)
        {
            HostLog.Error($"preview failed for '{path}': {ex.Message}");
        }
    }

    private static async Task<IRandomAccessStream?> GetThumbnailAsync(string path, uint size)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            return await file.GetThumbnailAsync(ThumbnailMode.SingleItem, size);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> GetResizedPreviewAsync(string path, int width)
    {
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "EverythingCmdPal", "preview");
            Directory.CreateDirectory(directory);

            var stamp = File.GetLastWriteTimeUtc(path).Ticks;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..16];
            var target = Path.Combine(directory, $"{hash}_{stamp}_{width}.png");

            if (File.Exists(target) && new FileInfo(target).Length > 0)
            {
                return target;
            }

            var thumb = await GetThumbnailAsync(path, (uint)Math.Clamp(width, 160, 1024));
            if (thumb is null) return null;

            var length = (uint)Math.Min(thumb.Size, 32u * 1024 * 1024);
            if (length == 0) return null;

            thumb.Seek(0);
            using var reader = new DataReader(thumb);
            await reader.LoadAsync(length);

            var buffer = new byte[length];
            reader.ReadBytes(buffer);

            await using (var file = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await file.WriteAsync(buffer.AsMemory(0, (int)length));
                if (file.Length == 0) return null;
            }

            PruneCache(directory);
            return target;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 预览缓存只增不减会撑满 %TEMP%：超过条数或体积上限时，按最后写入时间删最旧的。
    /// 尽力而为，删不掉（被占用）就跳过，不影响预览本身。返回实际删除的文件数。
    /// </summary>
    internal static int PruneCache(string directory, int maxFiles = MaxCachedPreviews, long maxBytes = MaxCacheBytes)
    {
        var deleted = 0;
        try
        {
            var files = new DirectoryInfo(directory).GetFiles("*.png");
            long totalBytes = 0;
            foreach (var file in files) totalBytes += file.Length;

            if (files.Length <= maxFiles && totalBytes <= maxBytes) return 0;

            Array.Sort(files, static (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));

            var remaining = files.Length;
            foreach (var file in files)
            {
                if (remaining <= maxFiles && totalBytes <= maxBytes) break;

                try
                {
                    var size = file.Length;
                    file.Delete();
                    remaining--;
                    totalBytes -= size;
                    deleted++;
                }
                catch
                {
                    // 正被宿主或其他实例读取，下一轮再清。
                }
            }
        }
        catch
        {
            // 清理失败不影响预览。
        }

        return deleted;
    }

    private static async Task<string?> ReadHeadAsync(string path, int maxBytes)
    {
        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);

            var length = (int)Math.Min(stream.Length, maxBytes);
            if (length <= 0) return null;

            var buffer = new byte[length];
            var read = await stream.ReadAsync(buffer.AsMemory(0, length));

            var sniff = Math.Min(read, 4096);
            for (var i = 0; i < sniff; i++)
            {
                if (buffer[i] == 0) return null;
            }

            return Encoding.UTF8.GetString(buffer, 0, read);
        }
        catch
        {
            return null;
        }
    }
}
