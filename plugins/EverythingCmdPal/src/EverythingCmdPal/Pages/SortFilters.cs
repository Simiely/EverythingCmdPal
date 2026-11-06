using System;
using System.Collections.Generic;
using EverythingCmdPal.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingCmdPal.Pages;

/// <summary>
/// 结果页顶部的排序过滤条。宿主的 IFilters 只暴露 CurrentFilterId（没有事件），
/// 所以这里靠 Filters 基类的属性变更通知把用户选择回传给页面。
/// 每个过滤项就是一种「属性 + 升降序」，Id 与设置项取值共用 <see cref="SortOrder"/>。
/// </summary>
internal sealed class SortFilters : Filters
{
    private readonly Action<SortOrder.SortSpec> _onChanged;
    private bool _syncing;

    public SortFilters(SortOrder.SortSpec current, Action<SortOrder.SortSpec> onChanged)
    {
        _onChanged = onChanged;

        _syncing = true;
        CurrentFilterId = current.Id;
        _syncing = false;

        PropChanged += (_, args) =>
        {
            if (_syncing || args.PropertyName != nameof(CurrentFilterId)) return;

            var spec = SortOrder.FromId(CurrentFilterId);
            HostLog.Info($"sort filter -> {spec.Id}");
            _onChanged(spec);
        };
    }

    public override IFilterItem[] GetFilters()
    {
        var items = new List<IFilterItem>(SortOrder.All.Count + 3);
        for (var i = 0; i < SortOrder.All.Count; i++)
        {
            // 每两种属性之间插一个分隔线：名称 | 路径 | 大小 | 修改时间
            if (i > 0 && i % 2 == 0) items.Add(new Separator());

            var spec = SortOrder.All[i];
            items.Add(new Filter { Id = spec.Id, Name = spec.Label });
        }

        return [.. items];
    }
}
