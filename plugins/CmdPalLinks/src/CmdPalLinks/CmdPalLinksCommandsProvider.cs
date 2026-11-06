using System;
using System.Threading.Tasks;
using CmdPalLinks.Commands;
using CmdPalLinks.Pages;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks;

public partial class CmdPalLinksCommandsProvider : CommandProvider
{
    private readonly LibraryStore _store;
    private readonly SettingsManager _settings;
    private readonly IconCache _icons;
    private readonly ItemsPage _itemsPage;
    private readonly ImportPage _importPage;
    private readonly CommandItem _libraryCommand;
    private readonly ICommandItem[] _commands;
    private bool _loaded;

    public CmdPalLinksCommandsProvider()
    {
        DisplayName = "链接库";
        Icon = Icons.App;

        _store = new LibraryStore();
        _settings = new SettingsManager();
        _icons = new IconCache();
        // 全扩展只保留一个 ImportPage 实例（列表末项、行内「更多」、设置页共用），
        // 避免多处各持一份参数/预览状态导致“这次进来看不到上次的结果”。
        _importPage = new ImportPage(_store);
        _itemsPage = new ItemsPage(_store, _settings, _icons, _importPage);

        // 导入不再占顶层：首页只保留「链接库」一个入口。
        // 导入入口在设置页命令区、以及资料库列表行「更多」菜单里（同一个 ImportPage）。
        Settings = new LibrarySettings(_settings.Settings, _store, _importPage);

        _libraryCommand = new CommandItem(_itemsPage)
        {
            Title = "链接库",
            Subtitle = "正在加载…",
            // 品牌链条图标只挂在 Provider 上（扩展身份，宿主只画一次）；
            // 顶层命令项改用链接字形，避免同一张 StoreLogo 在首页被画第二遍。
            Icon = Icons.LibraryCommand,
        };

        _commands = [_libraryCommand];

        _store.Changed += OnLibraryChanged;
    }

    private string CountText() => $"共 {_store.Items.Count} 条";

    private void OnLibraryChanged()
    {
        _libraryCommand.Subtitle = CountText();
        RaiseItemsChanged();
    }

    public override ICommandItem[] TopLevelCommands()
    {
        HostLog.Info($"provider: TopLevelCommands called, loaded={_loaded}");
        if (!_loaded)
        {
            _loaded = true;
            _ = LoadInBackground();
        }

        return _commands;
    }

    private async Task LoadInBackground()
    {
        HostLog.Info("provider: background load start");
        try
        {
            _settings.Load();
        }
        catch (Exception ex)
        {
            HostLog.Error($"settings load failed: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            await _store.StartLoading();
        }
        catch (Exception ex)
        {
            HostLog.Error($"store load failed: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        _itemsPage.Refresh();
        _libraryCommand.Subtitle = CountText();
        RaiseItemsChanged();
    }
}
