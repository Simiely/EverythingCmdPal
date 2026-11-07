using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CmdPalLinks.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace CmdPalLinks.Services;

/// <summary>
/// 列表图标：URL 用站点 favicon（本地缓存 + 后台补齐），file/folder 用 SHGetFileInfo 提取的 Shell 图标。
/// Get() 只读内存缓存，绝不发起网络或 Shell 调用，保证列表加载不被阻塞。
/// </summary>
internal sealed class IconCache
{
    private const long MaxBytes = 512 * 1024;
    private const int MaxParallelDownloads = 3;

    private static readonly TimeSpan Ttl = TimeSpan.FromDays(30);
    private static readonly TimeSpan PruneAfter = TimeSpan.FromDays(90);
    private static readonly TimeSpan ValidateInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);
    private static readonly string[] KnownExtensions = [".ico", ".png", ".jpg", ".gif", ".webp"];
    private static readonly string[] Schemes = ["https", "http"];

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(6),
    };

    private sealed record ShellEntry(IconInfo Icon, long Stamp, string Source, int Width, int Height);

    private readonly string _dir;
    private readonly SemaphoreSlim _downloads = new(MaxParallelDownloads, MaxParallelDownloads);
    private readonly ConcurrentDictionary<string, byte> _inflight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _failed = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ShellEntry> _shell = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _shellInflight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _shellAttempted = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _shellValidated = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _missing = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _missingChecked = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IconInfo?> _custom = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _colorInflight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _colorFailed = new(StringComparer.OrdinalIgnoreCase);
    private int _notifyPending;

    public IconCache()
    {
        _dir = Path.Combine(Utilities.BaseSettingsPath("CmdPalLinks"), "icons");
        _ = Task.Run(Prune);
    }

    public event Action? Changed;

    /// <summary>同步判断条目是否缺失：本地路径直接检查，UNC 路径先返回 false 并在后台核实。</summary>
    public bool IsMissing(LinkItem item)
    {
        if (!string.Equals(item.Kind, LinkKinds.Url, StringComparison.OrdinalIgnoreCase))
        {
            var key = KeyOf(item.Target);
            if (_missing.TryGetValue(key, out var known))
            {
                ScheduleMissingCheck(key, item.Target, item.Kind, known);
                return known;
            }

            if (IsUncPath(item.Target))
            {
                _missing[key] = false;
                ScheduleMissingCheck(key, item.Target, item.Kind, false);
                return false;
            }

            var missing = !Exists(item.Target, item.Kind);
            _missing[key] = missing;
            _missingChecked[key] = DateTime.UtcNow.Ticks;
            return missing;
        }

        return false;
    }

    /// <summary>同步取图标；未命中时返回通用图标并在后台补齐，完成后触发 <see cref="Changed"/>。</summary>
    public IconInfo Get(LinkItem item)
    {
        // 自定义图标来源（iTab 的 src）优先：下载原图并按 URL 缓存；失败后回落到自绘/favicon。
        var source = LibraryStore.NormalizeIconSource(item.IconSource);
        if (source.Length > 0 && string.Equals(item.Kind, LinkKinds.Url, StringComparison.OrdinalIgnoreCase))
        {
            var sourceKey = "src|" + source;
            var sourceCached = FindCached(sourceKey, out var sourceExpired);
            if (sourceCached is not null)
            {
                if (sourceExpired)
                {
                    ScheduleSource(sourceKey, source);
                }

                return new IconInfo(sourceCached);
            }

            if (!_failed.ContainsKey(sourceKey))
            {
                ScheduleSource(sourceKey, source);
                return Icons.ForKind(item.Kind);
            }
        }

        if (IconKinds.IsCustom(item.IconKind, item.IconEmoji, item.IconColor))
        {
            return GetCustom(item);
        }

        if (!string.Equals(item.Kind, LinkKinds.Url, StringComparison.OrdinalIgnoreCase))
        {
            var key = KeyOf(item.Target);
            if (IsMissing(item))
            {
                return Icons.ForKind(item.Kind);
            }

            if (_shell.TryGetValue(key, out var entry))
            {
                ScheduleValidate(key, item.Target, item.Kind);
                return entry.Icon;
            }

            ScheduleShell(key, item.Target, item.Kind);
            return Icons.ForKind(item.Kind);
        }

        var host = HostOf(item.Target);
        if (host.Length == 0)
        {
            return Icons.ForKind(item.Kind);
        }

        var cached = FindCached(host, out var expired);
        if (cached is not null)
        {
            if (expired)
            {
                Schedule(host);
            }

            return new IconInfo(cached);
        }

        Schedule(host);
        return Icons.ForKind(item.Kind);
    }

    /// <summary>
    /// 自绘图标（色块/透明底/emoji）：按 kind|color|emoji|title 哈希缓存。
    /// emoji 条目先用 GDI 单色轮廓同步出图（保证列表立刻有图标），
    /// 同时在后台用独立子进程渲染彩色字形 PNG（<c>color-*.png</c>），成功后再替换并通知刷新。
    /// key 带渲染版本前缀：渲染实现变化后旧 PNG 不再命中，避免复用历史错误产物。
    /// </summary>
    private IconInfo GetCustom(LinkItem item)
    {
        var key = "r3|" + string.Join(
            '|',
            IconKinds.Normalize(item.IconKind),
            item.IconColor ?? string.Empty,
            item.IconEmoji ?? string.Empty,
            item.Title ?? string.Empty);

        if (_custom.TryGetValue(key, out var cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            Directory.CreateDirectory(_dir);
            var stem = Stem(key);
            var lightPath = Path.Combine(_dir, "custom-" + stem + "-light.png");
            var darkPath = Path.Combine(_dir, "custom-" + stem + "-dark.png");
            var colorPath = Path.Combine(_dir, "color-" + stem + ".png");

            var emoji = (item.IconEmoji ?? string.Empty).Trim();
            var useColor = emoji.Length > 0 && IconKinds.Normalize(item.IconKind) == IconKinds.Emoji;

            if (useColor && File.Exists(colorPath))
            {
                var colorIcon = new IconInfo(new IconData(colorPath), new IconData(colorPath));
                _custom[key] = colorIcon;
                HostLog.Info($"color emoji loaded: kind={item.IconKind} emoji='{item.IconEmoji}'");
                return colorIcon;
            }

            if (!File.Exists(lightPath))
            {
                WriteCustom(lightPath, CustomIconRenderer.Render(item, dark: false));
            }

            if (!File.Exists(darkPath))
            {
                WriteCustom(darkPath, CustomIconRenderer.Render(item, dark: true));
            }

            var icon = new IconInfo(new IconData(lightPath), new IconData(darkPath));
            _custom[key] = icon;

            if (useColor)
            {
                ScheduleColor(key, item, colorPath);
            }
            else
            {
                HostLog.Info($"custom icon loaded: kind={item.IconKind} emoji='{item.IconEmoji}' color='{item.IconColor}'");
            }

            return icon;
        }
        catch (Exception ex)
        {
            HostLog.Error($"custom icon failed: {ex.GetType().Name}: {ex.Message}");
            return Icons.ForKind(item.Kind);
        }
    }

    /// <summary>后台子进程渲染彩色 emoji；失败/无彩色字形时保持现有单色图标。</summary>
    private void ScheduleColor(string key, LinkItem item, string colorPath)
    {
        if (_colorFailed.ContainsKey(key) || !_colorInflight.TryAdd(key, 0))
        {
            return;
        }

        var emoji = (item.IconEmoji ?? string.Empty).Trim();
        // 与单色渲染一致：emoji 只有在显式 #RRGGBB 底色时才叠色块，none/留空为透明底。
        CustomIconRenderer.ParseColor(item.IconColor, out var hasColor);
        var block = hasColor && IconKinds.Normalize(item.IconKind) == IconKinds.Emoji;

        _ = Task.Run(() =>
        {
            try
            {
                var png = EmojiRenderClient.Render(emoji, item.IconColor, block, colorPath + ".tmp");
                if (png is null)
                {
                    _colorFailed.TryAdd(key, 0);
                    HostLog.Info($"color emoji fallback: mono outline emoji='{emoji}' (no color glyph or child render failed)");
                    return;
                }

                WriteCustom(colorPath, png);
                var icon = new IconInfo(new IconData(colorPath), new IconData(colorPath));
                _custom[key] = icon;
                HostLog.Info($"color emoji rendered: emoji='{emoji}' bytes={png.Length}");
                NotifyChanged();
            }
            catch (Exception ex)
            {
                _colorFailed.TryAdd(key, 0);
                HostLog.Error($"color emoji background failed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _colorInflight.TryRemove(key, out _);
            }
        });
    }

    private static void WriteCustom(string path, byte[] bytes)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }

    private void ScheduleShell(string key, string path, string kind)
    {
        if (_shell.ContainsKey(key) || !_shellInflight.TryAdd(key, 0))
        {
            return;
        }

        if (_shellAttempted.TryGetValue(key, out var last) && DateTime.UtcNow.Ticks - last < RetryInterval.Ticks)
        {
            _shellInflight.TryRemove(key, out _);
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _downloads.WaitAsync().ConfigureAwait(false);
                try
                {
                    var exists = Exists(path, kind);
                    _missing[key] = !exists;
                    _missingChecked[key] = DateTime.UtcNow.Ticks;
                    if (!exists)
                    {
                        HostLog.Info($"shell icon skipped: path not found: {path}");
                        NotifyChanged();
                        return;
                    }

                    var started = Stopwatch.StartNew();
                    var result = ShellIconExtractor.Extract(path);
                    started.Stop();

                    if (result is null)
                    {
                        _shellAttempted[key] = DateTime.UtcNow.Ticks;
                        HostLog.Info($"shell icon unavailable: {path} took={started.Elapsed.TotalMilliseconds:F1} ms fallback=generic");
                        NotifyChanged();
                        return;
                    }

                    var icon = await ToIconInfoAsync(result.Value.Png).ConfigureAwait(false);
                    _shell[key] = new ShellEntry(icon, Stamp(path, kind), result.Value.Source, result.Value.Width, result.Value.Height);
                    _shellAttempted.TryRemove(key, out _);
                    _shellValidated[key] = DateTime.UtcNow.Ticks;
                    HostLog.Info($"shell icon loaded: {path} source={result.Value.Source} {result.Value.Width}x{result.Value.Height} bytes={result.Value.Png.Length} took={started.Elapsed.TotalMilliseconds:F1} ms");
                    NotifyChanged();
                }
                finally
                {
                    _downloads.Release();
                }
            }
            catch (Exception ex)
            {
                _shellAttempted[key] = DateTime.UtcNow.Ticks;
                HostLog.Error($"shell icon failed: {path} {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _shellInflight.TryRemove(key, out _);
            }
        });
    }

    /// <summary>文件/目录内容变化时让缓存失效；每 30 秒最多检查一次，全部在后台线程。</summary>
    private void ScheduleValidate(string key, string path, string kind)
    {
        var now = DateTime.UtcNow.Ticks;
        if (_shellValidated.TryGetValue(key, out var last) && now - last < ValidateInterval.Ticks)
        {
            return;
        }

        _shellValidated[key] = now;
        _ = Task.Run(() =>
        {
            try
            {
                var exists = Exists(path, kind);
                _missing[key] = !exists;
                _missingChecked[key] = DateTime.UtcNow.Ticks;
                if (!exists)
                {
                    if (_shell.TryRemove(key, out _))
                    {
                        HostLog.Info($"shell icon invalidated: path not found: {path}");
                        NotifyChanged();
                    }

                    return;
                }

                var stamp = Stamp(path, kind);
                if (_shell.TryGetValue(key, out var entry) && entry.Stamp != stamp)
                {
                    _shell.TryRemove(key, out _);
                    HostLog.Info($"shell icon invalidated: path changed: {path}");
                    NotifyChanged();
                }
            }
            catch (Exception ex)
            {
                HostLog.Error($"shell icon validate failed: {path} {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    private void ScheduleMissingCheck(string key, string path, string kind, bool known)
    {
        var now = DateTime.UtcNow.Ticks;
        if (_missingChecked.TryGetValue(key, out var last) && now - last < ValidateInterval.Ticks)
        {
            return;
        }

        _missingChecked[key] = now;
        _ = Task.Run(() =>
        {
            try
            {
                var missing = !Exists(path, kind);
                if (missing == known)
                {
                    return;
                }

                _missing[key] = missing;
                HostLog.Info($"path health changed: missing={missing} path={path}");
                NotifyChanged();
            }
            catch (Exception ex)
            {
                HostLog.Error($"path health check failed: {path} {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    private string? FindCached(string host, out bool expired)
    {
        expired = false;
        var stem = Stem(host);
        foreach (var ext in KnownExtensions)
        {
            var path = Path.Combine(_dir, stem + ext);
            if (!File.Exists(path))
            {
                continue;
            }

            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
            expired = age > Ttl || age < TimeSpan.Zero;
            return path;
        }

        return null;
    }

    private void Schedule(string host)
    {
        if (_failed.ContainsKey(host) || !_inflight.TryAdd(host, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _downloads.WaitAsync().ConfigureAwait(false);
                try
                {
                    await FetchAsync(host).ConfigureAwait(false);
                }
                finally
                {
                    _downloads.Release();
                }
            }
            catch (Exception ex)
            {
                HostLog.Error($"favicon task failed: host={host} {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _inflight.TryRemove(host, out _);
            }
        });
    }

    private void ScheduleSource(string key, string url)
    {
        if (_failed.ContainsKey(key) || !_inflight.TryAdd(key, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _downloads.WaitAsync().ConfigureAwait(false);
                try
                {
                    await FetchSourceAsync(key, url).ConfigureAwait(false);
                }
                finally
                {
                    _downloads.Release();
                }
            }
            catch (Exception ex)
            {
                HostLog.Error($"custom icon task failed: url={url} {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _inflight.TryRemove(key, out _);
            }
        });
    }

    private async Task FetchSourceAsync(string key, string url)
    {
        try
        {
            using var response = await Http
                .GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _failed.TryAdd(key, 0);
                HostLog.Info($"custom icon unavailable: url={url} status={response.StatusCode}");
                return;
            }

            var length = response.Content.Headers.ContentLength;
            if (length is > MaxBytes)
            {
                _failed.TryAdd(key, 0);
                HostLog.Info($"custom icon unavailable: url={url} bytes={length} reason=too-large");
                return;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var ext = ExtensionOf(bytes);
            if (ext is null || bytes.LongLength > MaxBytes)
            {
                _failed.TryAdd(key, 0);
                HostLog.Info($"custom icon unavailable: url={url} bytes={bytes.Length} reason=not-an-image");
                return;
            }

            if (await SaveAsync(key, ext, bytes).ConfigureAwait(false))
            {
                HostLog.Info($"custom icon fetched: url={url} ext={ext} bytes={bytes.Length}");
            }
        }
        catch (Exception ex)
        {
            _failed.TryAdd(key, 0);
            HostLog.Error($"custom icon fetch failed: url={url} {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task FetchAsync(string host)
    {
        var started = DateTime.UtcNow;
        foreach (var scheme in Schemes)
        {
            try
            {
                using var response = await Http
                    .GetAsync(new Uri($"{scheme}://{host}/favicon.ico"), HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var length = response.Content.Headers.ContentLength;
                if (length is > MaxBytes)
                {
                    break;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var ext = ExtensionOf(bytes);
                if (ext is null || bytes.LongLength > MaxBytes)
                {
                    HostLog.Error($"favicon rejected: host={host} bytes={bytes.Length} reason=not-an-image");
                    break;
                }

                if (await SaveAsync(host, ext, bytes).ConfigureAwait(false))
                {
                    var took = (DateTime.UtcNow - started).TotalMilliseconds;
                    HostLog.Info($"favicon fetched: host={host} ext={ext} bytes={bytes.Length} took={took:F0} ms");
                    return;
                }
            }
            catch (Exception ex)
            {
                HostLog.Error($"favicon fetch failed: host={host} scheme={scheme} {ex.GetType().Name}: {ex.Message}");
            }
        }

        _failed.TryAdd(host, 0);
        HostLog.Info($"favicon unavailable: host={host} fallback=generic");
    }

    private async Task<bool> SaveAsync(string host, string ext, byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            var path = Path.Combine(_dir, Stem(host) + ext);
            var tmp = path + ".tmp";
            await File.WriteAllBytesAsync(tmp, bytes).ConfigureAwait(false);
            File.Move(tmp, path, true);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            NotifyChanged();
            return true;
        }
        catch (Exception ex)
        {
            HostLog.Error($"favicon save failed: host={host} {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private void NotifyChanged()
    {
        if (Interlocked.CompareExchange(ref _notifyPending, 1, 0) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(500).ConfigureAwait(false);
            Interlocked.Exchange(ref _notifyPending, 0);
            try
            {
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                HostLog.Error($"icon notify failed: {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    private void Prune()
    {
        try
        {
            if (!Directory.Exists(_dir))
            {
                return;
            }

            var cutoff = DateTime.UtcNow - PruneAfter;
            var removed = 0;
            foreach (var file in Directory.EnumerateFiles(_dir))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                    {
                        File.Delete(file);
                        removed++;
                    }
                }
                catch
                {
                    // 单个文件删除失败不影响其余条目。
                }
            }

            if (removed > 0)
            {
                HostLog.Info($"icon cache pruned: {removed} file(s)");
            }
        }
        catch (Exception ex)
        {
            HostLog.Error($"icon cache prune failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<IconInfo> ToIconInfoAsync(byte[] png)
    {
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(png);
            await writer.StoreAsync();
            await writer.FlushAsync();
        }

        stream.Seek(0);
        return IconInfo.FromStream(stream);
    }

    private static bool Exists(string path, string kind) =>
        string.Equals(kind, LinkKinds.Folder, StringComparison.OrdinalIgnoreCase) ? Directory.Exists(path) : File.Exists(path);

    private static long Stamp(string path, string kind)
    {
        try
        {
            if (string.Equals(kind, LinkKinds.Folder, StringComparison.OrdinalIgnoreCase))
            {
                return Directory.GetLastWriteTimeUtc(path).Ticks;
            }

            return File.GetLastWriteTimeUtc(path).Ticks ^ new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsUncPath(string path) => path.StartsWith(@"\\", StringComparison.Ordinal);

    private static string KeyOf(string target)
    {
        var key = target.TrimEnd('\\', '/');
        return (key.Length == 0 ? target : key).ToLowerInvariant();
    }

    private static string HostOf(string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return uri.Scheme is "http" or "https" ? uri.Authority.ToLowerInvariant() : string.Empty;
    }

    private static string Stem(string host) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(host)))[..32].ToLowerInvariant();

    private static string? ExtensionOf(byte[] bytes)
    {
        if (bytes.Length < 4)
        {
            return null;
        }

        if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 1 && bytes[3] == 0)
        {
            return ".ico";
        }

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return ".png";
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return ".jpg";
        }

        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
        {
            return ".gif";
        }

        if (bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46)
        {
            return ".webp";
        }

        return null;
    }
}
