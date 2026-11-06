using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CmdPalLinks.Commands;
using CmdPalLinks.Models;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Pages;

internal sealed partial class ItemsPage : DynamicListPage
{
    private const int PageSize = 100;

    private readonly LibraryStore _store;
    private readonly SettingsManager _settings;
    private readonly IconCache _icons;
    private readonly ImportPage _importPage;
    private IndexedLink[]? _index;
    private string _query = string.Empty;
    private LinkItem[] _results = [];
    private IListItem[] _items = [];
    private int _loaded;
    private bool _dirty = true;
    private int _indexedCount;
    private double _tookMs;

    public ItemsPage(LibraryStore store, SettingsManager settings, IconCache icons, ImportPage importPage)
    {
        _store = store;
        _settings = settings;
        _icons = icons;
        _importPage = importPage;
        Title = "链接库";
        Name = "打开";
        // 图标只放在顶层 CommandItem 上；页面不再设置 Icon，
        // 避免宿主同时渲染“页面/命令图标”和“命令项图标”造成同一行出现两个相同图标。
        PlaceholderText = "搜索标题、别名、域名、文件名…";
        EmptyContent = new CommandItem(new AddItemPage(_store))
        {
            Title = "添加第一个条目",
            Subtitle = "URL / 文件 / 文件夹",
            Icon = Icons.Add,
        };
        ApplyLayout();
        base.HasMoreItems = false;
        _store.Changed += OnStoreChanged;
        _settings.Settings.SettingsChanged += (_, _) =>
        {
            ApplyLayout();
            ResetResults();
            RaiseItemsChanged();
        };
        _icons.Changed += () => RaiseItemsChanged();
    }

    /// <summary>IDL 计算属性：任何时刻读取都返回真实的剩余量，避免宿主缓存首次读取的 false。</summary>
    public override bool HasMoreItems
    {
        get => _loaded < _results.Length;
        set => base.HasMoreItems = value;
    }

    private void OnStoreChanged()
    {
        _index = null;
        ResetResults();
        RaiseItemsChanged();
    }

    public void Refresh()
    {
        ApplyLayout();
        _index = null;
        ResetResults();
        RaiseItemsChanged();
    }

    /// <summary>清空累计数组与缓存结果，下一次 GetItems 重新检索并重建首屏。</summary>
    private void ResetResults()
    {
        _results = [];
        _items = [];
        _loaded = 0;
        _dirty = true;
        base.HasMoreItems = false;
    }

    /// <summary>把设置里的布局落到页面属性；宿主监听属性变化后立即重排。</summary>
    private void ApplyLayout()
    {
        var grid = string.Equals(_settings.Layout, SettingsManager.LayoutGridMedium, StringComparison.OrdinalIgnoreCase)
            ? new MediumGridLayout { ShowTitle = true }
            : null;

        GridProperties = grid;
        HostLog.Info($"layout applied: {_settings.Layout} grid={grid is not null}");
    }

