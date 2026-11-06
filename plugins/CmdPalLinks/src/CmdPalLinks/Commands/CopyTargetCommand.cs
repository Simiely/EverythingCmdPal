using CmdPalLinks.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Commands;

internal sealed partial class CopyTargetCommand : InvokableCommand
{
    private readonly LinkItem _item;

    public CopyTargetCommand(LinkItem item)
    {
        _item = item;
        Name = "复制目标";
        Icon = Icons.Copy;
    }

    public override ICommandResult Invoke(object? sender)
    {
        ClipboardHelper.SetText(_item.Target);
        HostLog.Info($"Copy target: {_item.Target}");

        return CommandResult.ShowToast(new ToastArgs
        {
            Message = $"已复制：{_item.Title}",
            Result = CommandResult.Dismiss(),
        });
    }
}
