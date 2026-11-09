using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using CmdPalLinks.Models;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Pages;

/// <summary>文件夹子列表：只显示该文件夹下的条目，返回上一层的导航由宿主页面栈负责。</summary>
internal sealed partial class FolderItemsPage : DynamicListPage
{
    private readonly LibraryStore _store;
    private readonly SettingsManager _settings;
    private readonly IconCache _icons;
    private readonly ImportPage _importPage;
    private readonly string _folder;
    private IndexedLink[]? _index;
    private string _query = string.Empty;
    private IListItem[] _items = [];

    public FolderItemsPage(
        LibraryStore store,
        SettingsManager settings,
        IconCache icons,
        ImportPage importPage,
        string folder)
    {
        _store = store;
        _settings = settings;
        _icons = icons;
        _importPage = importPage;
        _folder = folder;
        Title = LibraryStore.FolderLeaf(folder);
        Name = "打开文件夹";
        PlaceholderText = "在此文件夹内搜索…";
        EmptyContent = new CommandItem(new AddItemPage(store, folder))
        {
            Title = "此文件夹为空",
            Subtitle = "点此添加条目，或从链接库把条目移进来",
            Icon = Icons.Add,
        };
        _store.Changed += () =>
        {
            _index = null;
            RaiseItemsChanged();
        };
        _icons.Changed += () => RaiseItemsChanged();
        ApplyLayout();
        _settings.Settings.SettingsChanged += (_, _) =>
        {
            ApplyLayout();
            RaiseItemsChanged();
        };
    }

    /// <summary>与主列表同一套布局：设置选网格时子页也是图标磁贴。</summary>
    private void ApplyLayout()
    {
        var grid = string.Equals(_settings.Layout, SettingsManager.LayoutGridMedium, StringComparison.OrdinalIgnoreCase)
            ? new MediumGridLayout { ShowTitle = true }
            : null;

        GridProperties = grid;
        HostLog.Info($"folder layout applied: {_settings.Layout} folder='{_folder}' grid={grid is not null}");
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _query = newSearch ?? string.Empty;
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        var started = Stopwatch.StartNew();
        if (_index is null)
        {
            _index = SearchEngine.BuildIndex(_store.ItemsInFolder(_folder));
        }

        var outcome = SearchEngine.Search(_index, _query, int.MaxValue, int.MaxValue);
        var rows = new List<IListItem>();

        // 先列直接子文件夹（保持 folders 顺序，「常用」因此固定在第一位），再列本层条目。
        if (string.IsNullOrWhiteSpace(_query))
        {
            foreach (var (name, count) in _store.ChildFolderCounts(_folder))
            {
                rows.Add(new ListItem(new FolderItemsPage(_store, _settings, _icons, _importPage, name))
                {
                    Title = LibraryStore.FolderLeaf(name),
                    Subtitle = count == 0 ? "空文件夹" : $"{count} 条",
                    Icon = Icons.FolderRow,
                    MoreCommands = ItemsPage.BuildFolderCommands(_store, name),
                });
            }
        }

        foreach (var item in outcome.Items)
        {
            rows.Add(ItemsPage.BuildItemRow(item, _store, _settings, _icons, _importPage, out _));
        }

        // 任意文件夹（含「网页链接」外的顶层文件夹）都能直接加条目，默认就落在当前层。
        // 只有空文件夹且无子文件夹时保留 EmptyContent 的「此文件夹为空」提示，避免只剩一行添加入口。
        if (string.IsNullOrWhiteSpace(_query) && rows.Count > 0)
        {
            rows.Add(new ListItem(new AddItemPage(_store, _folder))
            {
                Title = "添加到此文件夹",
                Subtitle = $"保存到「{LibraryStore.FolderLeaf(_folder)}」，也可改文件夹字段",
                Icon = Icons.Add,
            });
        }

        _items = rows.ToArray();
        HostLog.Info($"folder page '{_folder}' q='{_query}' rows={_items.Length} took={started.Elapsed.TotalMilliseconds:F2} ms");
        return _items;
    }
}

/// <summary>新建文件夹；可从“移动到文件夹”页带回调，创建后直接把条目移进去。</summary>
internal sealed partial class FolderCreatePage : ContentPage
{
    private readonly FolderCreateForm _form;

    public FolderCreatePage(LibraryStore store)
        : this(store, null)
    {
    }

    public FolderCreatePage(LibraryStore store, Action<string>? created)
    {
        Icon = Icons.FolderAdd;
        Title = "新建文件夹";
        Name = "新建";
        _form = new FolderCreateForm(store, created);
    }

    public override IContent[] GetContent() => new IContent[] { _form };
}

