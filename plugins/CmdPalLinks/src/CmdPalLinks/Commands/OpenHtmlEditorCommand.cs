using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Commands;

/// <summary>
/// 打开随包的 HTML 编辑器：先在本进程内起一个只绑回环地址的 HTTP 服务，
/// 页面与读写接口同源，编辑器可直接加载现有条目并整表保存回 links.json。
/// </summary>
internal sealed partial class OpenHtmlEditorCommand : InvokableCommand
{
    private readonly LibraryStore _store;

    public OpenHtmlEditorCommand(LibraryStore store)
    {
        _store = store;
        Name = "批量编辑";
        Icon = Icons.Edit;
    }

    public override ICommandResult Invoke(object? sender)
    {
        var url = EditorServer.Start(_store);
        if (url is null)
        {
            return CommandResult.ShowToast(new ToastArgs
            {
                Message = "编辑器服务启动失败，请重试或重装扩展",
                Result = CommandResult.KeepOpen(),
            });
        }

        var opened = ShellHelpers.OpenInShell(url);
        HostLog.Info($"editor open: result={opened}");

        return opened
            ? CommandResult.KeepOpen()
            : CommandResult.ShowToast(new ToastArgs
            {
                Message = "无法打开浏览器，请检查默认浏览器设置",
                Result = CommandResult.KeepOpen(),
            });
    }
}
