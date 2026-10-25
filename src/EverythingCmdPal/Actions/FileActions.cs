using System;
using System.Diagnostics;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingCmdPal.Actions;

internal sealed partial class OpenPathCommand : InvokableCommand
{
    private readonly string _path;

    public OpenPathCommand(string path)
    {
        _path = path;
        Name = "打开";
        Icon = Icons.File;
    }

    public override CommandResult Invoke()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_path) { UseShellExecute = true });
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast(ex.Message);
        }
    }
}

internal sealed partial class RevealInFolderCommand : InvokableCommand
{
    private readonly string _path;

    public RevealInFolderCommand(string path)
    {
        _path = path;
        Name = "打开所在文件夹";
        Icon = Icons.Folder;
    }

    public override CommandResult Invoke()
    {
        try
        {
            if (Directory.Exists(_path))
            {
                Process.Start(new ProcessStartInfo(_path) { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_path}\"") { UseShellExecute = true });
            }

            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast(ex.Message);
        }
    }
}

internal sealed partial class CopyFullPathCommand : InvokableCommand
{
    private readonly string _path;

    public CopyFullPathCommand(string path)
    {
        _path = path;
        Name = "复制路径";
        Icon = new IconInfo("\uE8C8");
    }

    public override CommandResult Invoke()
    {
        ClipboardHelper.SetText(_path);
        return CommandResult.ShowToast("已复制路径");
    }
}

internal sealed partial class PeekCommand : InvokableCommand
{
    private readonly string _path;

    public PeekCommand(string path)
    {
        _path = path;
        Name = "用 Peek 预览";
        Icon = Icons.Peek;
    }

    public override CommandResult Invoke()
    {
        var exe = PeekLocator.Find();
        if (exe is null)
        {
            return CommandResult.ShowToast("未找到 Peek，请在 PowerToys 中启用 Peek");
        }

        try
        {
            Process.Start(new ProcessStartInfo(exe, $"\"{_path}\"") { UseShellExecute = false });
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast(ex.Message);
        }
    }
}

internal static class PeekLocator
{
    public static string? Find()
    {
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PowerToys", "WinUI3Apps", "PowerToys.Peek.UI.exe");
        if (File.Exists(local)) return local;

        var machine = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerToys", "WinUI3Apps", "PowerToys.Peek.UI.exe");
        if (File.Exists(machine)) return machine;

        return null;
    }
}
