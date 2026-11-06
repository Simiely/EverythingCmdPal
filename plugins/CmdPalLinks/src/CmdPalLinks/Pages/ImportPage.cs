using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Pages;

/// <summary>
/// 导入页（参数页）：原生文件选择 → 解析预览（计数 + 前 20 条非法行）→ 再次点击确认导入。
/// 说明：宿主表单没有“预览列表”这种页面协议，预览信息以参数行 + Toast 呈现；
/// 点击第一次是“解析预览”，第二次是“确认导入”，全部停留在当前页。
/// </summary>
internal sealed partial class ImportPage : ParametersPage
{
    private readonly LibraryStore _store;
    private readonly ImportStateStore _stateStore = new();
    private readonly FilePickerParameterRun _file;
    private readonly CommandParameterRun _mode;
    private readonly LabelRun _status;
    private readonly LabelRun _issues;
    private readonly ListItem _action;
    private ImportPreview? _preview;
    private string _previewSource = string.Empty;
    private ImportRunState? _lastRun;
    private string _resumePath = string.Empty;

    public ImportPage(LibraryStore store)
    {
        _store = store;
        Icon = Icons.Import;
        Title = "导入";
        Name = "导入";

        _file = new ImportFilePicker
        {
            PlaceholderText = "选择 CSV / JSON / TXT 文件",
        };

        var modePage = new StaticParameterList<string>(
            ["skip", "overwrite"],
            (value, item) =>
            {
                item.Title = value == "skip" ? "跳过重复（默认）" : "覆盖重复（合并 alias / tags）";
                item.Icon = value == "skip" ? Icons.Copy : Icons.Edit;
                return item;
            });
        modePage.Title = "重复处理";

        _mode = new CommandParameterRun
        {
            DisplayText = DescribeMode("skip"),
            PlaceholderText = "重复处理",
            Icon = Icons.Copy,
            Command = modePage,
            Required = false,
            Value = "skip",
        };

        modePage.ValueSelected += (_, value) =>
        {
            _mode.Value = value;
            _mode.DisplayText = DescribeMode(value);
            DescribePreview();
        };

        _status = new LabelRun("先选择文件，再点下方「解析预览」。");
        _issues = new LabelRun(string.Empty);
        _action = new ListItem(new ImportActionCommand(this))
        {
            Title = "解析预览",
            Subtitle = "只读取文件，不写入",
            Icon = Icons.Import,
        };

        // 进入页面先恢复“上次导入”摘要；文件还在时可一步重新导入，不再弹选择器。
        _lastRun = _stateStore.Load();
        ApplyLastRun();
    }

    public override IListItem Command => _action;

    public override IParameterRun[] Parameters => [_file, _mode, _status, _issues];

    private static string DescribeMode(object? value)
        => string.Equals(value as string, "overwrite", StringComparison.OrdinalIgnoreCase)
            ? "覆盖重复（合并 alias / tags）"
            : "跳过重复（默认）";

    private bool Overwrite
        => string.Equals(_mode.Value as string, "overwrite", StringComparison.OrdinalIgnoreCase);

    private void ResetPreview()
    {
        _preview = null;
        _previewSource = string.Empty;
        UpdateActionForResume();
        _issues.Text = string.Empty;
    }

    /// <summary>有可用的上次文件时，主按钮直接变成「重新导入上次文件」；否则恢复为选择文件后的解析预览。</summary>
    private void UpdateActionForResume()
    {
        if (!string.IsNullOrEmpty(_resumePath) && File.Exists(_resumePath))
        {
            _action.Title = "重新导入上次文件";
            _action.Subtitle = $"{Path.GetFileName(_resumePath)} · 仍先预览再确认";
            return;
        }

        _action.Title = "解析预览";
        _action.Subtitle = "只读取文件，不写入";
    }

    /// <summary>页面打开时展示上次运行摘要（成功/预览/取消/失败都照实显示，文件不存在时明确提示）。</summary>
    private void ApplyLastRun()
    {
        var last = _lastRun;
        if (last is null || string.IsNullOrWhiteSpace(last.FilePath))
        {
            return;
        }

        var exists = File.Exists(last.FilePath);
        _resumePath = exists ? last.FilePath : string.Empty;

        var when = FormatUsedAt(last.UsedUtc);
        var counts = $"可导入 {last.Addable} · 重复 {last.Duplicates} · 非法 {last.Invalid}";
        _status.Text = exists
            ? $"上次：{last.FileName}（{OutcomeLabel(last.Outcome)} · {when}）\r\n{counts}\r\n可直接点下方按钮重新导入该文件，或先更换文件。"
            : $"上次文件已不存在：{last.FilePath}\r\n（{OutcomeLabel(last.Outcome)} · {when}）· 请重新选择文件。";

        UpdateActionForResume();
    }

