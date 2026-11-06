using System;
using System.IO;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Commands;

internal static class ExportPaths
{
    public static string Directory
        => Path.Combine(Utilities.BaseSettingsPath("CmdPalLinks"), "exports");

    public static string Next(string extension)
    {
        System.IO.Directory.CreateDirectory(Directory);
        return Path.Combine(Directory, $"links-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}");
    }
}

/// <summary>导出全部条目为 JSON（含 icon 字段、schemaVersion=2、updatedAt），可原样回导。</summary>
internal sealed partial class ExportJsonCommand : InvokableCommand
{
    private readonly LibraryStore _store;

    public ExportJsonCommand(LibraryStore store)
    {
        _store = store;
    }

    public override string Name => "导出全部为 JSON";

    public override IconInfo Icon => Icons.Export;

    public override ICommandResult Invoke()
    {
        var path = ExportPaths.Next("json");
        var ok = _store.ExportJson(path);
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = ok ? $"已导出 JSON：{path}" : $"导出 JSON 失败：{path}",
            Result = CommandResult.KeepOpen(),
        });
    }
}

/// <summary>导出 CSV（UTF-8 BOM、CRLF、表头 type,target,title,alias,tags,emoji,color），与编辑页互通。</summary>
internal sealed partial class ExportCsvCommand : InvokableCommand
{
    private readonly LibraryStore _store;

    public ExportCsvCommand(LibraryStore store)
    {
        _store = store;
    }

    public override string Name => "导出全部为 CSV";

    public override IconInfo Icon => Icons.Export;

    public override ICommandResult Invoke()
    {
        var path = ExportPaths.Next("csv");
        var ok = _store.ExportCsv(path);
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = ok ? $"已导出 CSV：{path}" : $"导出 CSV 失败：{path}",
            Result = CommandResult.KeepOpen(),
        });
    }
}

/// <summary>撤销上一次批量导入（恢复写前快照）。</summary>
internal sealed partial class UndoImportCommand : InvokableCommand
{
    private readonly LibraryStore _store;

    public UndoImportCommand(LibraryStore store)
    {
        _store = store;
    }

    public override string Name => "撤销上次导入";

    public override IconInfo Icon => Icons.Undo;

    public override ICommandResult Invoke()
    {
        var result = _store.UndoLastImport();
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = result.Message,
            Result = CommandResult.KeepOpen(),
        });
    }
}
