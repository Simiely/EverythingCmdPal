using CmdPalLinks.Commands;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Pages;

/// <summary>
/// 设置页：保留原有设置表单，并把「导入 / 导出 / 撤销」挂到页面命令上。
/// 宿主把 ContentPage 的 Commands 作为页面级命令渲染（位置由宿主决定，无法保证在表单下方）。
/// </summary>
internal sealed partial class LibrarySettingsPage : ContentPage
{
    private readonly Settings _settings;

    public LibrarySettingsPage(Settings settings, ImportPage importPage, LibraryStore store)
    {
        _settings = settings;
        Name = "链接库设置";
        Icon = new IconInfo("\uE713");

        Commands =
        [
            new CommandContextItem(importPage)
            {
                Title = "导入 CSV",
                Subtitle = "选择 CSV / JSON / TXT，预览后再写入",
            },
            new CommandContextItem(new ExportJsonCommand(store))
            {
                Title = "导出全部为 JSON",
                Subtitle = "含 icon 字段，可原样回导",
            },
            new CommandContextItem(new ExportCsvCommand(store))
            {
                Title = "导出全部为 CSV",
                Subtitle = "与编辑页表结构一致",
            },
            new CommandContextItem(new UndoImportCommand(store))
            {
                Title = "撤销上次导入",
                Subtitle = "恢复导入前的快照",
            },
        ];

        _settings.SettingsChanged += (_, _) => RaiseItemsChanged();
    }

    public override IContent[] GetContent() => _settings.ToContent();
}

internal sealed partial class LibrarySettings : ICommandSettings
{
    public LibrarySettings(Settings settings, LibraryStore store, ImportPage importPage)
    {
        SettingsPage = new LibrarySettingsPage(settings, importPage, store);
    }

    public IContentPage SettingsPage { get; }
}
