using System;
using System.Collections.Generic;
using System.Linq;
using EverythingCmdPal.Interop;

namespace EverythingCmdPal.Services;

/// <summary>
/// 排序组合（Everything3 属性 ID + 升降序）。Id 同时用于设置项取值和过滤条的
/// <c>Filters.CurrentFilterId</c>，所以两处永远指向同一个排序。
/// </summary>
internal static class SortOrder
{
    internal readonly record struct SortSpec(uint PropertyId, bool Ascending, string Id, string Label);

    internal static readonly SortSpec NameAsc = new(Everything3PropertyIds.Name, true, "name:asc", "名称 ↑");
    internal static readonly SortSpec NameDesc = new(Everything3PropertyIds.Name, false, "name:desc", "名称 ↓");
    internal static readonly SortSpec PathAsc = new(Everything3PropertyIds.Path, true, "path:asc", "路径 ↑");
    internal static readonly SortSpec PathDesc = new(Everything3PropertyIds.Path, false, "path:desc", "路径 ↓");
    internal static readonly SortSpec SizeDesc = new(Everything3PropertyIds.Size, false, "size:desc", "大小 ↓");
    internal static readonly SortSpec SizeAsc = new(Everything3PropertyIds.Size, true, "size:asc", "大小 ↑");
    internal static readonly SortSpec ModifiedDesc = new(Everything3PropertyIds.DateModified, false, "modified:desc", "修改时间 ↓");
    internal static readonly SortSpec ModifiedAsc = new(Everything3PropertyIds.DateModified, true, "modified:asc", "修改时间 ↑");

    internal static readonly IReadOnlyList<SortSpec> All =
    [
        NameAsc, NameDesc, PathAsc, PathDesc, SizeDesc, SizeAsc, ModifiedDesc, ModifiedAsc,
    ];

    internal static SortSpec Default => NameAsc;

    internal static SortSpec FromId(string? id)
        => All.FirstOrDefault(spec => string.Equals(spec.Id, id, StringComparison.Ordinal)) is { Id: not null } match
            ? match
            : Default;
}
