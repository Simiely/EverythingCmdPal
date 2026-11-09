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

internal readonly record struct FolderResult(bool Success, string Message, int Affected = 0);

internal readonly record struct ReplaceResult(bool Success, int Total, int Added, int Updated, int Removed, string Message, int ErrorLine);

internal sealed class LibraryStore
{
    /// <summary>一级文件夹：首页唯一入口。</summary>
    public const string RootFolder = "网页链接";

    /// <summary>二级「常用」：固定第一位，条目归属仍写实际路径，不做虚拟聚合。</summary>
    public const string CommonFolder = "网页链接/常用";

    /// <summary>
    /// 2 = 新增 iconKind / iconColor / iconEmoji（旧文件读取时补默认值，不丢字段）。
    /// 3 = 新增 folder / iconSource 与顶层 folders（空文件夹也能保留）。
    /// </summary>
    private const int SchemaVersion = 3;

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
    private readonly List<string> _folders = new();
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

    /// <summary>文件夹列表（含空文件夹），顺序按创建/导入时的出现顺序。</summary>
    public IReadOnlyList<string> Folders
    {
        get
        {
            WaitForLoad();
            lock (_gate)
            {
                return _folders.ToArray();
            }
        }
    }

    /// <summary>顶层文件夹（不含 '/'）及其整棵子树的条目数。</summary>
    public IReadOnlyList<(string Name, int Count)> TopFolderCounts()
    {
        return ChildFolderCounts(string.Empty);
    }

    /// <summary>指定文件夹的直接子文件夹（保持 folders 顺序）及各子树条目数；parent 为空表示顶层。</summary>
    public IReadOnlyList<(string Name, int Count)> ChildFolderCounts(string parent)
    {
        WaitForLoad();
        parent = NormalizeFolderName(parent);
        lock (_gate)
        {
            return _folders
                .Where(folder => IsDirectChildLocked(folder, parent))
                .Select(folder => (folder, CountInTreeLocked(folder)))
                .ToArray();
        }
    }

    /// <summary>某个文件夹及其全部子文件夹里的条目数。</summary>
    public int CountInTree(string folder)
    {
        WaitForLoad();
        folder = NormalizeFolderName(folder);
        lock (_gate)
        {
            return CountInTreeLocked(folder);
        }
    }

