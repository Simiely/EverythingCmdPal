using System;
using System.IO;
using EverythingCmdPal.Services;

namespace EverythingCmdPal.Pages;

/// <summary>
/// Lists the direct children of one folder. Reuses the whole ResultsPage machinery
/// (paging / sorting / diagnostics / preview); only the query translation differs:
/// search-box text becomes `parent:"folder" text` (void 官方语法，parent: 限直接子项).
/// </summary>
internal sealed class BrowsePage : ResultsPage
{
    private readonly string _folder;

    public BrowsePage(SettingsManager settings, string folder, bool? showDetails = null)
        : base(settings, null, showDetails)
    {
        _folder = folder.TrimEnd('\\');
        Title = Path.GetFileName(_folder) is { Length: > 0 } name ? name : _folder;
        Name = "浏览";
        PlaceholderText = "在当前文件夹内筛选子项名";
        SetPendingQuery(BuildBrowseQuery(_folder, null));
    }

    /// <summary>Pure query builder（SelfTest 直接覆盖此函数）。</summary>
    internal static string BuildBrowseQuery(string folder, string? filter)
    {
        var normalized = folder.TrimEnd('\\');
        return string.IsNullOrWhiteSpace(filter)
            ? $"parent:\"{normalized}\""
            : $"parent:\"{normalized}\" {filter.Trim()}";
    }

    protected override string ToEverythingQuery(string searchText) =>
        BuildBrowseQuery(_folder, searchText);

    protected override string IdleTitle => "此文件夹为空";
    protected override string IdleSubtitle => _folder;
    protected override string NoResultTitle => "没有匹配的子项";
    protected override string NoResultSubtitle => "试着输入子项名的一部分";
}
