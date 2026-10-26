using System;
using System.Collections.Generic;

namespace EverythingCmdPal.Services;

/// <summary>
/// Owns one search: current query, pagination offsets, total count and de-duplication.
/// The page only asks for the first page or the next page; it does not track offsets.
/// </summary>
internal sealed class SearchSession
{
    private readonly IEverythingSearch _search;
    private readonly int _pageSize;
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    public SearchSession(IEverythingSearch search, int pageSize)
    {
        _search = search;
        _pageSize = pageSize;
    }

    public string Query { get; private set; } = string.Empty;

    /// <summary>当前这次搜索使用的排序；分页时必须沿用，否则第 2 页会和第 1 页错位。</summary>
    public SortOrder.SortSpec Sort { get; private set; } = SortOrder.Default;

    public int Total { get; private set; }

    public int NextOffset { get; private set; }

    public bool HasMore => NextOffset < Total;

    public IReadOnlyList<SearchHit> Start(string query, SortOrder.SortSpec sort)
    {
        Query = query;
        Sort = sort;
        Total = 0;
        NextOffset = 0;
        _seen.Clear();

        var hits = _search.Search(query, 0, _pageSize, sort, out var total);
        Total = total;
        NextOffset = hits.Count;
        return Dedupe(hits);
    }

    public IReadOnlyList<SearchHit> LoadNext()
    {
        if (!HasMore || string.IsNullOrWhiteSpace(Query))
        {
            return Array.Empty<SearchHit>();
        }

        var hits = _search.Search(Query, NextOffset, _pageSize, Sort, out var total);
        Total = total;
        NextOffset += hits.Count;
        return Dedupe(hits);
    }

    private List<SearchHit> Dedupe(IReadOnlyList<SearchHit> hits)
    {
        var unique = new List<SearchHit>(hits.Count);
        foreach (var hit in hits)
        {
            if (_seen.Add(hit.FullPath))
            {
                unique.Add(hit);
            }
        }

        return unique;
    }
}
