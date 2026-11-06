using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CmdPalLinks.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Services;

internal enum AddStatus
{
    Added,
    Duplicate,
    InvalidTarget,
    SaveFailed,
}

internal readonly record struct AddResult(AddStatus Status, LinkItem? Item, string Message);

internal enum UpdateStatus
{
    Updated,
    Duplicate,
    InvalidTarget,
    SaveFailed,
    NotFound,
}

internal readonly record struct UpdateResult(UpdateStatus Status, LinkItem? Item, string Message);

internal readonly record struct LoadResult(string Source, int ItemCount, double ElapsedMs);

internal enum ImportStatus
{
    Imported,
    NoChanges,
    Nothing,
    SaveFailed,
}

internal readonly record struct ImportResult(ImportStatus Status, int Added, int Updated, int Skipped, int Total, string Message);

internal readonly record struct UndoResult(bool Success, int Restored, string Message);

internal readonly record struct ReplaceResult(bool Success, int Total, int Added, int Updated, int Removed, string Message, int ErrorLine);

internal sealed class LibraryStore
{
    /// <summary>
    /// 2 = 新增 iconKind / iconColor / iconEmoji（旧文件读取时补默认值，不丢字段）。
    /// </summary>
    private const int SchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _backupPath;
    private readonly string _tempPath;
    private readonly string _importBackupPath;
    private readonly List<LinkItem> _items = new();
    private Task<LoadResult>? _initialLoad;

    public LibraryStore()
        : this(DefaultPath())
    {
    }

    internal LibraryStore(string path)
    {
        _path = path;
        _backupPath = path + ".bak";
        _tempPath = path + ".tmp";
        _importBackupPath = path + ".import-bak";
    }

    public event Action? Changed;

    public string FilePath => _path;

    public string LoadSource { get; private set; } = "empty";

    public IReadOnlyList<LinkItem> Items
    {
        get
        {
            WaitForLoad();
            lock (_gate)
            {
                return _items.ToArray();
            }
        }
    }

    /// <summary>
    /// Starts the initial load on a background thread so extension startup does not
    /// block on JSON parsing. Subsequent access waits for the same task.
    /// </summary>
    public Task<LoadResult> StartLoading()
    {
        lock (_gate)
        {
            return _initialLoad ??= Task.Run(Load);
        }
    }

