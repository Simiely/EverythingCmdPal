using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Commands;

/// <summary>打开随包的 HTML 编辑器：解出本地副本后用系统默认浏览器打开。</summary>
internal sealed partial class OpenHtmlEditorCommand : InvokableCommand
{
    public OpenHtmlEditorCommand()
    {
        Name = "批量编辑";
        Icon = Icons.Edit;
    }

    public override ICommandResult Invoke(object? sender)
    {
        var path = HtmlEditorLauncher.EnsureLocalCopy();
        if (path is null)
        {
            return CommandResult.ShowToast(new ToastArgs
            {
                Message = "编辑器文件缺失，请重装扩展后再试",
                Result = CommandResult.KeepOpen(),
            });
        }

        var opened = ShellHelpers.OpenInShell(path);
        HostLog.Info($"editor open: {path} result={opened}");

        return opened
            ? CommandResult.KeepOpen()
            : CommandResult.ShowToast(new ToastArgs
            {
                Message = $"无法打开编辑器：{path}",
                Result = CommandResult.KeepOpen(),
            });
    }
}