internal sealed partial class FolderCreateForm : FormContent
{
    private readonly LibraryStore _store;
    private readonly Action<string>? _created;

    public FolderCreateForm(LibraryStore store, Action<string>? created)
    {
        _store = store;
        _created = created;
        TemplateJson = FolderFormTemplate.Build("新建文件夹", "文件夹名称", string.Empty, "创建", withLocation: true);
    }

    public override ICommandResult SubmitForm(string payload)
    {
        var root = JsonNode.Parse(payload) as JsonObject;
        var name = LibraryStore.NormalizeFolderName(AddItemForm.Text(root ?? new JsonObject(), "name"));
        var location = AddItemForm.Text(root ?? new JsonObject(), "location");
        // 默认仍建在「网页链接」之下；选「顶层」时才与它同级，名称含 / 时按完整路径处理。
        if (name.Length > 0
            && !name.Contains('/')
            && !string.Equals(location, "top", StringComparison.OrdinalIgnoreCase)
            && _store.Folders.Any(folder => folder.StartsWith(LibraryStore.RootFolder, StringComparison.OrdinalIgnoreCase)))
        {
            name = LibraryStore.RootFolder + "/" + name;
        }

        var result = _store.CreateFolder(name);
        if (!result.Success)
        {
            return CommandResult.ShowToast(new ToastArgs
            {
                Message = result.Message,
                Result = CommandResult.KeepOpen(),
            });
        }

        _created?.Invoke(name);
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = result.Message,
            Result = CommandResult.Dismiss(),
        });
    }
}

/// <summary>重命名文件夹：空文件夹也能改名，条目会跟着改。</summary>
internal sealed partial class FolderRenamePage : ContentPage
{
    private readonly FolderRenameForm _form;

    public FolderRenamePage(LibraryStore store, string folder)
    {
        Icon = Icons.Edit;
        Title = $"重命名：{folder}";
        Name = "重命名";
        _form = new FolderRenameForm(store, folder);
    }

    public override IContent[] GetContent() => new IContent[] { _form };
}

internal sealed partial class FolderRenameForm : FormContent
{
    private readonly LibraryStore _store;
    private readonly string _folder;

    public FolderRenameForm(LibraryStore store, string folder)
    {
        _store = store;
        _folder = folder;
        // 只显示叶子名；重命名时自动保留原有父路径，避免把二级文件夹改成顶层。
        TemplateJson = FolderFormTemplate.Build("重命名文件夹", "新名称", LibraryStore.FolderLeaf(folder), "保存");
    }

    public override ICommandResult SubmitForm(string payload)
    {
        var root = JsonNode.Parse(payload) as JsonObject;
        var name = LibraryStore.NormalizeFolderName(AddItemForm.Text(root ?? new JsonObject(), "name"));
        var slash = _folder.LastIndexOf('/');
        if (name.Length > 0 && !name.Contains('/') && slash >= 0)
        {
            name = _folder.Substring(0, slash + 1) + name;
        }

        var result = _store.RenameFolder(_folder, name);
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = result.Message,
            Result = result.Success ? CommandResult.Dismiss() : CommandResult.KeepOpen(),
        });
    }
}

/// <summary>删除文件夹的确认页：先看影响条数，再点“确认删除”，不会一键清空条目。</summary>
internal sealed partial class FolderDeletePage : ContentPage
{
    private readonly FolderDeleteForm _form;

    public FolderDeletePage(LibraryStore store, string folder)
    {
        Icon = Icons.Delete;
        Title = $"删除文件夹：{folder}";
        Name = "删除";
        _form = new FolderDeleteForm(store, folder);
    }

    public override IContent[] GetContent() => new IContent[] { _form };
}

internal sealed partial class FolderDeleteForm : FormContent
{
    private readonly LibraryStore _store;
    private readonly string _folder;

    public FolderDeleteForm(LibraryStore store, string folder)
    {
        _store = store;
        _folder = folder;
        // 删除前展示整棵子树的条数；若有子文件夹，Store 会拒绝，提示先删子文件夹。
        var count = store.CountInTree(folder);
        var hasChildren = store.Folders.Any(name => name.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
        TemplateJson = FolderFormTemplate.BuildDelete(folder, count, hasChildren);
    }

    public override ICommandResult SubmitForm(string payload)
    {
        var result = _store.DeleteFolder(_folder);
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = result.Message,
            Result = result.Success ? CommandResult.Dismiss() : CommandResult.KeepOpen(),
        });
    }
}