    public void WaitForLoad()
    {
        try
        {
            StartLoading().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            HostLog.Error($"store load wait failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static string DefaultPath()
    {
        var directory = Utilities.BaseSettingsPath("CmdPalLinks");
        return Path.Combine(directory, "links.json");
    }

    public LoadResult Load()
    {
        var stopwatch = Stopwatch.StartNew();
        var source = "empty";
        var loaded = new List<LinkItem>();

        if (File.Exists(_path))
        {
            try
            {
                loaded = ReadFile(_path);
                source = "main";
            }
            catch (Exception ex)
            {
                HostLog.Error($"store load main failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (source != "main" && File.Exists(_backupPath))
        {
            try
            {
                loaded = ReadFile(_backupPath);
                source = "bak";
            }
            catch (Exception ex)
            {
                HostLog.Error($"store load bak failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        lock (_gate)
        {
            _items.Clear();
            _items.AddRange(loaded);
            LoadSource = source;
        }

        stopwatch.Stop();
        var elapsed = stopwatch.Elapsed.TotalMilliseconds;
        HostLog.Info($"store loaded: {loaded.Count} items in {elapsed:F2} ms (source={source})");
        return new LoadResult(source, loaded.Count, elapsed);
    }

    public AddResult Add(
        string kind,
        string title,
        string target,
        string[]? aliases = null,
        string[]? tags = null,
        string iconKind = IconKinds.Auto,
        string iconColor = "",
        string iconEmoji = "")
    {
        WaitForLoad();
        kind = (kind ?? string.Empty).Trim().ToLowerInvariant();
        target = (target ?? string.Empty).Trim();

        if (!TryNormalize(kind, target, out _, out var error))
        {
            return new AddResult(AddStatus.InvalidTarget, null, error);
        }

        if (!DedupeKey(kind, target, out var normalized, out _))
        {
            return new AddResult(AddStatus.InvalidTarget, null, error);
        }

        var now = DateTimeOffset.UtcNow;
        var item = new LinkItem
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Title = string.IsNullOrWhiteSpace(title) ? DefaultTitle(kind, target) : title.Trim(),
            Target = target,
            Aliases = Clean(aliases),
            Tags = Clean(tags),
            IconKind = IconKinds.Normalize(iconKind),
            IconColor = (iconColor ?? string.Empty).Trim(),
            IconEmoji = (iconEmoji ?? string.Empty).Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        AddResult result;
        lock (_gate)
        {
            var duplicate = _items.FirstOrDefault(existing => SameKey(existing, kind, normalized));
            if (duplicate is not null)
            {
                return new AddResult(AddStatus.Duplicate, duplicate, "已存在");
            }

            _items.Add(item);
            try
            {
                SaveLocked();
                result = new AddResult(AddStatus.Added, item, "已添加");
            }
            catch (Exception ex)
            {
                HostLog.Error($"store save failed: {ex.GetType().Name}: {ex.Message}");
                result = new AddResult(AddStatus.SaveFailed, item, $"已加入列表，但写入磁盘失败：{ex.Message}");
            }
        }

        Changed?.Invoke();
        return result;
    }

    public UpdateResult Update(Guid id, string kind, string title, string target, string[]? aliases = null, string[]? tags = null)
    {
        WaitForLoad();
        kind = (kind ?? string.Empty).Trim().ToLowerInvariant();
        target = (target ?? string.Empty).Trim();

        if (!TryNormalize(kind, target, out _, out var error))
        {
            return new UpdateResult(UpdateStatus.InvalidTarget, null, error);
        }

        if (!DedupeKey(kind, target, out var normalized, out _))
        {
            return new UpdateResult(UpdateStatus.InvalidTarget, null, error);
        }

        UpdateResult result;
        lock (_gate)
        {
            var item = _items.FirstOrDefault(existing => existing.Id == id);
            if (item is null)
            {
                return new UpdateResult(UpdateStatus.NotFound, null, "条目不存在");
            }

            var duplicate = _items.FirstOrDefault(existing => existing.Id != id && SameKey(existing, kind, normalized));
            if (duplicate is not null)
            {
                return new UpdateResult(UpdateStatus.Duplicate, duplicate, "已存在");
            }

            item.Kind = kind;
            item.Title = string.IsNullOrWhiteSpace(title) ? DefaultTitle(kind, target) : title.Trim();
            item.Target = target;
            item.Aliases = Clean(aliases);
            item.Tags = Clean(tags);
            item.UpdatedAt = DateTimeOffset.UtcNow;

            try
            {
                SaveLocked();
                result = new UpdateResult(UpdateStatus.Updated, item, "已保存");
            }
            catch (Exception ex)
            {
                HostLog.Error($"store update save failed: {ex.GetType().Name}: {ex.Message}");
                result = new UpdateResult(UpdateStatus.SaveFailed, item, $"已修改，但写入磁盘失败：{ex.Message}");
            }
        }

        Changed?.Invoke();
        return result;
    }

    public bool Remove(Guid id)
    {
        WaitForLoad();
        var removed = false;
        lock (_gate)
        {
            var item = _items.FirstOrDefault(existing => existing.Id == id);
            if (item is null)
            {
                return false;
            }

            _items.Remove(item);
            removed = true;
            try
            {
                SaveLocked();
            }
            catch (Exception ex)
            {
                HostLog.Error($"store remove save failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Changed?.Invoke();
        return removed;
    }

    public void RecordOpen(Guid id)
    {
        WaitForLoad();
        var changed = false;
        lock (_gate)
        {
            var item = _items.FirstOrDefault(existing => existing.Id == id);
            if (item is null)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            item.OpenCount++;
            item.LastOpenedAt = now;
            item.UpdatedAt = now;

            try
            {
                SaveLocked();
            }
            catch (Exception ex)
            {
                HostLog.Error($"store save failed (record open): {ex.GetType().Name}: {ex.Message}");
            }

            changed = true;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// 批量导入：单锁 + 单次原子写；写前保留一份快照，失败回滚内存并保留快照文件。
    /// </summary>
    public ImportResult ImportBatch(IReadOnlyList<LinkItem> incoming, bool overwrite)
    {
        WaitForLoad();
        if (incoming.Count == 0)
        {
            return new ImportResult(ImportStatus.Nothing, 0, 0, 0, _items.Count, "没有可导入的条目");
        }

        var added = 0;
        var updated = 0;
        var skipped = 0;
        var total = 0;

        lock (_gate)
        {
            if (!BackupForImportLocked())
            {
                return new ImportResult(ImportStatus.SaveFailed, 0, 0, 0, _items.Count, "无法写入导入快照，已取消导入（现有数据未改动）");
            }

            try
            {
                var known = new Dictionary<string, LinkItem>(StringComparer.OrdinalIgnoreCase);
                foreach (var existing in _items)
                {
                    if (DedupeKey(existing.Kind, existing.Target, out var key, out _))
                    {
                        known[key] = existing;
                    }
                }

                foreach (var source in incoming)
                {
                    if (!DedupeKey(source.Kind, source.Target, out var key, out _))
                    {
                        skipped++;
                        continue;
                    }

                    var existing = known.GetValueOrDefault(key);
                    if (existing is not null)
                    {
                        if (!overwrite)
                        {
                            skipped++;
                            continue;
                        }

                        existing.Title = string.IsNullOrWhiteSpace(source.Title) ? existing.Title : source.Title;
                        existing.Aliases = Merge(existing.Aliases, source.Aliases);
                        existing.Tags = Merge(existing.Tags, source.Tags);
                        existing.IconKind = source.IconKind;
                        existing.IconColor = (source.IconColor ?? string.Empty).Trim();
                        existing.IconEmoji = (source.IconEmoji ?? string.Empty).Trim();
                        existing.UpdatedAt = DateTimeOffset.UtcNow;
                        updated++;
                        continue;
                    }

                    var item = new LinkItem
                    {
                        Id = Guid.NewGuid(),
                        Kind = source.Kind,
                        Title = string.IsNullOrWhiteSpace(source.Title) ? DefaultTitle(source.Kind, source.Target) : source.Title,
                        Target = source.Target,
                        Aliases = Clean(source.Aliases),
                        Tags = Clean(source.Tags),
                        IconKind = IconKinds.Normalize(source.IconKind),
                        IconColor = (source.IconColor ?? string.Empty).Trim(),
                        IconEmoji = (source.IconEmoji ?? string.Empty).Trim(),
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    };
                    _items.Add(item);
                    known[key] = item;
                    added++;
                }

                total = _items.Count;
                if (added == 0 && updated == 0)
                {
                    return new ImportResult(ImportStatus.NoChanges, 0, 0, skipped, total, "没有新增条目（全部跳过）");
                }

                SaveLocked();
            }
            catch (Exception ex)
            {
                HostLog.Error($"store import failed: {ex.GetType().Name}: {ex.Message}");
                try
                {
                    Load();
                }
                catch (Exception reloadEx)
                {
                    HostLog.Error($"store import rollback failed: {reloadEx.GetType().Name}: {reloadEx.Message}");
                }

                return new ImportResult(ImportStatus.SaveFailed, 0, 0, skipped, _items.Count, $"写入磁盘失败，已回滚：{ex.Message}");
            }
        }

        Changed?.Invoke();
        return new ImportResult(
            ImportStatus.Imported,
            added,
            updated,
            skipped,
            total,
            $"导入完成：新增 {added}、更新 {updated}、跳过 {skipped}");
    }

    public UndoResult UndoLastImport()
    {
        WaitForLoad();
        UndoResult result;
        lock (_gate)
        {
            if (!File.Exists(_importBackupPath))
            {
                return new UndoResult(false, 0, "没有可撤销的导入快照");
            }

            try
            {
                var restored = ReadFile(_importBackupPath);
                _items.Clear();
                _items.AddRange(restored);
                SaveLocked();
                File.Delete(_importBackupPath);
                result = new UndoResult(true, _items.Count, $"已撤销上次导入，现有 {_items.Count} 条");
            }
            catch (Exception ex)
            {
                HostLog.Error($"store undo import failed: {ex.GetType().Name}: {ex.Message}");
                result = new UndoResult(false, 0, $"撤销失败：{ex.Message}");
            }
        }

        if (result.Success)
        {
            Changed?.Invoke();
        }

        return result;
    }

    /// <summary>
    /// 编辑器整表保存：先全量校验，再在同一个锁与同一次原子写里替换内存与磁盘。
    /// 已有条目按 id（其次按判重键）保留 createdAt/openCount/lastOpenedAt，避免编辑一次丢掉统计。
    /// </summary>
    public ReplaceResult ReplaceAll(IReadOnlyList<LinkItem> incoming)
    {
        WaitForLoad();

        var prepared = new List<(LinkItem Item, string Key)>(incoming.Count);
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < incoming.Count; i++)
        {
            var source = incoming[i];
            var line = i + 1;
            var kind = (source.Kind ?? string.Empty).Trim().ToLowerInvariant();
            var target = (source.Target ?? string.Empty).Trim();

            if (!LinkKinds.IsValid(kind))
            {
                return Fail(line, "类型必须是 url、file 或 folder");
            }

            if (!DedupeKey(kind, target, out var key, out var error))
            {
                return Fail(line, error);
            }

            if (seen.TryGetValue(key, out var first))
            {
                return Fail(line, $"target 与第 {first} 行重复");
            }

            seen[key] = line;

            var emoji = FirstTextElement(source.IconEmoji);
            var colorText = (source.IconColor ?? string.Empty).Trim();
            var iconKind = IconKinds.Normalize(source.IconKind);

            if (string.Equals(colorText, "none", StringComparison.OrdinalIgnoreCase))
            {
                iconKind = emoji.Length > 0 ? IconKinds.Emoji : IconKinds.Transparent;
                colorText = string.Empty;
            }
            else if (colorText.Length > 0)
            {
                if (!IsHexColor(colorText))
                {
                    return Fail(line, "底色需为 #RRGGBB、none 或留空");
                }

                colorText = colorText.ToUpperInvariant();
                iconKind = emoji.Length > 0 ? IconKinds.Emoji : IconKinds.Block;
            }
            else if (iconKind != IconKinds.Transparent && iconKind != IconKinds.Block)
            {
                // 色块可以没有显式底色（渲染时用默认色），不能在这里被降级成 auto 丢掉。
                iconKind = emoji.Length > 0 ? IconKinds.Emoji : IconKinds.Auto;
            }

            var item = new LinkItem
            {
                Id = source.Id,
                Kind = kind,
                Title = string.IsNullOrWhiteSpace(source.Title) ? DefaultTitle(kind, target) : source.Title.Trim(),
                Target = target,
                Aliases = Clean(source.Aliases),
                Tags = Clean(source.Tags),
                IconKind = iconKind,
                IconColor = colorText,
                IconEmoji = emoji,
                CreatedAt = now,
                UpdatedAt = now,
            };

            prepared.Add((item, key));
        }

        var added = 0;
        var updated = 0;
        var removed = 0;
        var total = 0;

        lock (_gate)
        {
            var byId = new Dictionary<Guid, LinkItem>();
            var byKey = new Dictionary<string, LinkItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in _items)
            {
                byId[existing.Id] = existing;
                if (DedupeKey(existing.Kind, existing.Target, out var existingKey, out _))
                {
                    byKey[existingKey] = existing;
                }
            }

            var previous = _items.ToArray();
            var next = new List<LinkItem>(prepared.Count);
            var matched = new HashSet<Guid>();
            var startedAt = DateTimeOffset.UtcNow;

            foreach (var (item, key) in prepared)
            {
                LinkItem? existing = null;
                if (item.Id != Guid.Empty && byId.TryGetValue(item.Id, out var byIdItem))
                {
                    existing = byIdItem;
                }
                else
                {
                    byKey.TryGetValue(key, out existing);
                }

                if (existing is not null)
                {
                    matched.Add(existing.Id);
                    item.Id = existing.Id;
                    item.CreatedAt = existing.CreatedAt;
                    item.OpenCount = existing.OpenCount;
                    item.LastOpenedAt = existing.LastOpenedAt;
                    updated++;
                }
                else
                {
                    item.Id = Guid.NewGuid();
                    item.CreatedAt = startedAt;
                    added++;
                }

                item.UpdatedAt = startedAt;
                next.Add(item);
            }

            removed = previous.Count(existing => !matched.Contains(existing.Id));
            _items.Clear();
            _items.AddRange(next);
            total = next.Count;

            try
            {
                SaveLocked();
            }
            catch (Exception ex)
            {
                _items.Clear();
                _items.AddRange(previous);
                HostLog.Error($"store replace save failed: {ex.GetType().Name}: {ex.Message}");
                return Fail(0, $"写入磁盘失败：{ex.Message}");
            }
        }

        Changed?.Invoke();
        return new ReplaceResult(true, total, added, updated, removed, $"已保存 {total} 条（新增 {added}、更新 {updated}、删除 {removed}）", 0);
    }

    private static ReplaceResult Fail(int line, string message)
        => new(false, 0, 0, 0, 0, message, line);

    private static string FirstTextElement(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? string.Empty : System.Globalization.StringInfo.GetNextTextElement(text).Trim();
    }

    private static bool IsHexColor(string value)
    {
        if (value.Length != 7 || value[0] != '#')
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!Uri.IsHexDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>导出完整 JSON（含 icon 字段、schemaVersion、updatedAt），可原样回导。</summary>
    public bool ExportJson(string path)
    {
        WaitForLoad();
        try
        {
            string json;
            lock (_gate)
            {
                var data = new LibraryData
                {
                    SchemaVersion = SchemaVersion,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Items = new List<LinkItem>(_items),
                };
                json = JsonSerializer.Serialize(data, JsonOptions);
            }

            return WriteAtomic(path, Encoding.UTF8.GetBytes(json));
        }
        catch (Exception ex)
        {
            HostLog.Error($"store export json failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>导出 CSV（UTF-8 BOM、CRLF、严格表头），与 HTML 编辑页表结构一致。</summary>
    public bool ExportCsv(string path)
    {
        WaitForLoad();
        try
        {
            return WriteAtomic(path, CsvCodec.ToCsvBytes(Items));
        }
        catch (Exception ex)
        {
            HostLog.Error($"store export csv failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private bool BackupForImportLocked()
    {
        try
        {
            // 保存“导入前内存状态”，即使主文件尚不存在也能撤销首次导入。
            var data = new LibraryData
            {
                SchemaVersion = SchemaVersion,
                UpdatedAt = DateTimeOffset.UtcNow,
                Items = new List<LinkItem>(_items),
            };
            File.WriteAllBytes(_importBackupPath, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, JsonOptions)));
            return true;
        }
        catch (Exception ex)
        {
            HostLog.Error($"store import backup failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool WriteAtomic(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        if (File.Exists(path))
        {
            File.Replace(temp, path, null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, path);
        }

        return true;
    }

    private static string[] Merge(string[]? left, string[]? right)
    {
        var merged = new List<string>();
        foreach (var value in Clean(left).Concat(Clean(right)))
        {
            if (!merged.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                merged.Add(value);
            }
        }

        return merged.ToArray();
    }

    internal static bool TryNormalize(string kind, string target, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        if (!LinkKinds.IsValid(kind))
        {
            error = "类型必须是 url、file 或 folder";
            return false;
        }

        if (kind == LinkKinds.Url)
        {
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri)
                || (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
            {
                error = "网址必须是以 http:// 或 https:// 开头的完整 URL";
                return false;
            }

            var scheme = uri.Scheme.ToLowerInvariant();
            var host = uri.Host.ToLowerInvariant();
            var path = uri.AbsolutePath == "/" ? string.Empty : uri.AbsolutePath;
            var prefix = uri.IsDefaultPort ? $"{scheme}://{host}" : $"{scheme}://{host}:{uri.Port}";
            normalized = prefix + path + uri.Query;
            return true;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(target);
        }
        catch (Exception)
        {
            error = "路径无效";
            return false;
        }

        if (kind == LinkKinds.File && !File.Exists(fullPath))
        {
            error = "文件不存在";
            return false;
        }

        if (kind == LinkKinds.Folder && !Directory.Exists(fullPath))
        {
            error = "文件夹不存在";
            return false;
        }

        normalized = fullPath.TrimEnd('\\', '/');
        return true;
    }

    private static List<LinkItem> ReadFile(string path)
    {
        var json = File.ReadAllText(path, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("empty file");
        }

        var data = JsonSerializer.Deserialize<LibraryData>(json, JsonOptions);
        if (data?.Items is null)
        {
            throw new InvalidDataException("missing items");
        }

        foreach (var item in data.Items)
        {
            if (item.Id == Guid.Empty)
            {
                item.Id = Guid.NewGuid();
            }

            item.Aliases ??= Array.Empty<string>();
            item.Tags ??= Array.Empty<string>();
            item.IconKind = IconKinds.Normalize(item.IconKind);
            item.IconColor = (item.IconColor ?? string.Empty).Trim();
            item.IconEmoji = (item.IconEmoji ?? string.Empty).Trim();

            if (!LinkKinds.IsValid(item.Kind))
            {
                HostLog.Error($"store load: unknown kind '{item.Kind}' id={item.Id}");
            }
        }

        return data.Items.Where(item => !string.IsNullOrWhiteSpace(item.Target)).ToList();
    }

    private void SaveLocked()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = new LibraryData
        {
            SchemaVersion = SchemaVersion,
            UpdatedAt = DateTimeOffset.UtcNow,
            Items = new List<LinkItem>(_items),
        };

        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(_tempPath, json, Encoding.UTF8);

        if (File.Exists(_path))
        {
            File.Replace(_tempPath, _path, _backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(_tempPath, _path);
        }
    }

    private static bool SameKey(LinkItem item, string kind, string normalized)
    {
        if (!string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!DedupeKey(item.Kind, item.Target, out var existing, out _))
        {
            return false;
        }

        return string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判重键：URL 忽略大小写 host、忽略末尾斜杠与默认端口；路径忽略末尾分隔符与大小写。
    /// 与 TryNormalize 不同，这里不要求文件/文件夹真实存在（导入清单允许预登记）。
    /// </summary>
    internal static bool DedupeKey(string kind, string target, out string key, out string error)
    {
        key = string.Empty;
        error = string.Empty;
        kind = (kind ?? string.Empty).Trim().ToLowerInvariant();
        target = (target ?? string.Empty).Trim();

        if (!LinkKinds.IsValid(kind))
        {
            error = "类型必须是 url、file 或 folder";
            return false;
        }

        if (kind == LinkKinds.Url)
        {
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri)
                || (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
            {
                error = "网址必须是以 http:// 或 https:// 开头的完整 URL";
                return false;
            }

            var scheme = uri.Scheme.ToLowerInvariant();
            var host = uri.Host.ToLowerInvariant();
            var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
            key = $"{scheme}://{host}{port}{uri.AbsolutePath.TrimEnd('/')}{uri.Query}";
            return true;
        }

        if (!Path.IsPathRooted(target))
        {
            error = "必须是绝对路径";
            return false;
        }

        try
        {
            key = Path.GetFullPath(target).TrimEnd('\\', '/').ToLowerInvariant();
        }
        catch (Exception)
        {
            error = "路径无效";
            return false;
        }

        return true;
    }

    internal static string DefaultTitle(string kind, string target)
    {
        if (kind == LinkKinds.Url && Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath == "/" ? string.Empty : uri.AbsolutePath;
            return uri.Host + path;
        }

        if (kind == LinkKinds.File)
        {
            var name = Path.GetFileNameWithoutExtension(target);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        if (kind == LinkKinds.Folder)
        {
            var name = Path.GetFileName(target.TrimEnd('\\', '/'));
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return target;
    }

    internal static string[] Clean(string[]? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Select(value => (value ?? string.Empty).Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