    private int CountInTreeLocked(string folder)
    {
        if (folder.Length == 0)
        {
            return _items.Count(item => string.IsNullOrWhiteSpace(item.Folder));
        }

        var prefix = folder + "/";
        return _items.Count(item =>
        {
            var current = (item.Folder ?? string.Empty).Trim();
            return string.Equals(current, folder, StringComparison.OrdinalIgnoreCase)
                || current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool IsDirectChildLocked(string candidate, string parent)
    {
        if (string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parent.Length == 0)
        {
            return !candidate.Contains('/');
        }

        var prefix = parent + "/";
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && !candidate.Substring(prefix.Length).Contains('/');
    }

    private static bool IsUnderFolder(string? folder, string ancestor)
    {
        var current = (folder ?? string.Empty).Trim();
        return string.Equals(current, ancestor, StringComparison.OrdinalIgnoreCase)
            || current.StartsWith(ancestor + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RenameUnderFolder(string? folder, string oldPrefix, string newPrefix)
    {
        var current = folder ?? string.Empty;
        if (string.Equals(current, oldPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return newPrefix;
        }

        var prefix = oldPrefix + "/";
        return current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? newPrefix + current.Substring(oldPrefix.Length)
            : current;
    }

    /// <summary>取路径最后一段作为显示名（「网页链接/常用」→「常用」）。</summary>
    internal static string FolderLeaf(string folder)
    {
        var name = NormalizeFolderName(folder);
        var slash = name.LastIndexOf('/');
        return slash < 0 ? name : name.Substring(slash + 1);
    }

    /// <summary>每个文件夹的条目数；含空文件夹（计数 0）。</summary>
    public IReadOnlyList<(string Name, int Count)> FolderCounts()
    {
        WaitForLoad();
        lock (_gate)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in _folders)
            {
                counts[folder] = 0;
            }

            foreach (var item in _items)
            {
                var folder = (item.Folder ?? string.Empty).Trim();
                if (folder.Length > 0)
                {
                    counts[folder] = counts.GetValueOrDefault(folder, 0) + 1;
                }
            }

            return counts.Select(pair => (pair.Key, pair.Value)).ToArray();
        }
    }

    public IReadOnlyList<LinkItem> ItemsInFolder(string folder)
    {
        WaitForLoad();
        folder = NormalizeFolderName(folder);
        lock (_gate)
        {
            return _items
                .Where(item => string.Equals((item.Folder ?? string.Empty).Trim(), folder, StringComparison.OrdinalIgnoreCase))
                .ToArray();
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
        var loadedFolders = new List<string>();

        if (File.Exists(_path))
        {
            try
            {
                (loaded, loadedFolders) = ReadFile(_path);
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
                (loaded, loadedFolders) = ReadFile(_backupPath);
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
            _folders.Clear();
            foreach (var folder in loadedFolders)
            {
                AddFolderLocked(folder);
            }

            // 旧文件（schema 2）没有顶层 folders，靠条目里的 folder 反推。
            foreach (var item in _items)
            {
                AddFolderLocked(item.Folder);
            }

            LoadSource = source;
        }

        stopwatch.Stop();
        var elapsed = stopwatch.Elapsed.TotalMilliseconds;
        HostLog.Info($"store loaded: {loaded.Count} items, {_folders.Count} folders in {elapsed:F2} ms (source={source})");
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
        string iconEmoji = "",
        string folder = "",
        string iconSource = "")
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

        var folderName = NormalizeFolderName(folder);
        if (folderName.Length > 0 && !IsValidFolderName(folderName))
        {
            return new AddResult(AddStatus.InvalidTarget, null, "文件夹名不能包含控制字符，且每段不超过 64 个字符");
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
            Folder = folderName,
            IconSource = NormalizeIconSource(iconSource),
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
            AddFolderLocked(item.Folder);
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

    public UpdateResult Update(
        Guid id,
        string kind,
        string title,
        string target,
        string[]? aliases = null,
        string[]? tags = null,
        string? folder = null,
        string? iconSource = null)
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
            if (folder is not null)
            {
                var folderName = NormalizeFolderName(folder);
                if (folderName.Length > 0 && !IsValidFolderName(folderName))
                {
                    return new UpdateResult(UpdateStatus.InvalidTarget, null, "文件夹名不能包含控制字符，且每段不超过 64 个字符");
                }

                item.Folder = folderName;
                AddFolderLocked(folderName);
            }

            if (iconSource is not null)
            {
                item.IconSource = NormalizeIconSource(iconSource);
            }

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

    /// <summary>新建空文件夹；同名（忽略大小写）时返回失败，不改动现有条目。</summary>
    public FolderResult CreateFolder(string name)
    {
        WaitForLoad();
        var folder = NormalizeFolderName(name);
        if (folder.Length == 0)
        {
            return new FolderResult(false, "文件夹名不能为空");
        }

        if (!IsValidFolderName(folder))
        {
            return new FolderResult(false, "文件夹名不能包含换行/控制字符，且不超过 64 个字符");
        }

        FolderResult result;
        lock (_gate)
        {
            if (_folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
            {
                return new FolderResult(false, $"文件夹已存在：{folder}");
            }

            AddFolderLocked(folder);
            try
            {
                SaveLocked();
                result = new FolderResult(true, $"已新建文件夹：{folder}");
            }
            catch (Exception ex)
            {
                _folders.RemoveAll(name => IsUnderFolder(name, folder));
                HostLog.Error($"store create folder failed: {ex.GetType().Name}: {ex.Message}");
                result = new FolderResult(false, $"写入磁盘失败：{ex.Message}");
            }
        }

        if (result.Success)
        {
            Changed?.Invoke();
        }

        return result;
    }

    /// <summary>重命名文件夹，并把该文件夹下所有条目一起改名；空文件夹同样可改名。</summary>
    public FolderResult RenameFolder(string oldName, string newName)
    {
        WaitForLoad();
        var oldFolder = NormalizeFolderName(oldName);
        var newFolder = NormalizeFolderName(newName);
        if (oldFolder.Length == 0)
        {
            return new FolderResult(false, "文件夹不存在");
        }

        if (newFolder.Length == 0)
        {
            return new FolderResult(false, "新文件夹名不能为空");
        }

        if (string.Equals(oldFolder, CommonFolder, StringComparison.OrdinalIgnoreCase))
        {
            return new FolderResult(false, "「常用」名称固定，不可重命名");
        }

        if (!IsValidFolderName(newFolder))
        {
            return new FolderResult(false, "文件夹名不能包含换行/控制字符，且不超过 64 个字符");
        }

        FolderResult result;
        lock (_gate)
        {
            if (!_folders.Contains(oldFolder, StringComparer.OrdinalIgnoreCase))
            {
                return new FolderResult(false, $"文件夹不存在：{oldFolder}");
            }

            if (string.Equals(oldFolder, newFolder, StringComparison.OrdinalIgnoreCase))
            {
                return new FolderResult(true, "名称未变化");
            }

            if (newFolder.StartsWith(oldFolder + "/", StringComparison.OrdinalIgnoreCase))
            {
                return new FolderResult(false, "不能把文件夹移动到它自己的子层级");
            }

            var conflict = _folders.Any(name =>
                string.Equals(name, newFolder, StringComparison.OrdinalIgnoreCase)
                && !IsUnderFolder(name, oldFolder));
            if (conflict)
            {
                return new FolderResult(false, $"目标文件夹已存在：{newFolder}");
            }

            var previous = _items.ToArray();
            var previousFolders = _folders.ToArray();
            var affected = 0;
            try
            {
                for (var i = 0; i < _folders.Count; i++)
                {
                    _folders[i] = RenameUnderFolder(_folders[i], oldFolder, newFolder);
                }

                foreach (var item in _items)
                {
                    if (IsUnderFolder(item.Folder, oldFolder))
                    {
                        item.Folder = RenameUnderFolder(item.Folder, oldFolder, newFolder);
                        item.UpdatedAt = DateTimeOffset.UtcNow;
                        affected++;
                    }
                }

                SaveLocked();
                result = new FolderResult(true, $"已重命名：{oldFolder} → {newFolder}（{affected} 条）", affected);
            }
            catch (Exception ex)
            {
                _items.Clear();
                _items.AddRange(previous);
                _folders.Clear();
                _folders.AddRange(previousFolders);
                HostLog.Error($"store rename folder failed: {ex.GetType().Name}: {ex.Message}");
                result = new FolderResult(false, $"写入磁盘失败：{ex.Message}");
            }
        }

        if (result.Success)
        {
            Changed?.Invoke();
        }

        return result;
    }

    /// <summary>删除文件夹：只删除“分组”，条目移入同级「常用」（顶层则移回未分类），绝不删除条目。</summary>
    public FolderResult DeleteFolder(string name)
    {
        WaitForLoad();
        var folder = NormalizeFolderName(name);
        if (folder.Length == 0)
        {
            return new FolderResult(false, "文件夹不存在");
        }

        if (string.Equals(folder, CommonFolder, StringComparison.OrdinalIgnoreCase))
        {
            return new FolderResult(false, "「常用」不可删除");
        }

        FolderResult result;
        lock (_gate)
        {
            if (!_folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
            {
                return new FolderResult(false, $"文件夹不存在：{folder}");
            }

            if (_folders.Any(name => name.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)))
            {
                return new FolderResult(false, $"文件夹「{folder}」含子文件夹，请先删除子文件夹");
            }

            var previous = _items.ToArray();
            var previousFolders = _folders.ToArray();
            var destination = folder.StartsWith(RootFolder + "/", StringComparison.OrdinalIgnoreCase) ? CommonFolder : string.Empty;
            var moved = 0;
            try
            {
                _folders.RemoveAll(existing => string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase));
                foreach (var item in _items)
                {
                    if (string.Equals(item.Folder, folder, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Folder = destination;
                        item.UpdatedAt = DateTimeOffset.UtcNow;
                        moved++;
                    }
                }

                SaveLocked();
                var where = destination.Length == 0 ? "未分类" : $"「{FolderLeaf(destination)}」";
                result = new FolderResult(true, $"已删除文件夹：{folder}，{moved} 条移入{where}", moved);
            }
            catch (Exception ex)
            {
                _items.Clear();
                _items.AddRange(previous);
                _folders.Clear();
                _folders.AddRange(previousFolders);
                HostLog.Error($"store delete folder failed: {ex.GetType().Name}: {ex.Message}");
                result = new FolderResult(false, $"写入磁盘失败：{ex.Message}");
            }
        }

        if (result.Success)
        {
            Changed?.Invoke();
        }

        return result;
    }

    /// <summary>把单个条目移入文件夹；空字符串表示移回未分类。</summary>
    public FolderResult MoveToFolder(Guid id, string folder)
    {
        WaitForLoad();
        var target = NormalizeFolderName(folder);
        if (target.Length > 0 && !IsValidFolderName(target))
        {
            return new FolderResult(false, "文件夹名不能包含换行/控制字符，且不超过 64 个字符");
        }

        FolderResult result;
        lock (_gate)
        {
            var item = _items.FirstOrDefault(existing => existing.Id == id);
            if (item is null)
            {
                return new FolderResult(false, "条目不存在");
            }

            if (string.Equals(item.Folder, target, StringComparison.OrdinalIgnoreCase))
            {
                return new FolderResult(true, "已在该文件夹", 0);
            }

            var previousFolder = item.Folder;
            try
            {
                item.Folder = target;
                item.UpdatedAt = DateTimeOffset.UtcNow;
                AddFolderLocked(target);
                SaveLocked();
                result = new FolderResult(true, target.Length == 0
                    ? $"已移回未分类：{item.Title}"
                    : $"已移动到「{target}」：{item.Title}", 1);
            }
            catch (Exception ex)
            {
                item.Folder = previousFolder;
                HostLog.Error($"store move folder failed: {ex.GetType().Name}: {ex.Message}");
                result = new FolderResult(false, $"写入磁盘失败：{ex.Message}");
            }
        }

        if (result.Success && result.Affected > 0)
        {
            Changed?.Invoke();
        }

        return result;
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

                    var sourceFolder = NormalizeFolderName(source.Folder);
                    if (sourceFolder.Length > 0 && !IsValidFolderName(sourceFolder))
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
                        if (sourceFolder.Length > 0)
                        {
                            existing.Folder = sourceFolder;
                            AddFolderLocked(sourceFolder);
                        }

                        if (!string.IsNullOrWhiteSpace(source.IconSource))
                        {
                            existing.IconSource = NormalizeIconSource(source.IconSource);
                        }

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
                        Folder = sourceFolder,
                        IconSource = NormalizeIconSource(source.IconSource),
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    };
                    _items.Add(item);
                    AddFolderLocked(item.Folder);
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
                var (restored, restoredFolders) = ReadFile(_importBackupPath);
                _items.Clear();
                _items.AddRange(restored);
                _folders.Clear();
                foreach (var folder in restoredFolders)
                {
                    AddFolderLocked(folder);
                }

                foreach (var item in _items)
                {
                    AddFolderLocked(item.Folder);
                }

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
        => ReplaceAll(incoming, null);

    /// <param name="folders">
    /// 编辑器显式提交的文件夹顺序（可含空文件夹）；null = 保持现有 folders 并补入条目里的新文件夹。
    /// </param>
    public ReplaceResult ReplaceAll(IReadOnlyList<LinkItem> incoming, IReadOnlyList<string>? folders)
    {
        WaitForLoad();

        var orderedFolders = new List<string>();
        if (folders is not null)
        {
            foreach (var raw in folders)
            {
                var name = NormalizeFolderName(raw);
                if (name.Length == 0)
                {
                    continue;
                }

                if (!IsValidFolderName(name))
                {
                    return Fail(0, $"文件夹名不合法：{name}");
                }

                if (!orderedFolders.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    orderedFolders.Add(name);
                }
            }
        }

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

            var folder = NormalizeFolderName(source.Folder);
            if (folder.Length > 0 && !IsValidFolderName(folder))
            {
                return Fail(line, "文件夹名不能包含换行/控制字符，且不超过 64 个字符");
            }

            var iconSource = NormalizeIconSource(source.IconSource);
            if (!string.IsNullOrWhiteSpace(source.IconSource) && iconSource.Length == 0)
            {
                return Fail(line, "图标来源需为 http:// 或 https:// 开头的完整 URL");
            }

            // 保留完整的文字图标（iTab 常用 2~4 个字符）；渲染层仍取首个字符。
            var emoji = (source.IconEmoji ?? string.Empty).Trim();
            var colorText = (source.IconColor ?? string.Empty).Trim();
            var rawKind = (source.IconKind ?? string.Empty).Trim().ToLowerInvariant();
            var explicitKind = IconKinds.IsValid(rawKind);
            var iconKind = explicitKind ? rawKind : IconKinds.Auto;
            var transparentColor = string.Equals(colorText, "none", StringComparison.OrdinalIgnoreCase);

            if (transparentColor)
            {
                colorText = string.Empty;
            }
            else if (colorText.Length > 0)
            {
                if (!IsHexColor(colorText))
                {
                    return Fail(line, "底色需为 #RRGGBB、none 或留空");
                }

                colorText = colorText.ToUpperInvariant();
            }

            if (explicitKind)
            {
                // 显式 iconKind 优先：编辑器里选「默认图标」时必须把残留的 emoji / 底色清掉，
                // 否则 auto 会被下面的推断逻辑改回 emoji，表现为「切回默认不生效」。
                switch (iconKind)
                {
                    case IconKinds.Auto:
                    case IconKinds.Transparent:
                        emoji = string.Empty;
                        colorText = string.Empty;
                        break;
                    case IconKinds.Block:
                        emoji = string.Empty;
                        break;
                    case IconKinds.Emoji:
                        if (emoji.Length == 0)
                        {
                            iconKind = IconKinds.Auto;
                            colorText = string.Empty;
                        }

                        break;
                }
            }
            else if (emoji.Length > 0)
            {
                iconKind = IconKinds.Emoji;
            }
            else if (colorText.Length > 0)
            {
                iconKind = IconKinds.Block;
            }
            else
            {
                iconKind = transparentColor ? IconKinds.Transparent : IconKinds.Auto;
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
                Folder = folder,
                IconSource = iconSource,
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
            var previousFolders = _folders.ToArray();
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

            if (folders is not null)
            {
                _folders.Clear();
                _folders.AddRange(orderedFolders);
            }

            foreach (var (item, _) in prepared)
            {
                AddFolderLocked(item.Folder);
            }

            total = next.Count;

            try
            {
                SaveLocked();
            }
            catch (Exception ex)
            {
                _items.Clear();
                _items.AddRange(previous);
                _folders.Clear();
                _folders.AddRange(previousFolders);
                HostLog.Error($"store replace save failed: {ex.GetType().Name}: {ex.Message}");
                return Fail(0, $"写入磁盘失败：{ex.Message}");
            }
        }

        Changed?.Invoke();
        return new ReplaceResult(true, total, added, updated, removed, $"已保存 {total} 条（新增 {added}、更新 {updated}、删除 {removed}）", 0);
    }

    private static ReplaceResult Fail(int line, string message)
        => new(false, 0, 0, 0, 0, message, line);

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
                    Folders = new List<string>(_folders),
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
                Folders = new List<string>(_folders),
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

    private static (List<LinkItem> Items, List<string> Folders) ReadFile(string path)
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

        var folders = new List<string>();
        foreach (var folder in data.Folders ?? new List<string>())
        {
            var name = NormalizeFolderName(folder);
            if (IsValidFolderName(name))
            {
                AddFolder(folders, name);
            }
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
            item.Folder = NormalizeFolderName(item.Folder);
            item.IconSource = NormalizeIconSource(item.IconSource);
            if (item.Folder.Length > 0)
            {
                AddFolder(folders, item.Folder);
            }

            if (!LinkKinds.IsValid(item.Kind))
            {
                HostLog.Error($"store load: unknown kind '{item.Kind}' id={item.Id}");
            }
        }

        return (data.Items.Where(item => !string.IsNullOrWhiteSpace(item.Target)).ToList(), folders);
    }

    private static void AddFolder(List<string> folders, string folder)
    {
        if (folder.Length == 0 || folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        folders.Add(folder);
    }

    private void AddFolderLocked(string? folder)
    {
        var name = NormalizeFolderName(folder);
        if (name.Length == 0 || !IsValidFolderName(name))
        {
            return;
        }

        // 路径式文件夹：逐级补齐父级，保证「网页链接/常用」的父级也在列表里。
        var path = string.Empty;
        foreach (var part in name.Split('/'))
        {
            path = path.Length == 0 ? part : path + "/" + part;
            if (!_folders.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                _folders.Add(path);
            }
        }
    }

    internal static string NormalizeFolderName(string? folder)
    {
        var raw = (folder ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        var parts = raw.Split('/')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0);
        return string.Join("/", parts);
    }

    private static bool IsValidFolderName(string folder)
    {
        var parts = folder.Split('/');
        if (parts.Length == 0 || folder.Length > 256)
        {
            return false;
        }

        return parts.All(part => part.Length is > 0 and <= 64 && part.All(ch => !char.IsControl(ch)));
    }

    internal static string NormalizeIconSource(string? source)
    {
        var url = (source ?? string.Empty).Trim();
        if (url.Length == 0)
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
        {
            return string.Empty;
        }

        return url;
    }

    private void SaveLocked()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        foreach (var item in _items)
        {
            AddFolderLocked(item.Folder);
        }

        var data = new LibraryData
        {
            SchemaVersion = SchemaVersion,
            UpdatedAt = DateTimeOffset.UtcNow,
            Folders = new List<string>(_folders),
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
