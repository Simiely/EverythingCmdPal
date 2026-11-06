using System;
using CmdPalLinks.Models;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Commands;

internal sealed partial class DeleteItemCommand : InvokableCommand
{
    private readonly LibraryStore _store;
    private readonly LinkItem _item;

    public DeleteItemCommand(LibraryStore store, LinkItem item)
    {
        _store = store;
        _item = item;
        Name = "删除";
        Icon = Icons.Delete;
    }

    public override ICommandResult Invoke(object? sender)
    {
        var removed = _store.Remove(_item.Id);
        HostLog.Info($"Delete requested: id={_item.Id} title='{_item.Title}' result={removed}");

        return removed
            ? CommandResult.ShowToast(new ToastArgs
            {
                Message = $"已删除：{_item.Title}",
                Result = CommandResult.Dismiss(),
            })
            : CommandResult.ShowToast(new ToastArgs
            {
                Message = "条目已不存在",
                Result = CommandResult.KeepOpen(),
            });
    }
}