    private static string OutcomeLabel(string outcome) => outcome switch
    {
        ImportRunState.OutcomeImported => "已导入",
        ImportRunState.OutcomePreviewed => "仅预览",
        ImportRunState.OutcomeCancelled => "已取消",
        ImportRunState.OutcomeFailed => "失败",
        _ => "无记录",
    };

    private static string FormatUsedAt(string iso)
        => DateTimeOffset.TryParse(iso, out var value) ? value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "时间未知";

    private static ImportRunState BuildState(string outcome, string message, string path, ImportPreview? preview = null)
    {
        var state = new ImportRunState
        {
            Outcome = outcome,
            Message = message,
            FilePath = path,
            FileName = string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path),
            UsedUtc = DateTimeOffset.UtcNow.ToString("O"),
        };

        if (preview is not null)
        {
            state.Format = preview.Format;
            state.TotalRows = preview.TotalRows;
            state.Addable = preview.Addable.Count;
            state.Duplicates = preview.Duplicates.Count;
            state.Invalid = preview.Issues.Count;
            state.Warnings = preview.Warnings.Count;
        }

        return state;
    }

    /// <summary>写盘并同步内存态；文件仍存在时把它的路径记为可一步重试的“上次文件”。</summary>
    private void SaveState(ImportRunState state)
    {
        _lastRun = state;
        if (!string.IsNullOrEmpty(state.FilePath) && File.Exists(state.FilePath))
        {
            _resumePath = state.FilePath;
        }

        _stateStore.Save(state);
    }

    /// <summary>取消时沿用上次同一文件的计数，避免摘要显示成一片 0。</summary>
    private static void CopyCounts(ImportRunState target, ImportRunState? source)
    {
        if (source is null)
        {
            return;
        }

        target.Format = source.Format;
        target.TotalRows = source.TotalRows;
        target.Addable = source.Addable;
        target.Duplicates = source.Duplicates;
        target.Invalid = source.Invalid;
        target.Warnings = source.Warnings;
    }

    private ICommandResult Prepare()
    {
        var file = _file.File;
        var path = file?.Path;
        if (string.IsNullOrWhiteSpace(path) && !string.IsNullOrEmpty(_resumePath) && File.Exists(_resumePath))
        {
            // 没选新文件时直接用上次文件，省掉重新打开选择器。
            path = _resumePath;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            // 「取消」路径：选择器被取消或没选文件就点确认，照实写成 cancelled，不保留旧的成功预览。
            var cancelled = BuildState(ImportRunState.OutcomeCancelled, "未选择文件，已取消", _lastRun?.FilePath ?? string.Empty);
            CopyCounts(cancelled, _lastRun);
            SaveState(cancelled);
            _status.Text = "未选择文件：请点上方「选择 CSV / JSON / TXT 文件」后再试。";
            UpdateActionForResume();
            return Toast("请先选择文件（点上面的「选择 CSV / JSON / TXT 文件」）");
        }

        var result = CsvCodec.ParseFile(path, _store);
        if (result.FatalError is not null || result.Preview is null)
        {
            // 「失败」路径：写入 failed，避免下次打开还显示上次的成功计数。
            SaveState(BuildState(ImportRunState.OutcomeFailed, result.FatalError ?? "解析失败", path));
            ResetPreview();
            _status.Text = "解析失败：" + (result.FatalError ?? "未知错误");
            return Toast(result.FatalError ?? "解析失败");
        }

        _preview = result.Preview;
        _previewSource = path;
        _issues.Text = BuildIssueText(_preview);
        DescribePreview();
        SaveState(BuildState(ImportRunState.OutcomePreviewed, "已解析，尚未写入", path, _preview));

        var addable = _preview.Addable.Count;
        var duplicates = _preview.Duplicates.Count;
        var issues = _preview.Issues.Count;
        var toast = addable == 0 && duplicates == 0
            ? $"解析完成：没有可导入条目（非法 {issues}）"
            : $"解析完成：可导入 {addable} · 重复 {duplicates} · 非法 {issues}";
        return Toast(toast);
    }

    /// <summary>按当前预览与“跳过/覆盖”模式刷新状态行和按钮文案（切换模式时也调用）。</summary>
    private void DescribePreview()
    {
        var preview = _preview;
        if (preview is null)
        {
            return;
        }

        var addable = preview.Addable.Count;
        var duplicates = preview.Duplicates.Count;
        var issues = preview.Issues.Count;
        var warnings = preview.Warnings.Count;

        _status.Text =
            $"源：{Path.GetFileName(preview.Source)}（{preview.Format}）· 共 {preview.TotalRows} 行\r\n" +
            $"可导入 {addable} · 重复 {duplicates} · 非法 {issues} · 警告 {warnings}\r\n" +
            (Overwrite
                ? $"覆盖模式：将新增 {addable}、更新 {duplicates}"
                : $"跳过模式：将新增 {addable}、跳过 {duplicates}；可点上面的「重复处理」切换为覆盖");

        _action.Title = Overwrite ? $"确认导入（新增 {addable}，更新 {duplicates}）" : $"确认导入（新增 {addable}，跳过 {duplicates}）";
        _action.Subtitle = "写入磁盘前会先备份，可在设置页撤销";
    }

    private string BuildIssueText(ImportPreview preview)
    {
        if (preview.Issues.Count == 0)
        {
            return string.Empty;
        }

        var lines = preview.Issues
            .Take(20)
            .Select(issue => $"第 {issue.Line} 行：{issue.Reason}")
            .ToList();

        if (preview.Issues.Count > 20)
        {
            lines.Add($"…另有 {preview.Issues.Count - 20} 条非法行");
        }

        return string.Join("\r\n", lines);
    }

    private ICommandResult Commit()
    {
        var file = _file.File;
        var sourcePath = file?.Path ?? _resumePath;
        var preview = _preview;
        if (preview is null || string.IsNullOrWhiteSpace(sourcePath) || !string.Equals(sourcePath, _previewSource, StringComparison.Ordinal))
        {
            // 文件被更换（或尚未解析）时重新解析，避免把旧文件的预览写进磁盘。
            ResetPreview();
            return Prepare();
        }

        var incoming = preview.Addable.Select(row => row.Item).ToList();
        if (Overwrite)
        {
            incoming.AddRange(preview.Duplicates.Select(row => row.Item));
        }

        var result = _store.ImportBatch(incoming, Overwrite);
        if (result.Status == ImportStatus.Imported)
        {
            SaveState(BuildState(ImportRunState.OutcomeImported, result.Message, _previewSource, preview));
            _status.Text = $"{result.Message} · 现有 {result.Total} 条\r\n可在设置页执行「撤销上次导入」。";
            ResetPreview();
            _file.ClearValue();
            return Toast(result.Message);
        }

        SaveState(BuildState(ImportRunState.OutcomeFailed, result.Message, _previewSource, preview));
        if (result.Status is ImportStatus.Nothing or ImportStatus.NoChanges)
        {
            ResetPreview();
        }

        return Toast(result.Message);
    }

    private static ICommandResult Toast(string message) => CommandResult.ShowToast(new ToastArgs
    {
        Message = message,
        Result = CommandResult.KeepOpen(),
    });

    private sealed partial class ImportActionCommand : InvokableCommand
    {
        private readonly ImportPage _page;

        public ImportActionCommand(ImportPage page)
        {
            _page = page;
        }

        public override string Name => _page._preview is null ? "解析预览" : "确认导入";

        public override IconInfo Icon => Icons.Import;

        public override ICommandResult Invoke()
            => _page._preview is null ? _page.Prepare() : _page.Commit();
    }

    /// <summary>
    /// 只让选择器显示 .csv/.json/.txt：Toolkit 打开选择器时会回调 ConfigureFilePicker（虚方法），
    /// 这个钩子在宿主实现里一定存在（SetupFilePicker 属性并非所有版本都会调用）。
    /// </summary>
    private sealed partial class ImportFilePicker : FilePickerParameterRun
    {
        protected override void ConfigureFilePicker(object? sender, Windows.Storage.Pickers.FileOpenPicker picker)
        {
            picker.FileTypeFilter.Clear();
            picker.FileTypeFilter.Add(".csv");
            picker.FileTypeFilter.Add(".json");
            picker.FileTypeFilter.Add(".txt");
        }
    }
}