    private IndexedLink[] EnsureIndex()
    {
        if (_index is null)
        {
            var stopwatch = Stopwatch.StartNew();
            _index = SearchEngine.BuildIndex(_store.Items);
            HostLog.Info($"index built: {_index.Length} items in {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        }

        return _index;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _query = newSearch ?? string.Empty;
        ResetResults();
        base.HasMoreItems = false;
        RaiseItemsChanged();
    }

    /// <summary>宿主滚动到底时回调，向累计数组追加一页（IDL: IListPage.HasMoreItems / LoadMore）。</summary>
    public override void LoadMore()
    {
        if (_loaded >= _results.Length)
        {
            base.HasMoreItems = false;
            return;
        }

        _loaded = Math.Min(_loaded + PageSize, _results.Length);
        _items = BuildRows(_loaded, out _);
        base.HasMoreItems = _loaded < _results.Length;
        HostLog.Info($"load more: visible={_loaded} total={_results.Length} more={_loaded < _results.Length}");
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems()
    {
        var started = Stopwatch.StartNew();
        EnsureResults();
        var visible = Math.Min(_loaded, _results.Length);
        _items = BuildRows(visible, out var missingCount);
        base.HasMoreItems = visible < _results.Length;
        var top = _results.Length > 0 ? _results[0].Title : string.Empty;
        HostLog.Info($"search q='{_query}' items={_indexedCount} results={_results.Length} visible={visible} more={visible < _results.Length} missing={missingCount} took={_tookMs:F2} ms build={started.Elapsed.TotalMilliseconds:F2} ms top='{top}'");

        return _items;
    }

    /// <summary>检索只在脏标记时执行，避免宿主高频调用 GetItems 时重复打分。</summary>
    private void EnsureResults()
    {
        if (!_dirty)
        {
            return;
        }

        var outcome = SearchEngine.Search(EnsureIndex(), _query, int.MaxValue, int.MaxValue);
        _results = outcome.Items;
        _indexedCount = outcome.IndexedCount;
        _tookMs = outcome.ElapsedMs;
        // 实测宿主 0.11.11762 从不回调 LoadMore（无「加载更多」哨兵，realized 恒等于返回条数），
        // 因此一次性返回全部结果，保证超过一页的数据也能被访问到。
        _loaded = _results.Length;
        _dirty = false;
        base.HasMoreItems = _loaded < _results.Length;
    }

    private IListItem[] BuildRows(int count, out int missingCount)
    {
        var rows = new List<IListItem>(count);
        missingCount = 0;

        for (var i = 0; i < count; i++)
        {
            var item = _results[i];
            var missing = _icons.IsMissing(item);
            if (missing)
            {
                missingCount++;
            }

            var tags = _settings.ShowTags
                ? item.Tags.Select(tag => (ITag)new Tag(tag)).ToList()
                : new List<ITag>();

            if (missing)
            {
                tags.Add(new Tag("缺失"));
            }

            var subtitle = missing
                ? $"⚠ 路径不存在 · {item.Target}"
                : (_settings.ShowSubtitle ? (_settings.ShowFullPath ? item.Target : KindLabel(item.Kind)) : string.Empty);

            rows.Add(new ListItem(new OpenItemCommand(_store, item))
            {
                Title = item.Title,
                Subtitle = subtitle,
                Tags = tags.ToArray(),
                Icon = _icons.Get(item),
                MoreCommands = BuildCommands(item),
            });
        }

        // 「批量编辑」固定在列表最后，只在未输入检索词时出现（不参与检索、不干扰结果）；
        // 空库时交给 EmptyContent，不追加这一行。导入入口只在行内「更多」里保留。
        if (count >= _results.Length && _results.Length > 0 && string.IsNullOrWhiteSpace(_query))
        {
            // 宿主扩展页不能内嵌 HTML/WebView：这里起回环 HTTP 服务，用系统默认浏览器打开编辑器。
            rows.Add(new ListItem(new OpenHtmlEditorCommand(_store))
            {
                Title = "批量编辑",
                Subtitle = "浏览器打开编辑器，直接读写现有条目",
                Icon = Icons.Edit,
            });
        }

        return rows.ToArray();
    }

    private IContextItem[] BuildCommands(LinkItem item)
    {
        var commands = new List<IContextItem>
        {
            new CommandContextItem(new AddItemPage(_store))
            {
                Title = "添加条目",
                Icon = Icons.Add,
            },
        };

        if (item.Kind != LinkKinds.Url)
        {
            commands.Add(new CommandContextItem(new RevealInFolderCommand(item))
            {
                Title = "打开所在文件夹",
            });
        }

        commands.Add(new CommandContextItem(new CopyTargetCommand(item))
        {
            Title = "复制目标",
        });

        commands.Add(new CommandContextItem(new EditItemPage(_store, item))
        {
            Title = "编辑",
        });

        commands.Add(new CommandContextItem(new DeleteItemCommand(_store, item))
        {
            Title = "删除",
            IsCritical = true,
        });

        // 宿主只给 IContentPage 暴露页面级 Commands，IListPage 没有「页面更多」，
        // 所以导入入口挂到每行的「更多」菜单里（同一个 ImportPage 实例，不新增第二套导入流程）。
        commands.Add(new CommandContextItem(_importPage)
        {
            Title = "导入 CSV",
            Subtitle = "从 CSV / JSON / TXT 批量添加",
            Icon = Icons.Import,
        });

        return commands.ToArray();
    }

    private static string KindLabel(string kind) => kind switch
    {
        LinkKinds.Url => "网址",
        LinkKinds.File => "文件",
        LinkKinds.Folder => "文件夹",
        _ => kind,
    };
}
