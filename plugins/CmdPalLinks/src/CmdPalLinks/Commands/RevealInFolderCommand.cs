using CmdPalLinks.Models;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Commands;

internal sealed partial class RevealInFolderCommand : InvokableCommand
{
    private readonly LinkItem _item;

    public RevealInFolderCommand(LinkItem item)
    {
        _item = item;
        Name = "打开所在文件夹";
        Icon = Icons.FolderOpen;
    }

    public override ICommandResult Invoke(object? sender)
    {
        if (ItemHealth.IsMissing(_item))
        {
            return CommandResult.ShowToast(new ToastArgs
            {
                Message = $"路径不存在：{_item.Target}",
                Result = CommandResult.KeepOpen(),
            });
        }

        var success = _item.Kind == LinkKinds.Folder
            ? ShellHelpers.OpenInShell(_item.Target)
            : ShellHelpers.OpenInShell("explorer.exe", $"/select,\"{_item.Target}\"");

        HostLog.Info($"Reveal requested: {_item.Target} result={success}");

        return success
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(new ToastArgs
            {
                Message = $"无法打开所在文件夹：{_item.Target}",
                Result = CommandResult.KeepOpen(),
            });
    }
}