/// <summary>把单个条目移到已有文件夹、未分类，或现场新建一个文件夹。</summary>
internal sealed partial class MoveToFolderPage : DynamicListPage
{
    private readonly LibraryStore _store;
    private readonly IconCache _icons;
    private readonly LinkItem _item;
    private string _query = string.Empty;

    public MoveToFolderPage(LibraryStore store, IconCache icons, LinkItem item)
    {
        _store = store;
        _icons = icons;
        _item = item;
        Title = "移动到文件夹";
        Name = "移动";
        PlaceholderText = "搜索文件夹…";
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _query = newSearch ?? string.Empty;
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        var rows = new List<IListItem>();
        var query = _query.Trim();

        if (query.Length == 0)
        {
            rows.Add(new ListItem(new MoveToFolderCommand(_store, _item, string.Empty))
            {
                Title = "（未分类）",
                Subtitle = "从文件夹移出",
                Icon = Icons.LibraryCommand,
            });
        }

        foreach (var (name, count) in _store.FolderCounts())
        {
            if (query.Length > 0 && !name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            rows.Add(new ListItem(new MoveToFolderCommand(_store, _item, name))
            {
                Title = name,
                Subtitle = count == 0 ? "空文件夹" : $"{count} 条",
                Icon = Icons.FolderRow,
            });
        }

        if (query.Length == 0)
        {
            rows.Add(new ListItem(new FolderCreatePage(_store, name => _store.MoveToFolder(_item.Id, name)))
            {
                Title = "新建文件夹…",
                Subtitle = "创建后直接移入",
                Icon = Icons.FolderAdd,
            });
        }

        return rows.ToArray();
    }
}

internal sealed partial class MoveToFolderCommand : InvokableCommand
{
    private readonly LibraryStore _store;
    private readonly LinkItem _item;
    private readonly string _folder;

    public MoveToFolderCommand(LibraryStore store, LinkItem item, string folder)
    {
        _store = store;
        _item = item;
        _folder = folder;
        Name = folder.Length == 0 ? "移回未分类" : $"移动到 {folder}";
        Icon = folder.Length == 0 ? Icons.LibraryCommand : Icons.FolderRow;
    }

    public override ICommandResult Invoke(object? sender)
    {
        var result = _store.MoveToFolder(_item.Id, _folder);
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = result.Message,
            Result = result.Success ? CommandResult.Dismiss() : CommandResult.KeepOpen(),
        });
    }
}

internal static class FolderFormTemplate
{
    public static string Build(string title, string label, string value, string submit, bool withLocation = false)
    {
        var body = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = title,
                ["weight"] = "Bolder",
                ["wrap"] = true,
            },
        };

        if (withLocation)
        {
            body.Add(new JsonObject
            {
                ["type"] = "Input.ChoiceSet",
                ["id"] = "location",
                ["label"] = "位置",
                ["value"] = "root",
                ["choices"] = new JsonArray
                {
                    new JsonObject { ["title"] = "「网页链接」内", ["value"] = "root" },
                    new JsonObject { ["title"] = "顶层（「网页链接」外）", ["value"] = "top" },
                },
            });
        }

        body.Add(new JsonObject
        {
            ["type"] = "Input.Text",
            ["id"] = "name",
            ["label"] = label,
            ["value"] = value,
            ["isRequired"] = true,
            ["errorMessage"] = "请填写文件夹名称",
            ["placeholder"] = "例如：工作、学习、工具",
        });

        var data = new JsonObject { ["name"] = "name" };
        if (withLocation)
        {
            data["location"] = "location";
        }

        return new JsonObject
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.5",
            ["body"] = body,
            ["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Action.Submit",
                    ["title"] = submit,
                    ["data"] = data,
                },
            },
        }.ToJsonString();
    }

    public static string BuildDelete(string folder, int count, bool hasChildren = false)
    {
        var detail = hasChildren
            ? "该文件夹含子文件夹，需先删除子文件夹后才能删除。"
            : count == 0
                ? "该文件夹为空，删除后不会影响任何条目。"
                : folder.StartsWith(LibraryStore.RootFolder + "/", StringComparison.OrdinalIgnoreCase)
                    ? $"该文件夹下有 {count} 条条目，删除后会全部移入「常用」，不会删除条目。"
                    : $"该文件夹下有 {count} 条条目，删除后会全部移回「未分类」，不会删除条目。";

        return new JsonObject
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.5",
            ["body"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = $"删除文件夹「{folder}」？",
                    ["weight"] = "Bolder",
                    ["wrap"] = true,
                },
                new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = detail,
                    ["wrap"] = true,
                },
            },
            ["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Action.Submit",
                    ["title"] = "确认删除",
                    ["data"] = new JsonObject(),
                },
            },
        }.ToJsonString();
    }
}
