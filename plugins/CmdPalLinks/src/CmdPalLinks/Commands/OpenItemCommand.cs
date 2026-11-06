using CmdPalLinks.Models;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Commands;

internal sealed partial class OpenItemCommand : InvokableCommand
{
    private readonly LibraryStore _store;
    private readonly LinkItem _item;

    public OpenItemCommand(LibraryStore store, LinkItem item)
    {
        _store = store;
        _item = item;
        Name = "打开";
        // 列表行已由 ListItem.Icon 提供图标；命令不再带图标，
        // 避免宿主在行内同时渲染列表项图标与命令图标。
    }

    public override ICommandResult Invoke(object? sender)
    {
        HostLog.Info($"Open requested: {_item.Target}");
        _store.RecordOpen(_item.Id);
        var success = ShellHelpers.OpenInShell(_item.Target);
        HostLog.Info($"Open result: {success}");

        return success
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(new ToastArgs
            {
                Message = $"无法打开：{_item.Target}",
                Result = CommandResult.KeepOpen(),
            });
    }
}
