using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingCmdPal.Actions;

/// <summary>
/// Opens the current query in the Everything window itself
/// (Everything.exe -instance "name" -search "query", per voidtools docs).
/// </summary>
internal sealed partial class OpenInEverythingCommand : InvokableCommand
{
    private readonly string _exe;
    private readonly string _instance;
    private readonly Func<string> _queryProvider;

    public OpenInEverythingCommand(string exe, string instance, Func<string> queryProvider)
    {
        _exe = exe;
        _instance = instance;
        _queryProvider = queryProvider;
        Name = "在 Everything 中打开";
        Icon = new IconInfo("\uE721");
    }

    public override CommandResult Invoke()
    {
        if (string.IsNullOrEmpty(_exe))
        {
            return CommandResult.ShowToast("未找到 Everything.exe");
        }

        var query = _queryProvider();
        if (string.IsNullOrWhiteSpace(query))
        {
            return CommandResult.ShowToast("当前没有搜索词");
        }

        try
        {
            var args = string.IsNullOrEmpty(_instance)
                ? $"-search \"{query}\""
                : $"-instance \"{_instance}\" -search \"{query}\"";

            Process.Start(new ProcessStartInfo(_exe, args) { UseShellExecute = true });
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast(ex.Message);
        }
    }
}
